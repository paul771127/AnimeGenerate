#!/bin/bash
# HomeChat Mac 安裝程式:點兩下執行(第一次如果被擋,請按右鍵 →「打開」)
set -u
SRC="$(cd "$(dirname "$0")" && pwd)"
DEST="$HOME/HomeChat"            # 安裝位置(聊天資料在裡面的 data 資料夾)
PLIST="$HOME/Library/LaunchAgents/tw.homechat.plist"
PORT=8800

green() { printf '\033[32m%s\033[0m\n' "$*"; }
yellow() { printf '\033[33m%s\033[0m\n' "$*"; }
step() { printf '\n\033[36m▶ %s\033[0m\n' "$*"; }
ask() { read -r -p "$1 [Y/n] " a; [[ -z "$a" || "$a" =~ ^[Yy] ]]; }

green "======================================"
green "        HomeChat 安裝程式"
green "======================================"

# ---------------------------------------------------------------- 1. Python
step "檢查 Python"
PY=""
for c in /opt/homebrew/bin/python3 /usr/local/bin/python3 /usr/bin/python3; do
  if [ -x "$c" ] && "$c" -c 'import sys; sys.exit(0 if sys.version_info >= (3, 9) else 1)' >/dev/null 2>&1; then
    PY="$c"; break
  fi
done
if [ -z "$PY" ]; then
  yellow "需要先安裝 Apple 的開發者工具(內含 Python)。等一下會跳出視窗,請按「安裝」。"
  yellow "裝好之後,再點兩下這個安裝程式一次。"
  xcode-select --install >/dev/null 2>&1
  read -r -p "按 Enter 關閉" _
  exit 1
fi
green "Python:$PY"

# ---------------------------------------------------------------- 2. Tailscale
step "檢查 Tailscale(讓你在外面、朋友在任何地方都能連)"
TS=""
for c in /Applications/Tailscale.app/Contents/MacOS/Tailscale "$(command -v tailscale 2>/dev/null)"; do
  [ -n "$c" ] && [ -x "$c" ] && TS="$c" && break
done
if [ -z "$TS" ]; then
  if ask "要安裝 Tailscale 嗎?沒有它只有同一個 Wi-Fi 才能聊天(強烈建議安裝)"; then
    yellow "請在打開的 App Store 頁面安裝 Tailscale,打開它並登入(可用 Google / Apple 帳號)。"
    open "macappstore://apps.apple.com/app/tailscale/id1475387142" 2>/dev/null || open "https://tailscale.com/download/mac"
    read -r -p "裝好並登入後按 Enter 繼續" _
    [ -x /Applications/Tailscale.app/Contents/MacOS/Tailscale ] && TS=/Applications/Tailscale.app/Contents/MacOS/Tailscale
  fi
fi
if [ -n "$TS" ]; then
  if "$TS" status --json 2>/dev/null | grep -q '"BackendState": *"Running"'; then
    green "Tailscale 已登入"
  else
    open -a Tailscale 2>/dev/null
    yellow "請在選單列的 Tailscale 圖示登入(可用 Google / Apple 帳號)。登入後按 Enter 繼續。"
    read -r -p "" _
  fi
else
  yellow "略過 Tailscale:目前只有同一個 Wi-Fi 能連。之後安裝 Tailscale 再執行一次這個程式即可。"
fi

# ---------------------------------------------------------------- 3. 複製程式
step "安裝 HomeChat 到 $DEST"
launchctl unload "$PLIST" >/dev/null 2>&1   # 更新時先停掉舊的
mkdir -p "$DEST"
if [ "$SRC" != "$DEST" ]; then
  cp -f "$SRC/homechat.py" "$DEST/"
  rm -rf "$DEST/static" && cp -R "$SRC/static" "$DEST/"
  cp -f "$SRC/README.md" "$DEST/" 2>/dev/null
fi
mkdir -p "$DEST/data"
green "聊天紀錄會存在 $DEST/data"

# ---------------------------------------------------------------- 4. 開機自動啟動(背景執行)
step "設定開機自動啟動"
mkdir -p "$(dirname "$PLIST")"
cat > "$PLIST" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>Label</key><string>tw.homechat</string>
  <key>ProgramArguments</key>
  <array>
    <string>$PY</string>
    <string>$DEST/homechat.py</string>
  </array>
  <key>WorkingDirectory</key><string>$DEST</string>
  <key>RunAtLoad</key><true/>
  <key>KeepAlive</key><dict><key>SuccessfulExit</key><false/></dict>
  <key>StandardOutPath</key><string>$DEST/data/homechat.log</string>
  <key>StandardErrorPath</key><string>$DEST/data/homechat.log</string>
</dict>
</plist>
EOF
launchctl load -w "$PLIST"

# 桌面捷徑:點兩下打開 HomeChat
cat > "$HOME/Desktop/HomeChat.webloc" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict><key>URL</key><string>http://127.0.0.1:$PORT</string></dict></plist>
EOF
green "桌面已新增「HomeChat」,開機後也會自動在背景執行"

# ---------------------------------------------------------------- 5. 不要睡眠
step "電腦睡眠時大家都連不上"
if ask "要讓電腦「插著電時」不要自動睡眠嗎?(需要輸入電腦密碼;螢幕還是會自動關)"; then
  sudo pmset -c sleep 0 && green "已設定:插電時不會自動睡眠"
fi

# ---------------------------------------------------------------- 6. 打開
step "啟動 HomeChat"
for _ in $(seq 1 20); do
  curl -s -o /dev/null "http://127.0.0.1:$PORT/api/me" && break
  sleep 0.5
done
open "http://127.0.0.1:$PORT"
echo
green "完成!瀏覽器會打開 HomeChat:"
echo "  1. 第一次會請你設定名字和密碼"
echo "  2. 第一次使用 Tailscale 時,瀏覽器會再打開一個頁面,請按「Enable」啟用 Funnel"
echo "  3. 在網頁的「設定」可以看到手機要用的網址"
echo
echo "之後要打開 HomeChat:點桌面的「HomeChat」。"
read -r -p "按 Enter 關閉" _
