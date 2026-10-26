# 旁白與角色配音（v5：送進模型的文字改用簡體＋多音字拼音（emotion_script.json 的 tts_text）、巨像／樹王改用普通話參考音；v4 是繁體原文）：用本機 ComfyUI 的 CosyVoice3（Fun-CosyVoice3-0.5B，FL_CosyVoice3_Instruct2 節點）
# 依 narration/emotion_script.json 逐句生成：每個角色固定一段參考聲音（聲線），每句再用中文「指令」帶入情緒與語氣，
# 之後用 ffmpeg 做角色後製（老人降音加顫音、巨像低沉回聲、樹王多重聲音、澪空靈殘響）與響度一致。
# 用法（ComfyUI 先開著，port 8188）：python narration_tts_cosyvoice.py <narration 資料夾> [已跑完的 prompt_id]
#   27 句放在同一個 ComfyUI prompt 裡（模型只載一次），產出會覆蓋 narration/ 裡同名的 mp3。
#   給第二個參數時不重新合成，只從 ComfyUI 的歷史紀錄把那次的結果拿來後製。
#   環境變數 ONLY=v1_golem_01,v0_prologue_02 只重做這幾句；SPEED_MUL=1.15 把這次的語速再乘上去（太長的句子用）；
#   SEED_ADD=1 換種子重做（同一句唸得不好時）。念錯的字除了標拼音，也可以在 tts_text 換成同音的常用字（例：艾琳→爱琳）。
import json, os, re, subprocess, sys, time, urllib.request, uuid, shutil
from pathlib import Path

COMFY = 'http://127.0.0.1:8188'
COMFY_OUTPUT = Path(r'D:\ComfyUI_windows_portable\ComfyUI\output')
FFMPEG = shutil.which('ffmpeg') or r'C:\ffmpeg\ffmpeg-2024-05-23-git-ece95dc3dc-full_build\bin\ffmpeg.exe'
FFPROBE = shutil.which('ffprobe') or str(Path(FFMPEG).with_name('ffprobe.exe'))

# 角色 → (參考聲音（ComfyUI/input 底下的檔名）, 給 CosyVoice 的聲線描述, 基本語速, ffmpeg 後製)
TRIM = 'silenceremove=start_periods=1:start_threshold=-45dB:start_silence=0.05'
TRIM_END = 'areverse,silenceremove=start_periods=1:start_threshold=-50dB:start_silence=0.15,areverse'
PAUSE = 'silenceremove=stop_periods=-1:stop_duration=0.4:stop_silence=0.4:stop_threshold=-38dB'   # 句中停頓最多留 0.4 秒（鏡頭只有 5 秒）
ROOM = 'aecho=0.8:0.85:28|41:0.18|0.12'
WARM = 'equalizer=f=180:t=q:w=1:g=2,equalizer=f=6500:t=q:w=1.5:g=-3'
CAST = {
    '旁白':     ('zh_cn_female.wav',      '沉稳自然的说书人', 1.0, f'{WARM},{ROOM}'),
    '艾琳':     ('zh_female_warm.wav',    '年轻女性，声音温柔、柔和、亲切', 1.0, f'{WARM},{ROOM}'),
    '老焰':     ('zh_male_calm.wav',      '苍老的老爷爷，声音低沉沙哑、说话慢、气息重、略带颤抖', 0.95,
                 f'asetrate=24000*0.95,aresample=24000,atempo=1.053,vibrato=f=4.5:d=0.025,lowpass=f=7000,{WARM},{ROOM}'),   # v7：降音、顫音減輕（太重會把字糊掉、聽起來念錯）；老人感主要靠指令
    '熔岩巨像': ('zh_cn_male.wav',        '巨大的石头怪物，声音极低、粗、闷', 0.95,   # v5：原本用粤语参考音，普通话会带粤语腔、字念歪
                 'asetrate=24000*0.78,aresample=24000,atempo=1.28,lowpass=f=3000,aecho=0.8:0.6:70|140:0.4|0.25'),
    '腐化樹王': ('zh_male_calm.wav',      '古老的树精，声音低沉、空洞', 0.95,   # v5：原本用英文参考音，中文发音会怪
                 'asetrate=24000*0.86,aresample=24000,atempo=1.16,chorus=0.6:0.8:45|60:0.4|0.3:0.25|0.4:2|1.5,aecho=0.8:0.7:90|180:0.3|0.2'),
    '海斗':     ('zh_cn_male.wav',        '年轻男性，热血、有精神', 1.0, f'{WARM},{ROOM}'),
    '澪':       ('zh_female_bright.wav',  '年轻女性，空灵、温柔、带一点哀伤', 0.95,
                 f'{WARM},aecho=0.7:0.6:60|120|240:0.35|0.22|0.12,highpass=f=120'),
}
# 從語氣指示推語速：寫了「放慢／緩慢／慢」就慢一點點（情緒本身已經會讓它放慢），寫了「加快／急促／喊」就快一點。
# 每個鏡頭只有 5 秒，所以整體不能太慢；多出來的靠混音時最後一格多停一下。
def speed_for(base, direction):
    if re.search(r'放慢|緩慢|慢|停頓|輕聲|低語', direction): base -= 0.03
    if re.search(r'加快|急促|急|喊|大聲|激動', direction): base += 0.08
    return round(min(1.3, max(0.7, base)), 2)

def instruct_for(who, emotion, direction):
    voice = CAST[who][1]
    return f'你是{voice}。请用{emotion}的情绪说这句话，{direction}。'

def api(path, data=None):
    req = urllib.request.Request(COMFY + path, data=None if data is None else json.dumps(data).encode(),
                                 headers={'Content-Type': 'application/json'})
    with urllib.request.urlopen(req, timeout=60) as r:
        body = r.read()
        return json.loads(body) if body else None

def main():
    narr = Path(sys.argv[1] if len(sys.argv) > 1 else '.').resolve()
    script = json.loads((narr / 'emotion_script.json').read_text(encoding='utf-8'))
    run = uuid.uuid4().hex[:6]
    wf = {'M': {'class_type': 'FL_CosyVoice3_ModelLoader',
                'inputs': {'model_version': 'Fun-CosyVoice3-0.5B', 'download_source': 'HuggingFace', 'device': 'auto',
                           'force_redownload': False, 'force_reload': False}}}
    for who, (ref, _, _, _) in CAST.items():
        wf[f'ref_{who}'] = {'class_type': 'LoadAudio', 'inputs': {'audio': ref}}
    only = {s for s in os.environ.get('ONLY', '').split(',') if s}
    speed_mul = float(os.environ.get('SPEED_MUL', '1'))
    seed_add = int(os.environ.get('SEED_ADD', '0'))   # 同一句想換個唸法：SEED_ADD=1 換種子重做
    lines = []
    for video, items in script['videos'].items():
        for i, it in enumerate(items):
            who = it['speaker'] if it['speaker'] in CAST else '旁白'
            stem = it['file'][:-4]
            if only and stem not in only: continue
            speed = round(speed_for(CAST[who][2], it['direction']) * speed_mul, 2)
            # v5：用 tts_text（簡體＋多音字標拼音）和簡體的情緒／語氣；字幕仍用 text
            instruct = instruct_for(who, it.get('tts_emotion', it['emotion']), it.get('tts_direction', it['direction']))
            text = it.get('tts_text', it['text'])
            seed = (int.from_bytes(stem.encode(), 'big') + seed_add) % (2**31 - 1)
            wf[f'tts_{stem}'] = {'class_type': 'FL_CosyVoice3_Instruct2',
                                 'inputs': {'model': ['M', 0], 'text': text, 'instruct_text': instruct,
                                            'reference_audio': [f'ref_{who}', 0], 'speed': speed, 'seed': seed,
                                            'text_frontend': True}}
            wf[f'save_{stem}'] = {'class_type': 'SaveAudio',
                                  'inputs': {'audio': [f'tts_{stem}', 0], 'filename_prefix': f'spellduel_narr/{stem}_{run}'}}
            lines.append((stem, who, text, instruct, speed))
            print(f'{stem}  {who}  speed={speed}  指令：{instruct}', flush=True)
    t0 = time.time()
    if len(sys.argv) > 2:
        pid = sys.argv[2]
        hist = api(f'/history/{pid}')
        if pid not in hist: raise SystemExit(f'prompt {pid} not in ComfyUI history')
    else:
        pid = api('/prompt', {'prompt': wf, 'client_id': 'spellduel'})['prompt_id']
        print(f'submitted {len(lines)} lines, prompt_id={pid}', flush=True)
        while True:
            time.sleep(10)
            hist = api(f'/history/{pid}')
            if pid in hist: break
    entry = hist[pid]
    if entry.get('status', {}).get('status_str') != 'success':
        msgs = [m for m in entry['status'].get('messages', []) if m[0] == 'execution_error']
        raise RuntimeError(f'failed: {json.dumps(msgs, ensure_ascii=False)[:3000]}')
    for stem, who, text, instruct, speed in lines:
        item = entry['outputs'][f'save_{stem}']['audio'][0]
        raw = COMFY_OUTPUT / item.get('subfolder', '') / item['filename']
        out = narr / f'{stem}.mp3'
        fx = CAST[who][3]
        subprocess.run([FFMPEG, '-y', '-v', 'error', '-i', str(raw), '-af',
                        f'{TRIM},{PAUSE},{fx},{TRIM_END},loudnorm=I=-17:TP=-2:LRA=9', '-ar', '44100', '-b:a', '128k', str(out)], check=True)
        d = float(subprocess.check_output([FFPROBE, '-v', 'error', '-show_entries', 'format=duration', '-of', 'csv=p=0', str(out)]))
        print(f'{out.name} {d:.2f}s  {who}  {text}', flush=True)
    new = {s + '.mp3': {'file': s + '.mp3', 'speaker': w, 'reference': CAST[w][0], 'speed': sp, 'instruct': ins, 'prompt_id': pid}
           for s, w, _, ins, sp in lines}
    log_path = narr / os.environ.get('LOG', 'tts_v5_log.json')
    old = json.loads(log_path.read_text(encoding='utf-8')) if (only and log_path.exists()) else {}
    merged = {e['file']: e for e in old.get('lines', [])}
    merged.update(new)   # 只重做幾句時，把它們併進原本的紀錄
    log = {'tool': 'CosyVoice3 Fun-CosyVoice3-0.5B via ComfyUI FL_CosyVoice3_Instruct2',
           'post': 'ffmpeg: 去頭尾空白、句中停頓最多 0.4 秒、角色後製、loudnorm I=-17', 'lines': list(merged.values())}
    log_path.write_text(json.dumps(log, ensure_ascii=False, indent=1), encoding='utf-8')
    print(f'ALL DONE in {time.time() - t0:.0f}s', flush=True)

if __name__ == '__main__':
    main()
