# HomeChat 家用聊天室

架在**自己家中電腦**的聊天室。對話紀錄全部存在你的電腦,不經過 LINE 或任何雲端。

- 只要 Python 3.10+,**不用裝任何套件**
- 你(主人)用密碼登入,電腦、手機瀏覽器都能開
- 每個聯絡人一條專屬**邀請連結**,用 LINE 傳給對方,對方用手機瀏覽器打開就能跟你聊,不用裝 App
- 匯入聯絡人名單,也能匯入 **LINE 匯出的聊天紀錄**,把舊對話一起搬過來
- 即時收訊息、已讀、未讀數、桌面通知、匯出 .txt

## 安裝(只要做一次)

### 1. Python

- **Windows**:到 <https://www.python.org/downloads/> 下載安裝,**第一個畫面一定要勾「Add python.exe to PATH」**
- **Mac**:打開「終端機」輸入 `python3 --version`,跳出安裝開發者工具就按「安裝」

### 2. Tailscale(讓你在外面、朋友在任何地方都能連)

HomeChat 用 Tailscale 的 **Funnel** 功能,幫家裡電腦開一個**固定不變**的 https 網址,
例如 `https://home-pc.tail1234.ts.net`。免費、不用買網域、不用設定路由器。
**只有家裡這台電腦要裝**,你的手機和朋友都不用裝,直接用瀏覽器開網址就好。

1. 到 <https://tailscale.com/download> 下載安裝到**家裡電腦**
2. 打開 Tailscale,用 Google / Microsoft / Apple 帳號登入
3. 第一次啟動 HomeChat 時,畫面上會出現一個 `https://login.tailscale.com/...` 的網址,
   用瀏覽器打開,按「啟用」(Enable HTTPS / Funnel)就完成了,之後不用再做

> Linux 要先執行一次 `sudo tailscale set --operator=$USER`,HomeChat 才有權限開 Funnel。

### 3. 下載 HomeChat

登入 GitHub 後下載 zip 解壓縮:
<https://github.com/paul771127/animegenerate/archive/refs/heads/claude/homechat-self-hosted-chat.zip>

或用 git:`git clone -b claude/homechat-self-hosted-chat https://github.com/paul771127/animegenerate.git homechat`

## 啟動

- **Windows**:打開有 `homechat.py` 的資料夾,在上方網址列輸入 `cmd` 按 Enter,然後輸入
  `python homechat.py --name 你的名字`
- **Mac**:終端機輸入 `cd `(後面有空格)再把資料夾拖進來按 Enter,然後輸入
  `python3 homechat.py --name 你的名字`

第一次執行會要你設定主人密碼(打字時畫面不會顯示,正常)。接著會自動開外部連線,看到這樣就成功了:

```
============================================================
  在外面(4G/5G、別的 Wi-Fi、朋友):https://home-pc.tail1234.ts.net
  這台電腦:http://127.0.0.1:8800
  同 Wi-Fi:http://192.168.1.23:8800
============================================================
這個視窗不要關。按 Ctrl+C 結束
```

- **手機**:開「在外面」那個網址並登入,**不管在家還是在外面都用這個網址**。
  在瀏覽器選「加入主畫面」就像 App 一樣
- **電腦**:開 `http://127.0.0.1:8800`
- Windows 第一次執行時防火牆會問要不要允許,請選「允許」
- **視窗不能關、電腦不能關機或睡眠**,不然大家都連不上(見下面「開機自動執行」)

| 參數 | 預設 | 說明 |
|---|---|---|
| `--name` | 主人 | 你的顯示名稱(也可以在網頁「設定」改) |
| `--tunnel` | `auto` | 外部連線:`auto` 有 Tailscale 用 Tailscale,沒有就用 cloudflared;`tailscale`、`cloudflare`、`off`(只在家裡 Wi-Fi 用) |
| `--public-url` | | 你自己有固定網域時填這個,就不會自動開通道 |
| `--port` | 8800 | 埠號 |
| `--db` | `data/homechat.db` | 資料庫位置 |
| `--set-password` | | 改密碼(會登出所有裝置) |

### 不想裝 Tailscale:Cloudflare 臨時網址

裝 cloudflared(Windows:cmd 輸入 `winget install --id Cloudflare.cloudflared`;Mac:`brew install cloudflared`)
後啟動 HomeChat,也會自動拿到一個 `https://xxxx.trycloudflare.com` 網址,不用註冊帳號。
缺點是**每次重開 HomeChat 網址都會變**,朋友手上的舊邀請連結會失效、要重傳,所以比較適合先試試看。

## 匯入聯絡人

LINE **沒有**提供匯出好友名單的功能,也不開放個人帳號的 API,所以有兩種做法:

1. **貼上名單**:右上角 ⋯ →「貼上聯絡人名單」,一行一個人。可以只寫名字,或 `名字,LINE ID,備註`
   (從 Excel / Google 試算表直接複製貼上也可以)
2. **匯入 LINE 聊天紀錄**(推薦):會自動建立聯絡人,並把舊對話搬過來
   - 手機 LINE 打開跟某人的聊天室 → 右上角 ≡ → 設定 → **傳送聊天記錄** → 存成 .txt
   - 把 .txt 傳到電腦,在 HomeChat 選 ⋯ →「匯入 LINE 聊天紀錄」,可以一次選多個檔案
   - 同一份重複匯入不會重複;群組或看不出誰是對方時,會請你選

## 測試

```bash
pip install pytest
python -m pytest tests
```

## 跟朋友聊天

1. 點開聯絡人 → 右上角 ⋯ →「邀請連結」→「用 LINE 傳送」(或複製後自己貼)
2. 對方在 LINE 點連結就會打開聊天室(對方不用裝任何東西,在哪裡都能連),之後再開同一條連結就會回到同一個對話
3. **拿到連結的人就能以對方的身分聊天**,請只私訊給本人。外流了就按「換新連結」,舊連結和已打開的手機會立刻失效

## 開機自動執行

- **Windows**:建一個 `homechat.bat`,內容 `cd /d C:\路徑\homechat && python homechat.py`,放進「啟動」資料夾(Win+R 輸入 `shell:startup`)
- **macOS / Linux**:用 `launchd` / `systemd`,或 `crontab -e` 加一行 `@reboot cd /路徑/homechat && python3 homechat.py`

電腦關機或睡眠時大家都連不上,訊息也收不到;要一直能聊,電腦要保持開著(可在電源設定關閉自動睡眠)。

## 備份

所有資料都在 `data/homechat.db` 一個檔案(旁邊的 `-wal`、`-shm` 也一起)。
關掉 HomeChat 後複製整個 `data` 資料夾就是完整備份。單一對話也可以從 ⋯ →「匯出聊天紀錄 .txt」。

## 安全

- 主人密碼用 PBKDF2 雜湊儲存;同一來源 10 分鐘內錯 10 次會暫時鎖住
- 訪客只能看到、發送自己那一個對話,看不到聯絡人列表或其他人的訊息
- 外部連線一律走 HTTPS(Tailscale / Cloudflare 自動提供),不用在路由器開 port,家裡 IP 不會曝光
- 網址是公開的,任何人都能看到登入頁,所以請設一個夠長的密碼

## 目前沒有

傳圖片 / 檔案、群組聊天、手機關掉瀏覽器時的推播通知(瀏覽器開著時有通知)。
