# 刷號程式(Windows + 雷電模擬器)

一輪流程:清除遊戲資料(= 新遊客帳號)→ 開遊戲 → 依規則過新手教學、領獎勵、(選用)抽卡
→ 讀魔法石數量、辨識抽到的角色 → 符合條件就截下引繼碼畫面 → 下一輪。
每輪結果都記在 `output/<遊戲>/records.csv`,可以算出貨率、每輪時間、預估時薪。

> ⚠️ 用程式自動刷號違反遊戲使用條款,帳號可能被封或被官方收回;賣出後被封,買家可在 8591 要求退款。

## 1. 安裝(只要做一次)

1. **Python 3.10 以上**:<https://www.python.org/downloads/>,安裝時勾「Add python.exe to PATH」。
2. **雷電模擬器 9**:<https://www.ldplayer.tw/>,預設裝在 `C:\LDPlayer\LDPlayer9`。
3. 雷電設定(右側齒輪):
   - 進階設定 → 解析度選 **手機版 720×1280**、DPI **320**
   - 其他設定 → **ADB 調試:開啟本地連接**
4. 在雷電裡從 Play 商店安裝神魔之塔。
5. 下載這個 repo,在 `tools\reroll` 資料夾開命令提示字元:

   ```
   pip install -r requirements.txt
   ```

6. 確認連得到模擬器:

   ```
   C:\LDPlayer\LDPlayer9\adb.exe devices
   ```

   應該看到 `emulator-5554  device`。再確認遊戲的套件名稱,填進 `games\tos.yaml` 的 `package`:

   ```
   C:\LDPlayer\LDPlayer9\adb.exe shell pm list packages | findstr tos
   ```

## 2. 截取模板(第一次最花時間)

程式靠「比對畫面上的小圖」決定下一步,所以要先把會出現的按鈕截下來。
先手動清除遊戲資料(雷電 → 設定 → 應用 → 神魔之塔 → 清除資料),然後**手動玩一遍新手教學**,
每走到一個畫面就截一次:

```
python capture.py tos btn_agree        # 跳出截圖視窗 → 拖曳框住「同意」按鈕 → Enter
python capture.py tos btn_skip
python capture.py tos home_screen      # 主畫面上一塊一定會出現的區域(例如底部選單)
...
```

還缺哪些模板:

```
python run.py games\tos.yaml --check
```

**魔法石數字**:在主畫面把 0~9 每個數字各截一次(數字出現時再截),存到 `digits/stone/`:

```
python capture.py tos digits/stone/5
python capture.py tos --region         # 框選整個魔法石數字區域,把印出的座標填進 yaml 的 region
```

截好後測試比對分數(0.85 以上才會被認到):

```
python capture.py tos --test btn_skip
```

框選要點:框「只有按鈕本身」,不要框到會變動的背景或數字;太小(小於 30×30)容易誤判。

## 3. 調整流程 `games\tos.yaml`

每個階段(phase)是一組「看到 A 就點 B」的規則,**由上到下找第一條符合的規則執行**。
遊戲跳公告、動畫長短不同都不會卡住。常用寫法:

```yaml
- name: 跳過劇情          # 名稱(記錄與 until_hits 用)
  when: btn_skip          # 畫面上有這張圖才執行(可寫清單 = 全部都要有)
  when_not: btn_ok        # 畫面上有這張圖就不執行
  tap: btn_skip           # 點這張圖的位置
- name: 轉珠
  when: tut_hand
  swipe: [120, 900, 600, 900]    # 從 (120,900) 滑到 (600,900)
- name: 多個動作
  when: pull_result
  do: [{scan_cards: true}, {snap: pull}, {tap_at: [360, 1100]}, {wait: [1, 2]}]
```

其他動作:`back: true`(返回鍵)、`text: 名字{rand}`、`read_number: stones`、`snap: 標籤`(存截圖)、`done: true`(結束階段)。

階段結束條件:`until: home_screen`(看到某張圖)或 `until_hits: {rule: 規則名, count: N}`(某規則執行 N 次)。
`stuck_after` 秒內沒有任何規則命中,會執行 `stuck` 動作(預設按返回);超過 `timeout` 這輪算失敗。

留號條件 `keep_if` 與預估賣價 `price` 在檔案最下面,依 8591 行情調整。

## 4. 執行

```
python run.py games\tos.yaml --cycles 3 --debug      # 先跑 3 輪看 log,確認每條規則有觸發
python run.py games\tos.yaml --instances 0            # 單開不停跑(Ctrl+C 停止)
python run.py games\tos.yaml --instances 0 1 2 3      # 4 開(先用雷電多開器建好 4 台並各自設定、裝遊戲)
python run.py games\tos.yaml --stats                  # 出貨率、平均每輪時間、每台每小時預估收入
```

程式會自動用 `ldconsole.exe` 開啟對應編號的模擬器;已手動開好就加 `--no-launch`。

好號的引繼碼截圖在 `output\tos\snaps\<時間>-#<編號>\`,照截圖上架 8591。

## 5. 降低封號風險

- 點擊位置、間隔已加入隨機;不要把 `poll` 調得太快。
- 同一個 IP 短時間大量開新帳號容易被標記,多開數量從 2~3 台開始,觀察幾天再加。
- 先跑 20~30 輪看 `--stats`,確認每小時收入值得再擴大。

## 換別款遊戲

複製 `games\tos.yaml` 改名(例如 `arknights.yaml`),`templates:` 改成新資料夾名稱,
重新截模板、改規則即可,程式本身不用動。

## 測試

```
python -m pytest tests -q     # 用模擬畫面測試流程引擎,不需要模擬器
```
