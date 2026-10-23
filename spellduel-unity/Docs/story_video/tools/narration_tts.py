import json, subprocess, re, os
subs = json.load(open('subs.json'))
VOICE = 'zh-TW-HsiaoChenNeural'
for f, lines in subs.items():
    base = f[:-4]
    for i, line in enumerate(lines):
        text = re.sub(r'^[^：]{1,6}：', '', line).replace('{玩家}', '你')
        out = f'{base}_{i:02d}.mp3'
        if not os.path.exists(out) or os.path.getsize(out) == 0:
            subprocess.run(['edge-tts', '--voice', VOICE, '--rate', '+8%', '--text', text, '--write-media', out], check=True, capture_output=True, timeout=90)
        d = float(subprocess.check_output(['ffprobe','-v','error','-show_entries','format=duration','-of','csv=p=0',out]))
        print(out, f'{d:.2f}', text)
