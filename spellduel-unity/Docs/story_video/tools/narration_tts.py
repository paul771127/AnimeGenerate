# 旁白與角色配音（v3）：每個角色各自的聲音，再加一點後製（房間殘響、EQ、響度一致），比較不像機器念稿。
# 在 narration/ 底下執行：python3 ../tools/narration_tts.py
import json, subprocess, re, os
subs = json.load(open('subs.json'))
# 角色 → (edge-tts 聲音, 語速, 音高（edge-tts 低於約 −15Hz 會沒有聲音，再低用 asetrate 降）, ffmpeg 後製)
TRIM = 'silenceremove=start_periods=1:start_threshold=-45dB:start_silence=0.05'   # 去掉開頭的空白
TRIM_END = 'areverse,silenceremove=start_periods=1:start_threshold=-50dB:start_silence=0.15,areverse'   # 去掉結尾的空白（保留一點殘響）
ROOM = 'aecho=0.8:0.85:28|41:0.18|0.12'            # 很短的房間殘響（讓聲音不像貼在耳邊的合成音）
WARM = 'equalizer=f=180:t=q:w=1:g=2,equalizer=f=6500:t=q:w=1.5:g=-3'
CAST = {
    '旁白':     ('zh-TW-HsiaoChenNeural', '-2%',  '-2Hz',  f'{WARM},{ROOM}'),
    '老焰':     ('zh-TW-YunJheNeural',    '-20%', '-15Hz', f'asetrate=24000*0.93,aresample=24000,atempo=1.04,vibrato=f=4.5:d=0.05,lowpass=f=6500,{WARM},{ROOM}'),   # 老人：低沉、慢、聲音微微顫
    '艾琳':     ('zh-TW-HsiaoYuNeural',   '+6%',  '+3Hz',  f'{WARM},{ROOM}'),
    '海斗':     ('zh-CN-YunxiNeural',     '+0%',  '-4Hz',  f'{WARM},{ROOM}'),
    '澪':       ('zh-CN-XiaoxiaoNeural',  '-10%', '+0Hz',  f'{WARM},{ROOM}'),
    '熔岩巨像': ('zh-CN-YunjianNeural',   '-22%', '-15Hz', 'asetrate=24000*0.8,aresample=24000,atempo=1.12,lowpass=f=3200,aecho=0.8:0.6:70|140:0.4|0.25'),   # 石頭巨人：很低、悶、回聲
    '腐化樹王': ('zh-CN-YunyangNeural',   '-20%', '-15Hz', 'asetrate=24000*0.86,aresample=24000,atempo=1.08,chorus=0.6:0.8:45|60:0.4|0.3:0.25|0.4:2|1.5,aecho=0.8:0.7:90|180:0.3|0.2'),   # 古樹：低沉、多重聲音
}
for f, lines in subs.items():
    base = f.replace('_v2', '')[:-4]
    for i, line in enumerate(lines):
        m = re.match(r'^([^：]{1,6})：', line)
        who = m.group(1) if m else '旁白'
        voice, rate, pitch, fx = CAST.get(who, CAST['旁白'])
        text = line[m.end():] if m else line
        text = text.replace('{玩家}', '你')
        raw = f'{base}_{i:02d}_raw.mp3'; out = f'{base}_{i:02d}.mp3'
        for attempt in range(4):   # 線上服務偶爾沒回音訊：重試
            r = subprocess.run(['edge-tts', '--voice', voice, f'--rate={rate}', f'--pitch={pitch}', '--text', text, '--write-media', raw], capture_output=True, timeout=90)
            if r.returncode == 0 and os.path.getsize(raw) > 0: break
        else: raise RuntimeError(f'edge-tts 失敗：{who} {text}')
        subprocess.run(['ffmpeg', '-y', '-v', 'error', '-i', raw, '-af', f'{TRIM},{fx},{TRIM_END},loudnorm=I=-17:TP=-2:LRA=9', '-ar', '44100', '-b:a', '128k', out], check=True)
        os.remove(raw)
        d = float(subprocess.check_output(['ffprobe', '-v', 'error', '-show_entries', 'format=duration', '-of', 'csv=p=0', out]))
        print(out, f'{d:.2f}', who, text)
