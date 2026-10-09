# HomeChat Windows 安裝程式:安裝 Python / Tailscale、複製程式、建立捷徑與開機自動啟動
$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$Src  = Split-Path -Parent $PSScriptRoot          # 下載解壓縮的資料夾
$Dest = Join-Path $env:USERPROFILE "HomeChat"     # 安裝位置(聊天資料在裡面的 data 資料夾)
$Port = 8800

function Say($text, $color = "White") { Write-Host $text -ForegroundColor $color }
function Step($text) { Write-Host ""; Write-Host "▶ $text" -ForegroundColor Cyan }
function Ask($question) {
    $ans = Read-Host "$question [Y/n]"
    return ($ans -eq "" -or $ans -match "^[Yy]")
}
function Has-Winget { return [bool](Get-Command winget -ErrorAction SilentlyContinue) }
function Refresh-Path {
    $env:Path = [Environment]::GetEnvironmentVariable("Path", "Machine") + ";" +
                [Environment]::GetEnvironmentVariable("Path", "User")
}

function Find-Python {
    # 1. py 啟動器 / python 指令(排除 Microsoft Store 的假 python)
    foreach ($cmd in @("py", "python")) {
        $c = Get-Command $cmd -ErrorAction SilentlyContinue
        if (-not $c -or $c.Source -like "*WindowsApps*") { continue }
        $argList = @()
        if ($cmd -eq "py") { $argList += "-3" }
        $argList += @("-c", "import sys; print(sys.executable) if sys.version_info >= (3, 9) else sys.exit(1)")
        try {
            $exe = & $c.Source @argList 2>$null
            if ($LASTEXITCODE -eq 0 -and $exe) { return ($exe | Select-Object -First 1).Trim() }
        } catch {}
    }
    # 2. 常見安裝位置
    $cands = @(Get-ChildItem -Path "$env:LOCALAPPDATA\Programs\Python\Python3*\python.exe",
                                   "$env:ProgramFiles\Python3*\python.exe" -ErrorAction SilentlyContinue |
               Sort-Object FullName -Descending)
    if ($cands.Count -gt 0) { return $cands[0].FullName }
    return $null
}

Say "======================================" Green
Say "        HomeChat 安裝程式" Green
Say "======================================" Green

# ---------------------------------------------------------------- 1. Python
Step "檢查 Python"
$py = Find-Python
if (-not $py) {
    if (Has-Winget) {
        Say "沒有找到 Python,正在自動安裝(約 1~3 分鐘)…"
        winget install -e --id Python.Python.3.12 --scope user --silent --accept-package-agreements --accept-source-agreements | Out-Host
        Refresh-Path
        $py = Find-Python
    }
    if (-not $py) {
        Say "沒辦法自動安裝 Python。請到打開的網頁下載安裝," Yellow
        Say "安裝時第一個畫面要勾「Add python.exe to PATH」,裝好後再執行一次這個安裝程式。" Yellow
        Start-Process "https://www.python.org/downloads/"
        exit 1
    }
}
$pyw = Join-Path (Split-Path $py) "pythonw.exe"
if (-not (Test-Path $pyw)) { $pyw = $py }
Say "Python:$py" Green

# ---------------------------------------------------------------- 2. Tailscale
Step "檢查 Tailscale(讓你在外面、朋友在任何地方都能連)"
$ts = Join-Path $env:ProgramFiles "Tailscale\tailscale.exe"
if (-not (Test-Path $ts)) {
    if (Ask "要安裝 Tailscale 嗎?沒有它只有同一個 Wi-Fi 才能聊天(強烈建議安裝)") {
        if (Has-Winget) {
            Say "正在安裝 Tailscale…(如果跳出「是否允許變更」請按「是」)"
            winget install -e --id Tailscale.Tailscale --silent --accept-package-agreements --accept-source-agreements | Out-Host
        }
        if (-not (Test-Path $ts)) {
            Say "請在打開的網頁下載安裝 Tailscale,裝好後按 Enter 繼續。" Yellow
            Start-Process "https://tailscale.com/download/windows"
            Read-Host | Out-Null
        }
    }
}
if (Test-Path $ts) {
    $state = ""
    try { $state = (& $ts status --json 2>$null | Out-String | ConvertFrom-Json).BackendState } catch {}
    if ($state -ne "Running") {
        Say "需要登入 Tailscale(可以用 Google / Microsoft 帳號)。等一下會打開瀏覽器,請登入並按「Connect」。" Yellow
        $out = Join-Path $env:TEMP "homechat-ts-login.txt"
        $err = Join-Path $env:TEMP "homechat-ts-login-err.txt"
        $proc = Start-Process -FilePath $ts -ArgumentList "login", "--timeout=600s" -NoNewWindow -PassThru `
                -RedirectStandardOutput $out -RedirectStandardError $err
        $opened = $false
        for ($i = 0; $i -lt 600 -and -not $proc.HasExited; $i++) {
            Start-Sleep -Seconds 1
            $text = (Get-Content $out, $err -Raw -ErrorAction SilentlyContinue) -join "`n"
            if (-not $opened -and $text -match "(https://login\.tailscale\.com/\S+)") {
                Start-Process $Matches[1]
                $opened = $true
            }
        }
        try { $state = (& $ts status --json 2>$null | Out-String | ConvertFrom-Json).BackendState } catch {}
    }
    if ($state -eq "Running") { Say "Tailscale 已登入" Green }
    else { Say "Tailscale 還沒登入,先用同一個 Wi-Fi。之後登入 Tailscale 再重新執行這個安裝程式即可。" Yellow }
} else {
    Say "略過 Tailscale:目前只有同一個 Wi-Fi 能連。" Yellow
}

# ---------------------------------------------------------------- 3. 複製程式
Step "安裝 HomeChat 到 $Dest"
# 如果已經在執行(更新的情況),先關掉
Get-CimInstance Win32_Process -Filter "Name like 'python%'" -ErrorAction SilentlyContinue |
    Where-Object { $_.CommandLine -like "*HomeChat\homechat.py*" } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
Start-Sleep -Seconds 1
New-Item -ItemType Directory -Force -Path $Dest | Out-Null
if ((Resolve-Path $Src).Path -ne (Resolve-Path $Dest).Path) {
    Copy-Item -Force (Join-Path $Src "homechat.py") $Dest
    Copy-Item -Force -Recurse (Join-Path $Src "static") $Dest
    Copy-Item -Force (Join-Path $Src "README.md") $Dest -ErrorAction SilentlyContinue
}
Say "聊天紀錄會存在 $Dest\data" Green

# ---------------------------------------------------------------- 4. 捷徑 + 開機自動啟動
Step "建立桌面捷徑和開機自動啟動"
$shell = New-Object -ComObject WScript.Shell
function Make-Shortcut($path, $arguments) {
    $s = $shell.CreateShortcut($path)
    $s.TargetPath = $pyw
    $s.Arguments = "`"$Dest\homechat.py`" $arguments"
    $s.WorkingDirectory = $Dest
    $s.IconLocation = "$env:SystemRoot\System32\shell32.dll,160"
    $s.Description = "HomeChat 家用聊天室"
    $s.Save()
}
$desktop = [Environment]::GetFolderPath("Desktop")
$startup = [Environment]::GetFolderPath("Startup")
Make-Shortcut (Join-Path $desktop "HomeChat.lnk") "--open"
Make-Shortcut (Join-Path $startup "HomeChat.lnk") ""
Say "桌面已新增「HomeChat」,開機後也會自動在背景執行" Green

# ---------------------------------------------------------------- 5. 不要睡眠
Step "電腦睡眠時大家都連不上"
if (Ask "要讓電腦「插著電時」不要自動睡眠嗎?(螢幕還是會自動關)") {
    powercfg /change standby-timeout-ac 0 | Out-Null
    Say "已設定:插電時不會自動睡眠" Green
}

# ---------------------------------------------------------------- 6. 啟動
Step "啟動 HomeChat"
Start-Process -FilePath $pyw -ArgumentList "`"$Dest\homechat.py`" --open" -WorkingDirectory $Dest
Say ""
Say "完成!瀏覽器會打開 HomeChat:" Green
Say "  1. 第一次會請你設定名字和密碼"
Say "  2. 如果跳出 Windows 防火牆詢問,請按「允許」"
Say "  3. 第一次使用 Tailscale 時,瀏覽器會再打開一個頁面,請按「Enable」啟用 Funnel"
Say "  4. 在網頁的「設定」可以看到手機要用的網址"
Say ""
Say "之後要打開 HomeChat:點桌面的「HomeChat」。"
