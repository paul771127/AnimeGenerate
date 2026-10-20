"""政府電子採購網(標案)。

資料來自 g0v / openfun 的公開採購網 API,原始資料是依政府採購法公告的公開資訊,
機關聯絡人與電話本來就是「公告給廠商聯絡用」的,所以這是整套系統裡法律風險最低、
意圖訊號最強的來源。

對軟體 / 韌體 / 機構接案者的用法:
  * 招標中的案子 -> 直接投標或找得標廠商當二手承包
  * 已決標的案子 -> 看哪些機關固定在買這類服務,以及誰在搶(競爭者名單)
"""
from __future__ import annotations

import logging
import re
from datetime import date, datetime, timedelta
from typing import Iterable

from ..http import Fetcher, RobotsDenied
from ..models import Lead, extract_emails, extract_phones
from .base import register

log = logging.getLogger("leadgen.gov_tender")

API = "https://pcc-api.openfun.app/api"

# 還可以投標的公告類型 = 高意圖
OPEN_TYPES = (
    "公開招標公告",
    "公開取得報價單或企劃書公告",
    "經公開評選或公開徵求之限制性招標公告",
    "公開徵求廠商提供參考資料公告",
    "限制性招標公告",
)
# 已決標 = 情報價值(誰在買、誰在搶),意圖分數較低
AWARD_TYPES = ("決標公告", "無法決標公告")

# 個人接案者投得了的公告類型。
#
# 政府採購法第 8 條的「廠商」確實包含自然人,但實務上大案子的招標文件會要求
# 營業登記、納稅證明與押標金,個人過不了資格審。真正吃得到的是這兩種:
#   * 公開徵求廠商提供參考資料:這是 RFI,只是請你提供資料,沒有資格門檻,
#     而且是讓機關認識你最便宜的方式 —— 後續的案子常常就找有回應過的人。
#   * 公開取得報價單或企劃書:用於未達公告金額的採購,機關可以直接洽個人。
# 「公開招標」與「限制性招標」預設排除,那些幾乎都要公司行號。
INDIVIDUAL_FRIENDLY_TYPES = (
    "公開徵求廠商提供參考資料公告",
    "公開取得報價單或企劃書公告",
)
# 細節頁的「採購金額級距」寫這個,就是小額採購
SMALL_VALUE = "未達公告金額"

_MONEY = re.compile(r"([\d,]+)\s*元")


def _parse_money(s: str) -> int | None:
    if not s:
        return None
    m = _MONEY.search(s)
    if not m:
        return None
    try:
        return int(m.group(1).replace(",", ""))
    except ValueError:
        return None


# 「機關要買一台機器」和「機關要找人做東西」在標題上混在一起。對接案者來說
# 前者完全無用(他們買的是設備,不是你的工時),而打樣類關鍵字有九成命中前者,
# 所以要先把它們分開 —— 不做這件事,「3D列印」一個關鍵字就會灌進 1800 筆垃圾。
EQUIP_NOISE = re.compile(
    r"(列印機|印表機|掃描[器儀機]|工作站|電腦|筆電|伺服器|投影|螢幕|設備|儀器|機台|"
    r"耗材|碳粉|墨水|試劑|標準品|濾材|刀具|軟體(採購|授權|更新)|授權|訂閱|保固|保養|清潔)"
)
BUY_VERB = re.compile(r"(採購|購置|購買|租賃|續約|汰換|增購)")
# 強動詞:明確是「委外找人做」,就算標題也有「採購」兩字也算你的機會
STRONG_MAKE = re.compile(
    r"(委託|委辦|委外|技術服務|勞務|開發|設計|研發|打樣|試作|客製|改版|整合|撰寫)"
)
# 弱動詞:可能是找人做,也可能只是買現成品的描述,要讓設備採購的判斷先走
WEAK_MAKE = re.compile(r"(製作|建置|加工|組裝|改裝|製造|建模|逆向|供料)")
# 買材料/耗材。「3D列印暨無人機組裝材料等2案」有「組裝」這個弱動詞,
# 但它要買的是線材不是你的工時,所以材料 + 數量詞要先攔下來。
MATERIAL_BUY = re.compile(r"(材料|耗材|線材|耗品|備品|零配件)")
QTY = re.compile(r"(等\s*\d+\s*[案項式]|一批|乙批|\d+\s*[台臺套組箱])")

_ROC = re.compile(r"(\d{2,3})/(\d{1,2})/(\d{1,2})")


def _parse_roc(s: str) -> str:
    """民國日期 "115/08/21 17:00" -> "2026-08-21"。"""
    m = _ROC.search(s or "")
    if not m:
        return ""
    y, mo, d = (int(x) for x in m.groups())
    try:
        return date(y + 1911, mo, d).isoformat()
    except ValueError:
        return ""


def _parse_date(n: int) -> str:
    try:
        return datetime.strptime(str(n), "%Y%m%d").date().isoformat()
    except ValueError:
        return ""


def classify(title: str, category: str) -> str:
    """把標案分成四類。

      service      找人做(設計、開發、委託製作)—— 你的主場
      goods        買東西,但買的是「零件/機構件/治具」這類要有人做出來的東西。
                   對自己有列印機、能代工供貨的人來說這是真的機會,所以不丟掉,
                   只是意圖分數低一些。
      equipment    買現成設備(列印機、電腦、軟體授權)—— 對你完全無用,預設丟掉
      construction 土木工程 —— 預設丟掉
    """
    if category.startswith("工程類"):
        return "construction"
    if STRONG_MAKE.search(title):
        return "service"
    if EQUIP_NOISE.search(title) and BUY_VERB.search(title):
        return "equipment"
    if MATERIAL_BUY.search(title) and QTY.search(title):
        return "equipment"
    if WEAK_MAKE.search(title):
        return "service"
    if EQUIP_NOISE.search(title):
        return "equipment"
    return "service" if category.startswith("勞務類") else "goods"


@register
class GovTender:
    name = "gov_tender"

    def fetch(self, fetcher: Fetcher, cfg: dict) -> Iterable[Lead]:
        keywords: list[str] = cfg.get("keywords", [])
        max_pages = int(cfg.get("max_pages", 2))
        within_days = int(cfg.get("within_days", 120))
        detail_budget = int(cfg.get("detail_fetches", 40))  # 細節頁的抓取上限
        include_awards = bool(cfg.get("include_awards", True))
        drop_expired = bool(cfg.get("drop_expired", True))
        # 預設丟掉「買設備」與土木工程,只留「找人做」的案子
        drop_kinds = set(cfg.get("drop_kinds", ["equipment", "construction"]))
        # 只留個人接案者投得了的案子(沒有公司行號時的預設)
        individual_only = bool(cfg.get("individual_only", True))
        cutoff = date.today() - timedelta(days=within_days)

        seen: set[str] = set()
        # (lead, rec) 的暫存。細節頁(聯絡信箱、預算、截止投標)每筆要一次請求,
        # 額度有限,所以不能邊抓邊花 —— 照關鍵字順序先到先用會讓排在後面的
        # 關鍵字被餓死,拿不到信箱就等於沒有 reach 分數。
        pending: list[tuple[Lead, dict]] = []

        for kw in keywords:
            for page in range(1, max_pages + 1):
                try:
                    data = fetcher.get_json(
                        f"{API}/searchbytitle", params={"query": kw, "page": page}
                    )
                except (RobotsDenied, RuntimeError, ValueError) as e:
                    log.warning("標案查詢失敗 kw=%s page=%s: %s", kw, page, e)
                    break

                records = data.get("records") or []
                if not records:
                    break

                for rec in records:
                    brief = rec.get("brief") or {}
                    btype = brief.get("type", "")
                    is_open = any(t in btype for t in OPEN_TYPES)
                    is_award = any(t in btype for t in AWARD_TYPES)
                    if not (is_open or (include_awards and is_award)):
                        continue

                    category = brief.get("category", "") or ""
                    kind = classify(brief.get("title", "") or "", category)
                    if kind in drop_kinds:
                        continue

                    sig_date = _parse_date(rec.get("date", 0))
                    if sig_date and date.fromisoformat(sig_date) < cutoff:
                        continue

                    key = f"{rec.get('unit_id')}|{rec.get('job_number')}|{btype}"
                    if key in seen:
                        continue
                    seen.add(key)

                    unit = rec.get("unit_name", "")
                    page_url = "https://web.pcc.gov.tw/prkms/tender/common/basic/readTenderBasic"
                    public_url = rec.get("url", "")
                    source_url = (
                        f"https://pcc.g0v.ronny.tw{public_url}" if public_url else page_url
                    )

                    lead = Lead(
                        org=unit,
                        source=self.name,
                        source_url=source_url,
                        signal_title=brief.get("title", ""),
                        signal_type=(
                            ("tender_open" if kind == "service" else "tender_goods")
                            if is_open else "tender_award"
                        ),
                        signal_date=sig_date,
                        tags=[t for t in [kw, category, f"kind:{kind}"] if t],
                        evidence=f"{btype}:{brief.get('title','')}",
                        notes=f"案號 {rec.get('job_number','')} / {btype}",
                    )

                    winners = (brief.get("companies") or {}).get("names") or []
                    if is_award and winners:
                        lead.notes += f" / 得標:{'、'.join(winners[:3])}"

                    pending.append((lead, rec if is_open else {}))

        # 細節頁的額度優先給「還能投標、而且公告最新」的案子
        detail_targets = sorted(
            (pair for pair in pending if pair[1]),
            key=lambda pair: pair[0].signal_date or "",
            reverse=True,
        )[:detail_budget]
        openable = sum(1 for pair in pending if pair[1])
        log.info("抓細節頁 %s / %s 筆可投標公告", len(detail_targets), openable)
        for lead, rec in detail_targets:
            self._attach_detail(fetcher, rec, lead)

        for lead, _ in pending:
            if individual_only and lead.signal_type in ("tender_open", "tender_goods"):
                btype = lead.notes
                friendly = any(t in btype for t in INDIVIDUAL_FRIENDLY_TYPES)
                # 細節頁有抓到的話,小額採購一律算個人可投
                if not friendly and any(t == f"級距:{SMALL_VALUE}" for t in lead.tags):
                    friendly = True
                if not friendly:
                    continue
                lead.tags.append("個人可投")

            # 截止投標只在細節頁才知道,所以過期判斷要等細節抓完才做
            deadline = next(
                (t[9:] for t in lead.tags if t.startswith("deadline:")), ""
            )
            if deadline and date.fromisoformat(deadline) < date.today():
                if drop_expired:
                    continue
                lead.signal_type = "tender_expired"
            yield lead

    def _attach_detail(self, fetcher: Fetcher, rec: dict, lead: Lead) -> None:
        url = f"{API}/tender"
        params = {"unit_id": rec.get("unit_id"), "job_number": rec.get("job_number")}
        try:
            d = fetcher.get_json(url, params=params)
        except Exception as e:  # 細節頁失敗不該讓整批掛掉
            log.debug("標案細節失敗 %s: %s", params, e)
            return
        recs = d.get("records") or []
        if not recs:
            return
        det = recs[0].get("detail") or {}

        for k, v in det.items():
            if "聯絡電話" in k or "傳真" in k:
                lead.phones.extend(extract_phones(str(v)))
            elif "機關地址" in k:
                lead.location = str(v)
            elif "電子郵件信箱" in k:
                # 這是公告裡「請廠商聯絡此窗口」的指定信箱,蒐集目的與公告目的一致
                lead.emails.extend(extract_emails(str(v)))
            elif "聯絡人" in k:
                # 只留稱謂(「雷先生」),不展開成可識別的個人資料
                lead.notes += f" / 聯絡窗口:{v}"
            elif "預算金額" in k and "是否" not in k:
                lead.budget_twd = _parse_money(str(v))
            elif k.endswith("截止投標"):
                if (dl := _parse_roc(str(v))):
                    lead.tags.append(f"deadline:{dl}")
                    lead.notes += f" / 截止投標 {dl}"
            elif "採購金額級距" in k:
                lead.tags.append(f"級距:{str(v).strip()}")
            elif k.endswith("履約期限"):
                lead.notes += f" / 履約 {v}"
        lead.phones = list(dict.fromkeys(lead.phones))
        lead.emails = list(dict.fromkeys(lead.emails))
