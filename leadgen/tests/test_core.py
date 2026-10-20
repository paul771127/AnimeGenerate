"""不碰網路的單元測試:去重、評分、聯絡資訊抽取、退訂名單。"""
from __future__ import annotations

import sys
from datetime import date, timedelta
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from leadgen.models import (  # noqa: E402
    Lead, extract_emails, extract_phones, is_role_email, normalize_org,
    registrable_domain,
)
from leadgen.pipeline import dedupe  # noqa: E402
from leadgen.scoring import score_lead  # noqa: E402
from leadgen.store import Store  # noqa: E402

SCORING = {
    "weights": {"intent": 30, "budget": 25, "fit": 25, "reach": 10, "fresh": 10},
    "budget_full_at_twd": 300_000,
    "fresh_half_life_days": 30,
    "profile": {"core": ["韌體", "firmware"], "plus": ["iot"], "exclude": ["保全"]},
}


def test_normalize_org():
    assert normalize_org("奧義智慧科技股份有限公司") == normalize_org("奧義智慧科技")
    assert normalize_org("Acme Co., Ltd.") == normalize_org("acme")


def test_registrable_domain():
    assert registrable_domain("https://www.Example.com/contact") == "example.com"
    assert registrable_domain("info@Example.com") == "example.com"
    assert registrable_domain("") == ""


def test_extract_contacts():
    text = "聯絡 info@acme.com.tw 或 sales@acme.com.tw,電話 (02)2911-2241,手機 0912-345-678"
    emails = extract_emails(text)
    assert "info@acme.com.tw" in emails and "sales@acme.com.tw" in emails
    assert any(p.startswith("02") for p in extract_phones(text))
    assert any(p.startswith("0912") for p in extract_phones(text))
    # 圖片檔名不該被當成信箱
    assert extract_emails("logo@2x.png") == []


def test_role_email():
    assert is_role_email("info@acme.com")
    assert is_role_email("contact-us@acme.com")
    assert not is_role_email("jason.lin@acme.com")


def test_dedupe_merges_contacts_across_signals():
    a = Lead(org="Acme", source="gov_tender", source_url="u1",
             signal_title="案 A", signal_type="tender_open",
             website="https://acme.com")
    b = Lead(org="Acme Co., Ltd.", source="remotive", source_url="u2",
             signal_title="Firmware Engineer", signal_type="job",
             website="https://acme.com", emails=["info@acme.com"])
    out = dedupe([a, b])
    assert len(out) == 2, "不同訊號要各自保留一筆"
    assert all("info@acme.com" in l.emails for l in out), "同一組織的聯絡方式要互補"


def test_dedupe_drops_identical_signal():
    mk = lambda: Lead(org="Acme", source="gov_tender", source_url="u",
                      signal_title="案 A", signal_type="tender_open",
                      signal_date="2026-10-01", website="https://acme.com")
    assert len(dedupe([mk(), mk()])) == 1


def test_scoring_ranks_intent_above_registry():
    today = date.today().isoformat()
    hot = Lead(org="A", source="s", source_url="u", signal_type="freelance_request",
               signal_title="SEEKING FREELANCER firmware stm32", signal_date=today,
               emails=["info@a.com"], budget_twd=2_000_000)
    cold = Lead(org="B", source="s", source_url="u", signal_type="registry",
                signal_title="登記資料", signal_date=today)
    hot_score, detail = score_lead(hot, SCORING)
    cold_score, _ = score_lead(cold, SCORING)
    assert hot_score > 85, detail
    # 公司登記資料沒有意圖、沒有聯絡方式、沒有技能契合,只拿到「預算未知」的
    # 中性分與新鮮度,應該遠低於寄信門檻(70)
    assert cold_score < 30, cold_score
    assert hot_score > cold_score


def test_scoring_exclude_zeroes_fit():
    lead = Lead(org="A", source="s", source_url="u", signal_type="tender_open",
                signal_title="保全系統維護", signal_date=date.today().isoformat())
    _, detail = score_lead(lead, SCORING)
    assert detail["fit"] == 0.0


def test_scoring_stale_signal_loses_freshness():
    old = (date.today() - timedelta(days=200)).isoformat()
    lead = Lead(org="A", source="s", source_url="u", signal_type="tender_open",
                signal_title="韌體開發", signal_date=old)
    _, detail = score_lead(lead, SCORING)
    assert detail["fresh"] == 0.0


def test_store_roundtrip_and_suppression(tmp_path):
    store = Store(tmp_path / "t.sqlite3")
    lead = Lead(org="Acme", source="s", source_url="u", signal_title="案",
                signal_type="tender_open", emails=["info@acme.com"],
                website="https://acme.com", score=80)
    assert store.upsert(lead) == "inserted"
    assert store.upsert(lead) == "updated"
    assert len(store.top(min_score=70)) == 1

    store.suppress("acme.com", "unsubscribe")
    assert store.is_suppressed(lead)
    assert store.upsert(lead) == "suppressed", "退訂後不得再寫入"
    store.close()


def test_store_preserves_manual_status(tmp_path):
    store = Store(tmp_path / "t.sqlite3")
    lead = Lead(org="Acme", source="s", source_url="u", signal_title="案",
                signal_type="tender_open", score=80)
    store.upsert(lead)
    store.conn.execute("UPDATE leads SET status='contacted', notes='已寄信'")
    store.conn.commit()
    store.upsert(lead)
    row = store.top()[0]
    assert row["status"] == "contacted" and row["notes"] == "已寄信"
    store.close()


# ---------- 標案分類:設備採購 vs 委託製作 ----------
# 這些標題全部來自政府採購網的真實公告。打樣類關鍵字有九成命中「機關要買一台
# 機器」,對接案者完全無用,所以這個分類器是 gov_tender 名單品質的關鍵。

def test_classify_tender_kinds():
    from leadgen.sources.gov_tender import classify

    cases = [
        # 買現成設備 / 軟體授權 -> 對接案者無用
        ("教學用所需3D列印機採購", "財物類", "equipment"),
        ("工業級大型雷射光固化(SLA)3D快速成型設備壹台", "財物類", "equipment"),
        ("3D逆向工程軟體、高速大尺寸3D列印機採購", "財物類", "equipment"),
        ("辦公室電腦汰換採購", "財物類", "equipment"),
        # 找人做 -> 主場
        ("多數據生理監測裝置設計圖、樣品製作及送測費用", "勞務類", "service"),
        ("文湖線未來車車頭暨駕駛平台模型製作採購案", "財物類", "service"),
        ("焊接協作手臂機器人及應用系統開發案壹式", "勞務類", "service"),
        ("「115年AI資料中台系統開發」採購案", "勞務類842-軟體執行服務", "service"),
        ("積層製造蜂巢流道結構結合牡蠣殼粉陶瓷元件委託製作", "勞務類", "service"),
        # 買零件,但零件要有人做出來 -> 有列印機/能代工的話是機會
        ("電子鎖扣合機構件 等1項", "財物類", "goods"),
        ("進氣端外殼等2項", "財物類", "goods"),
        # 土木工程 -> 不是你的
        ("國小校舍耐震補強工程", "工程類", "construction"),
    ]
    wrong = [(t, classify(t, c), want) for t, c, want in cases if classify(t, c) != want]
    assert not wrong, f"分類錯誤:{wrong}"


def test_classify_strong_verb_beats_purchase_wording():
    """標題同時有「採購」和「開發」時,要判成 service —— 機關常把委外案寫成採購案。"""
    from leadgen.sources.gov_tender import classify
    assert classify("115年度物聯網監測系統開發採購案", "財物類") == "service"


def test_tender_goods_scores_below_service():
    from leadgen.models import Lead
    from datetime import date
    mk = lambda t: Lead(org="機關", source="gov_tender", source_url="u",
                        signal_title="韌體開發案", signal_type=t,
                        signal_date=date.today().isoformat(), budget_twd=1_000_000)
    svc, _ = score_lead(mk("tender_open"), SCORING)
    goods, _ = score_lead(mk("tender_goods"), SCORING)
    assert svc > goods


def test_classify_material_purchase_is_not_a_job():
    """「3D列印暨無人機組裝材料等2案」有弱動詞「組裝」,但它買的是線材不是你的工時。"""
    from leadgen.sources.gov_tender import classify
    assert classify("3D列印暨無人機組裝材料等2案", "財物類") == "equipment"
    assert classify("3D列印線材一批", "財物類") == "equipment"
    # 但「材料」出現在要做的東西名稱裡時不能誤殺
    assert classify("複合材料機構件設計與製作", "勞務類") == "service"
    assert classify("明潭發電機組定子線圈熱壓模具及特殊治具長期供料一批", "財物類") == "service"


def test_dedupe_collapses_correction_notices():
    """機關發更正公告時同一個案子會以不同日期出現兩次,只該留最新的。"""
    base = dict(org="臺北大眾捷運股份有限公司", source="gov_tender",
                signal_title="文湖線未來車車頭暨駕駛平台模型製作採購案",
                signal_type="tender_open", website="https://metro.taipei")
    old = Lead(**base, source_url="u1", signal_date="2026-09-01")
    new = Lead(**base, source_url="u2", signal_date="2026-09-20",
               emails=["info@metro.taipei"])
    out = dedupe([old, new])
    assert len(out) == 1
    assert out[0].signal_date == "2026-09-20"
    assert out[0].emails == ["info@metro.taipei"], "舊公告的聯絡方式要併進新的"


def test_dedupe_keeps_award_and_open_separately():
    """同一個案子的招標公告與決標公告是兩種情報,不能合併。"""
    base = dict(org="機關", source="gov_tender", signal_title="某案",
                website="https://a.gov.tw")
    out = dedupe([
        Lead(**base, source_url="u1", signal_type="tender_open", signal_date="2026-09-01"),
        Lead(**base, source_url="u2", signal_type="tender_award", signal_date="2026-10-01"),
    ])
    assert len(out) == 2


def test_unknown_budget_is_not_zero():
    """預算未公開的標案不該被當成預算是 0 來扣分。"""
    from datetime import date
    mk = lambda b: Lead(org="機關", source="gov_tender", source_url="u",
                        signal_title="韌體開發案", signal_type="tender_open",
                        signal_date=date.today().isoformat(), budget_twd=b)
    unknown, _ = score_lead(mk(None), SCORING)
    tiny, _ = score_lead(mk(1000), SCORING)
    big, _ = score_lead(mk(5_000_000), SCORING)
    assert tiny < unknown < big, f"{tiny} / {unknown} / {big}"


def test_short_chinese_title_can_reach_high_fit():
    """中文標案標題很短,命中兩個核心關鍵字就該接近滿分契合度。"""
    from datetime import date
    lead = Lead(org="機關", source="gov_tender", source_url="u",
                signal_title="機構設計與韌體開發委託案", signal_type="tender_open",
                signal_date=date.today().isoformat(), emails=["info@a.gov.tw"],
                budget_twd=2_000_000)
    score, detail = score_lead(lead, {**SCORING, "profile": {
        "core": ["韌體", "機構設計"], "plus": [], "exclude": []}})
    assert detail["fit"] >= 23, detail
    assert score >= 85, detail


def test_tender_keywords_avoid_chinese_substring_traps():
    """中文沒有詞界,有些短關鍵字會被吞進完全無關的詞裡。

    實測踩到的:關鍵字「試作」命中「…向量圖磚測<試作>業採購」,那是測繪案。
    這跟「治具櫃」仍然是治具、「原型機」仍然是原型不一樣 —— 那些是合法的
    複合詞,靠 classify() 分類就夠了。這條測試只擋跨詞邊界被吞掉的那幾個。
    """
    import yaml
    cfg = yaml.safe_load(
        (Path(__file__).resolve().parents[1] / "config.yaml").read_text(encoding="utf-8")
    )
    keywords = set(cfg["sources"]["gov_tender"]["keywords"])
    banned = {
        "試作": "會被「測試作業」「考試作業」吞掉",
        "模型": "會命中「數值模型」「氣候模型」等研究案",
        "設計": "太泛,會命中「設計監造」等土木案",
        "製作": "太泛,會命中各種影片與文宣製作",
        "開發": "太泛,會命中「土地開發」「觀光開發」",
    }
    bad = [f"{kw}({why})" for kw, why in banned.items() if kw in keywords]
    assert not bad, f"這些關鍵字會撈到無關的案子:{bad}"


def test_extract_urls_skips_non_company_hosts():
    """徵求外包的人會貼自家網址,但也常貼 ATS 與社群連結 —— 後者爬了找不到人。"""
    from leadgen.models import extract_urls
    body = ("Markforged | Software Engineers | https://www.markforged.com "
            "apply at https://boards.greenhouse.io/markforged "
            "or see https://github.com/markforged and https://linkedin.com/company/x")
    assert extract_urls(body) == ["https://www.markforged.com"]
    # 只有平台連結時要回空,不能把 ATS 當成官網去爬
    assert extract_urls("apply https://jobs.lever.co/acme") == []
    assert extract_urls("see https://news.ycombinator.com/item?id=1") == []
    assert extract_urls("no links here") == []


def test_extract_urls_dedupes_by_host_and_trims_punctuation():
    from leadgen.models import extract_urls
    out = extract_urls("see https://acme.io/about, and https://acme.io/contact.")
    assert out == ["https://acme.io/about"], out


# ---------- 實測踩到的名單污染 ----------

def test_email_regex_rejects_package_versions():
    """shapertools 的頁面讓我們抓到 leaflet@1.9.4 和 flag-icons@6.6.6 當信箱。"""
    assert extract_emails("leaflet@1.9.4 flag-icons@6.6.6 vue@3.4.21") == []
    assert extract_emails("softwarejobs@spacex.com--") == ["softwarejobs@spacex.com"]


def test_placeholder_emails_are_rejected():
    """網頁範本裡的示範信箱寄出去就是退信。"""
    from leadgen.models import is_placeholder_email
    for e in ("you@awesome.sauce", "example@domain.com", "your@yourcompany.com",
              "name@example.org", "test@foo.invalid"):
        assert is_placeholder_email(e), e
    for e in ("hiring@oxefit.com", "jobs@caladan.bio", "hr@powertools-tech.com"):
        assert not is_placeholder_email(e), e


def test_platform_domain_does_not_merge_unrelated_orgs():
    """SpaceX 和 Canonical 都貼 grnh.se 短網址,原本被當成同一個組織合併,
    Canonical 因此拿到了 SpaceX 的招募信箱。"""
    a = Lead(org="SpaceX", source="hn_freelance", source_url="u1",
             website="https://grnh.se/b6237bca2us")
    b = Lead(org="Canonical", source="hn_freelance", source_url="u2",
             website="https://grnh.se/32b29bd31us")
    assert a.dedupe_key != b.dedupe_key
    out = dedupe([a, b])
    assert len(out) == 2


def test_hn_org_name_is_the_company_not_the_sentence():
    from leadgen.sources.hn_freelance import _org_name
    assert _org_name("Zoo.dev is hiring a Senior Frontend Engineer focused on 3D") == "Zoo.dev"
    assert _org_name("Charge Robotics | Senior Mechanical Engineer | Onsite") == "Charge Robotics"
    assert _org_name("Elementary Robotics https://elementaryrobotics.com | Firmware") == "Elementary Robotics"
    assert _org_name('Woods Hole Oceanographic Institute "We go to impossible places"') == \
        "Woods Hole Oceanographic Institute"


# ---------- PTT 發案貼文解析 ----------

def test_ptt_line_id_regex_rejects_chinese_and_substrings():
    """\\w 在 Python 包含中文,原本抓到「LINE: 所在地區」當帳號;
    而且 line 沒有詞界,「StreamlineBio」裡的 line 也被當成 LINE 帳號。"""
    from leadgen.sources.ptt import _LINE_ID, _looks_like_line_id

    def ids(text):
        return [m.group(1) for m in _LINE_ID.finditer(text)
                if _looks_like_line_id(m.group(1))]

    assert ids("聯絡方式:line ID:katzeodin") == ["katzeodin"]
    assert ids("請先line:nealrzl 說明需求") == ["nealrzl"]
    assert ids("賴:kula3290") == ["kula3290"]
    assert ids("StreamlineBio-資深機構/機械工程師") == []
    assert ids("LINE: 所在地區") == []
    assert ids("line id: 所在地區跟報價") == []


def test_ptt_skips_selling_and_transfer_posts():
    """[徵求] 板上也有賣東西和轉讓課程的貼文,那些不是案源。"""
    from leadgen.sources.ptt import _NOT_A_JOB
    for title in ("[徵求] 售 3D列印機-ATOM 2 正常可印", "[徵求] PTC Creo 課程轉讓",
                  "[徵求] 二手銑床", "[徵求] 面交 鋁板"):
        assert _NOT_A_JOB.search(title), title
    # 不能誤殺:單字的售/賣要有詞界
    for title in ("[發案] 銷售系統開發", "[徵求] 新竹 電木NC加工廠", "[發案] python爬蟲"):
        assert not _NOT_A_JOB.search(title), title


def test_ptt_budget_parsing():
    from leadgen.sources.ptt import _parse_budget
    assert _parse_budget("約5000～7000元之間") == 5000
    assert _parse_budget("1.5萬") == 15000
    assert _parse_budget("一週內") is None      # 不能把工期當預算
    assert _parse_budget("") is None


def test_ptt_deadline_year_rollover():
    """徵求期限只寫「1/5」,年份要從發文日期推 —— 12 月發文表示跨年。"""
    from datetime import date
    from leadgen.sources.ptt import _parse_deadline
    assert _parse_deadline("10/21", date(2026, 10, 19)) == "2026-10-21"
    assert _parse_deadline("1/5", date(2026, 12, 20)) == "2027-01-05"
    assert _parse_deadline("", date(2026, 10, 19)) == ""


# ---------- 個人接案者投得了的標案 ----------

def test_individual_friendly_tender_types():
    """大案子的招標文件要營業登記與押標金,個人過不了資格審。
    真正吃得到的是 RFI 與未達公告金額的報價單案。"""
    from leadgen.sources.gov_tender import INDIVIDUAL_FRIENDLY_TYPES
    assert "公開徵求廠商提供參考資料公告" in INDIVIDUAL_FRIENDLY_TYPES
    assert "公開取得報價單或企劃書公告" in INDIVIDUAL_FRIENDLY_TYPES
    assert not any("公開招標公告" == t for t in INDIVIDUAL_FRIENDLY_TYPES)


def test_marketplace_project_scores_as_reachable():
    """外包平台沒有 email,是站內投標。少了 contacts_other 這類來源的
    reach 會永遠是 0 分,整個來源排不進寄信名單。"""
    from datetime import date
    lead = Lead(org="Freelancer 案件 #1", source="freelancer", source_url="u",
                signal_title="Embedded STM32 firmware", signal_type="marketplace_project",
                signal_date=date.today().isoformat(), budget_twd=200_000,
                contacts_other=["freelancer.com 站內投標"])
    score, detail = score_lead(lead, {**SCORING, "profile": {
        "core": ["embedded", "firmware"], "plus": [], "exclude": []}})
    assert lead.has_contact
    assert detail["reach"] >= 8, detail
    assert score >= 85, detail


# ---------- 外包平台的三個陷阱 ----------

def test_freelancer_onsite_detection():
    """平台的 local 旗標會漏:「On-Site ABB VFD Analysis」是 local=False
    卻明顯要到現場。從台灣接不了的案子不該進名單。"""
    from leadgen.sources.freelancer import ONSITE
    for t in ("On-Site ABB VFD Analysis and Repair",
              "UK-Based Electronics Specialist", "Must be based in Germany",
              "In-person training needed", "Local only please"):
        assert ONSITE.search(t), t
    for t in ("Embedded STM32 BLDC Control Development",
              "Semi-Automatic Pallet Shuttle Design", "3D Figurine Modeling"):
        assert not ONSITE.search(t), t


def test_freelancer_hourly_budget_is_not_compared_as_project_value():
    """一個 $15/hr 的海報案換算成「預算」只有 480 台幣,拿它去比固定價的
    預算下限是拿蘋果比橘子 —— 時薪案要用費率門檻判斷。"""
    from leadgen.sources.freelancer import _budget_twd
    hourly = {"budget": {"minimum": 15.0}, "currency": {"exchange_rate": 1.0}}
    assert _budget_twd(hourly) == 480            # 這是時薪,不是專案金額
    fixed = {"budget": {"minimum": 5000.0}, "currency": {"exchange_rate": 1.0}}
    assert _budget_twd(fixed) == 160_000
    # 非美金要先換成美金再換台幣
    inr = {"budget": {"minimum": 150000.0}, "currency": {"exchange_rate": 0.0105}}
    assert 48_000 < _budget_twd(inr) < 53_000
