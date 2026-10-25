# 把每句配音接到影片上：每句從自己鏡頭的開頭開始；上一句還沒講完就接在後面（不硬切、盡量不加速），
# 整支講不完才整體稍微加快（最多 1.06 倍）。畫面直接用 *_v2.mp4 的影像（不重新編碼），輸出 *_<版本>.mp4。
# 在 narration/ 底下執行：python ../tools/narration_mix.py [版本，預設 v4]   （v3 的配音也是用這支混的）
import json, subprocess, sys, shutil
from pathlib import Path
OUT = sys.argv[1] if len(sys.argv) > 1 else 'v4'
V = str(Path(__file__).resolve().parent.parent / 'videos') + '/'
FFMPEG = shutil.which('ffmpeg') or 'ffmpeg'; FFPROBE = shutil.which('ffprobe') or 'ffprobe'
subs = json.load(open('subs.json', encoding='utf-8'))
def dur(p): return float(subprocess.check_output([FFPROBE, '-v', 'error', '-show_entries', 'format=duration', '-of', 'csv=p=0', p]))
GAP = 0.2
for f, lines in subs.items():
    base = f.replace('_v2', '')[:-4]; src = V + base + '_v2.mp4'
    vd = dur(src); shot = vd / len(lines)
    ds = [dur(f'{base}_{i:02d}.mp3') for i in range(len(lines))]
    def plan(t):
        st, end = [], 0.0
        for i, d in enumerate(ds):
            s = max(i * shot + 0.12, end + GAP if i else 0.12); st.append(s); end = s + d / t
        return st, end
    t = 1.0
    while plan(t)[1] > vd - 0.1 and t < 1.06: t += 0.01   # 只稍微加快（太快會更像機器），其餘靠最後一格停久一點
    st, end = plan(t)
    pad = max(0.0, end + 0.6 - vd)   # 最後一句講不完：最後一格畫面停久一點（要重新編碼影像）
    total = vd + pad
    args = [FFMPEG, '-y', '-v', 'error', '-i', src]; filt = []
    for i in range(len(lines)):
        args += ['-i', f'{base}_{i:02d}.mp3']
        ms = int(st[i] * 1000)
        filt.append(f'[{i+1}:a]atempo={t:.3f},adelay={ms}|{ms},volume=1.5[a{i}]')
    filt.append(''.join(f'[a{i}]' for i in range(len(lines))) + f'amix=inputs={len(lines)}:normalize=0,apad,atrim=0:{total:.3f},afade=t=out:st={total-0.4:.3f}:d=0.4[out]')
    if pad > 0: filt.append(f'[0:v]tpad=stop_mode=clone:stop_duration={pad:.3f}[v]')
    out = V + f'{base}_{OUT}.mp4'
    args += ['-filter_complex', ';'.join(filt), '-map', '[v]' if pad > 0 else '0:v', '-map', '[out]'] + (['-c:v', 'libx264', '-preset', 'medium', '-crf', '21', '-pix_fmt', 'yuv420p'] if pad > 0 else ['-c:v', 'copy']) + [ '-c:a', 'aac', '-b:a', '128k', '-ac', '2', '-ar', '44100', '-movflags', '+faststart', out]
    subprocess.run(args, check=True)
    print(out.split('/')[-1], f'影片 {vd:.1f}s→{total:.1f}s', f'語速 ×{t:.2f}', f'講完 {end:.1f}s', '開始 ' + ' '.join(f'{x:.1f}' for x in st))
