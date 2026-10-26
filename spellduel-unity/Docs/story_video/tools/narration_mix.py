# 把每句配音接到影片上（v6 起）：畫面配合聲音——每個鏡頭至少放到「這一句講完 +0.35 秒」，
# 不夠長就先把這個鏡頭放慢（最多 1.3 倍），還不夠再停在最後一格。所以每一句都在自己鏡頭的開頭開始講，畫面和聲音對得上。
# 畫面來源是 *_v2.mp4（鏡頭交界實測在第 floor(i×124.333) 幀：124／124／125 幀輪流，約 5.18 秒），輸出 *_<版本>.mp4，並寫出 narration/timing.json（每句的開始／結束秒數，
# 遊戲字幕用，貼到 StoryVideo.cs 的 LineTimes）。
# 結尾：最後一句講完後定格約 2 秒，慢慢推近並淡出（自然收尾，畫面一定比聲音長）。
# 在 narration/ 底下執行：python ../tools/narration_mix.py [版本，預設 v8]
import json, subprocess, sys, shutil
from pathlib import Path
OUT = sys.argv[1] if len(sys.argv) > 1 else 'v8'
V = str(Path(__file__).resolve().parent.parent / 'videos') + '/'
FFMPEG = shutil.which('ffmpeg') or 'ffmpeg'; FFPROBE = shutil.which('ffprobe') or 'ffprobe'
def shot_at(i): return int(i * 124.3334) / 24.0   # 第 i 個鏡頭在 v2 裡開始的秒數（用場景切換偵測量出來的）
LEAD, TAIL = 0.12, 0.35   # 鏡頭開始後多久開口；講完後畫面再留多久
MAX_SLOW = 1.3        # 鏡頭最多放慢幾倍（再多就停格）
END_HOLD, END_ZOOM = 2.2, 0.06   # 結尾：最後一句講完後，最後一格定格 2.2 秒，慢慢推近 6% 並淡出成黑畫面（字幕在遊戲裡照常顯示）
subs = json.load(open('subs.json', encoding='utf-8'))
def dur(p): return float(subprocess.check_output([FFPROBE, '-v', 'error', '-show_entries', 'format=duration', '-of', 'csv=p=0', p]))
timing = {}
for f, lines in subs.items():
    base = f.replace('_v2', '')[:-4]; src = V + base + '_v2.mp4'
    n = len(lines); vlen = dur(src)
    W, H = map(int, subprocess.check_output([FFPROBE, '-v', 'error', '-select_streams', 'v', '-show_entries', 'stream=width,height', '-of', 'csv=p=0', src]).decode().strip().split(','))
    ds = [dur(f'{base}_{i:02d}.mp3') for i in range(n)]
    args = [FFMPEG, '-y', '-v', 'error', '-i', src]; filt = []; t0 = 0.0; seg = []
    for i, d in enumerate(ds):
        a, b = shot_at(i), (shot_at(i + 1) if i + 1 < n else vlen)
        SHOT = b - a
        need = max(SHOT, LEAD + d + TAIL)
        slow = min(MAX_SLOW, need / SHOT); hold = max(0.0, need - SHOT * slow)
        filt.append(f'[0:v]trim=start={a + 0.01:.3f}:end={b - 0.01:.3f},setpts=(PTS-STARTPTS)*{slow:.4f}'
                    + (f',tpad=stop_mode=clone:stop_duration={hold:.3f}' if hold > 0 else '') + f',fps=24[v{i}]')
        args += ['-i', f'{base}_{i:02d}.mp3']
        ms = int((t0 + LEAD) * 1000)
        filt.append(f'[{i + 1}:a]adelay={ms}|{ms},volume=1.5[a{i}]')
        seg.append([round(t0 + LEAD, 2), round(t0 + LEAD + d, 2)])
        t0 += need
    t_end = t0; t0 += END_HOLD   # 定格收尾：畫面一定比聲音長，播放器不會在台詞講完前結束
    zoom = f"if(gte(in_time,{t_end:.3f}),1+{END_ZOOM}*min(1,(in_time-{t_end:.3f})/{END_HOLD}),1)"
    filt.append(''.join(f'[v{i}]' for i in range(n)) + f'concat=n={n}:v=1:a=0,tpad=stop_mode=clone:stop_duration={END_HOLD + 0.5},'
                f"zoompan=z='{zoom}':x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)':d=1:s={W}x{H}:fps=24,"
                f'fade=t=out:st={t0 - 1.2:.3f}:d=1.2[v]')
    filt.append(''.join(f'[a{i}]' for i in range(n)) + f'amix=inputs={n}:normalize=0,apad,atrim=0:{t0:.3f},afade=t=out:st={t0 - 0.6:.3f}:d=0.6[out]')
    out = V + f'{base}_{OUT}.mp4'
    args += ['-filter_complex', ';'.join(filt), '-map', '[v]', '-map', '[out]', '-t', f'{t0:.3f}',
             '-c:v', 'libx264', '-preset', 'medium', '-crf', '21', '-pix_fmt', 'yuv420p',
             '-c:a', 'aac', '-b:a', '128k', '-ac', '2', '-ar', '44100', '-movflags', '+faststart', out]
    subprocess.run(args, check=True)
    timing[f'{base}_{OUT}.mp4'] = seg
    print(out.split('/')[-1], f'{vlen:.1f}s→{t0:.1f}s', '開始 ' + ' '.join(f'{s[0]:.1f}' for s in seg))
json.dump(timing, open('timing.json', 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
print('timing.json 已更新：把數字貼到 StoryVideo.cs 的 LineTimes（檔名也要改成新版本）')
