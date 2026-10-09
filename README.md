# HomeChat 家用聊天室

架在**自己家中電腦**的聊天室。對話紀錄全部存在你的電腦,不經過 LINE 或任何雲端。

- 只要 Python 3.10+,**不用裝任何套件**
- 你(主人)用密碼登入,電腦、手機瀏覽器都能開
- 每個聯絡人一條專屬**邀請連結**,用 LINE 傳給對方,對方用手機瀏覽器打開就能跟你聊,不用裝 App
- 匯入聯絡人名單,也能匯入 **LINE 匯出的聊天紀錄**,把舊對話一起搬過來
- 即時收訊息、已讀、未讀數、桌面通知、匯出 .txt

## 啟動

```bash
git clone -b claude/homechat-self-hosted-chat https://github.com/paul771127/animegenerate.git homechat
cd homechat
python homechat.py --name 你的名字
```

第一次執行會要你設定主人密碼。啟動後畫面會顯示網址:

```
HomeChat 已啟動,資料存在 ~/homechat/data/homechat.db
  這台電腦:   http://127.0.0.1:8800
  同 Wi-Fi 手機: http://192.168.1.23:8800
```

- 電腦:開 `http://127.0.0.1:8800`
- 手機(跟電腦連同一個 Wi-Fi):開上面第二個網址。在手機瀏覽器選「加入主畫面」就像 App 一樣
- Windows 第一次執行時防火牆會問要不要允許,請選「允許私人網路」

| 參數 | 預設 | 說明 |
|---|---|---|
| `--port` | 8800 | 埠號 |
| `--name` | 主人 | 你的顯示名稱(也可以在網頁「設定」改) |
| `--db` | `data/homechat.db` | 資料庫位置 |
| `--public-url` | | 對外網址,邀請連結會用這個(也可以在網頁「設定」填) |
| `--set-password` | | 改密碼(會登出所有裝置) |

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
2. 對方在 LINE 點連結就會打開聊天室,之後再開同一條連結就會回到同一個對話
3. **拿到連結的人就能以對方的身分聊天**,請只私訊給本人。外流了就按「換新連結」,舊連結和已打開的手機會立刻失效

## 讓外面連進來

只在家裡 Wi-Fi 用,上面就夠了。要**在外面用手機連**,或**讓朋友連進來**,需要一個對外網址。
不建議直接在路由器開 port(沒有 HTTPS,家裡 IP 會曝光),建議用下面其中一種:

**A. Cloudflare Tunnel(朋友也要連時推薦,免費、自動 HTTPS)**

```bash
# 安裝 cloudflared: https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/downloads/
cloudflared tunnel --url http://localhost:8800
```

會給你一個 `https://xxxx.trycloudflare.com` 網址,到 HomeChat「設定」填進「對外網址」,之後產生的邀請連結就會用它。

> 快速通道的網址**每次重開都會變**,舊的邀請連結會連不到。長期使用請在 Cloudflare 綁自己的網域建立固定通道
> (`cloudflared tunnel create homechat`,見 Cloudflare 文件),網址就不會變。

**B. Tailscale(只有你自己的手機要連時最簡單)**

電腦和手機都裝 [Tailscale](https://tailscale.com/) 並登入同一個帳號,手機就能用 `http://電腦的Tailscale IP:8800` 從任何地方連回家。朋友沒有裝就連不到,所以不適合給朋友用。

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
- 讓外面連進來時請一定用 HTTPS(Cloudflare Tunnel 會自動提供),並設一個夠長的密碼

## 目前沒有

傳圖片 / 檔案、群組聊天、手機關掉瀏覽器時的推播通知(瀏覽器開著時有通知)。
