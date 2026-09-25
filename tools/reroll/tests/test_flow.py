"""用假的模擬器(一組合成畫面 + 點擊會切換畫面)測試整個流程引擎。

  cd tools/reroll && python -m pytest tests -q
"""

from pathlib import Path

import numpy as np
import pytest
import yaml

from reroll import vision
from reroll.flow import Game, Runner

W, H = 720, 1280
rng = np.random.default_rng(0)


def patch(w=120, h=60):
    return rng.integers(0, 255, (h, w, 3), dtype=np.uint8)


# 每個按鈕都是一塊獨特的雜訊圖,放在固定位置
BTN = {name: (patch(), pos) for name, pos in {
    "btn_agree": (360, 1000), "btn_skip": (600, 100), "home_screen": (360, 1220),
    "icon_gift": (650, 300), "btn_claim_all": (360, 900), "icon_settings": (60, 100),
    "btn_transfer": (360, 600), "transfer_screen": (360, 200), "card_atem": (200, 500),
}.items()}

DIGIT_W, DIGIT_H = 20, 30
DIGITS = {str(d): patch(DIGIT_W, DIGIT_H) for d in range(10)}
STONE_REGION = [480, 20, 220, 50]


def draw(img, name):
    p, (x, y) = BTN[name]
    h, w = p.shape[:2]
    img[y - h // 2:y - h // 2 + h, x - w // 2:x - w // 2 + w] = p


def screen(*names, stones=None):
    img = np.full((H, W, 3), 40, np.uint8)
    for n in names:
        draw(img, n)
    if stones is not None:
        for i, ch in enumerate(str(stones)):
            x = 500 + i * (DIGIT_W + 4)
            img[30:30 + DIGIT_H, x:x + DIGIT_W] = DIGITS[ch]
    return img


class FakeDevice:
    """狀態機:點到按鈕就切到下一個畫面。"""

    def __init__(self, stones=620, pull_atem=True):
        self.serial = "fake"
        self.stones = stones
        self.pull_atem = pull_atem
        self.state = "off"
        self.taps = []
        self.cleared = 0

    def screenshot(self):
        s = self.state
        if s == "agree":
            return screen("btn_agree")
        if s == "skip":
            return screen("btn_skip")
        if s == "home":
            return screen("home_screen", "icon_gift", "icon_settings", stones=self.stones)
        if s == "gift":
            return screen("btn_claim_all")
        if s == "result":
            return screen("card_atem") if self.pull_atem else screen()
        if s == "settings":
            return screen("btn_transfer")
        if s == "transfer":
            return screen("transfer_screen")
        return screen()

    def _hit(self, name, x, y):
        p, (bx, by) = BTN[name]
        return abs(x - bx) < p.shape[1] // 2 and abs(y - by) < p.shape[0] // 2

    def tap(self, x, y):
        self.taps.append((x, y))
        nxt = {"agree": ("btn_agree", "skip"), "skip": ("btn_skip", "result"),
               "result": (None, "home"), "gift": ("btn_claim_all", "home"),
               "settings": ("btn_transfer", "transfer")}
        if self.state == "home":
            if self._hit("icon_gift", x, y):
                self.state = "gift"
            elif self._hit("icon_settings", x, y):
                self.state = "settings"
            return
        btn, to = nxt.get(self.state, (None, None))
        if to and (btn is None or self._hit(btn, x, y)):
            self.state = to

    def swipe(self, *a):
        pass

    def key(self, code):
        pass

    def text(self, s):
        pass

    def clear_app(self, pkg):
        self.cleared += 1
        self.state = "off"

    def start_app(self, pkg):
        self.state = "agree"

    def stop_app(self, pkg):
        pass

    @staticmethod
    def human_pause(lo=0, hi=0):
        pass


CFG = """
game: test
package: x.y
templates: t
phases:
  - name: tutorial
    timeout: 20
    until: home_screen
    rules:
      - {name: 同意, when: btn_agree, tap: btn_agree}
      - {name: 跳過, when: btn_skip, tap: btn_skip}
      - name: 抽卡結果
        when_not: [btn_agree, btn_skip]
        do: [{scan_cards: true}, {tap_at: [360, 640]}]
  - name: rewards
    timeout: 20
    until_hits: {rule: 領取, count: 1}
    rules:
      - {name: 開禮物, when: [home_screen, icon_gift], tap: icon_gift}
      - {name: 領取, when: btn_claim_all, tap: btn_claim_all}
  - name: count
    timeout: 20
    until_hits: {rule: 讀, count: 1}
    rules:
      - {name: 讀, when: home_screen, read_number: stones}
  - name: transfer
    on_keep: true
    timeout: 20
    until_hits: {rule: 截圖, count: 1}
    rules:
      - {name: 設定, when: [home_screen, icon_settings], tap: icon_settings}
      - {name: 引繼, when: btn_transfer, tap: btn_transfer}
      - {name: 截圖, when: transfer_screen, snap: code}
evaluate:
  numbers:
    stones: {region: [480, 20, 220, 50], digits: digits/stone}
  cards:
    atem: {template: card_atem, value: 200}
  keep_if: "stones >= 600 or card_value >= 300"
  price: "max(0, stones * 1.35 - 290) + card_value"
"""


@pytest.fixture
def game(tmp_path: Path):
    tdir = tmp_path / "templates" / "t"
    for name, (p, _) in BTN.items():
        vision.imwrite(tdir / f"{name}.png", p)
    for d, p in DIGITS.items():
        vision.imwrite(tdir / "digits" / "stone" / f"{d}.png", p)
    return Game(yaml.safe_load(CFG), tmp_path)


def test_templates_complete(game):
    assert game.check_templates() == []


def test_keep_cycle(game, tmp_path):
    dev = FakeDevice(stones=620)
    r = Runner(game, dev, tmp_path / "out").cycle()
    assert r.status == "keep", r
    assert r.numbers == {"stones": 620}
    assert r.cards == {"atem": 1}
    assert r.card_value == 200
    assert r.price == pytest.approx(620 * 1.35 - 290 + 200)
    assert dev.state == "transfer"           # 好號才跑的引繼碼階段有執行
    assert any("code" in p.name for p in Path(r.snap_dir).iterdir())


def test_drop_cycle(game, tmp_path):
    dev = FakeDevice(stones=480, pull_atem=False)
    r = Runner(game, dev, tmp_path / "out").cycle()
    assert r.status == "drop"
    assert r.numbers == {"stones": 480}
    assert dev.state == "home"               # 爛號不會去開引繼碼


def test_timeout_is_fail(game, tmp_path):
    game.cfg["phases"][0]["timeout"] = 1
    dev = FakeDevice()
    dev.start_app = lambda pkg: setattr(dev, "state", "stuck")  # 一直停在沒按鈕的畫面
    r = Runner(game, dev, tmp_path / "out").cycle()
    assert r.status == "fail" and "tutorial" in r.error


def test_read_number_multi_digit():
    img = screen(stones=1405)
    assert vision.read_number(img, STONE_REGION, DIGITS) == 1405


def test_tos_config_loads():
    root = Path(__file__).parents[1]
    g = Game(yaml.safe_load(open(root / "games" / "tos.yaml", encoding="utf-8")), root)
    missing = g.check_templates()
    assert "home_screen" in missing and "digits/stone/0~9.png" in missing
