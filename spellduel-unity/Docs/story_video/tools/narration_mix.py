import json, subprocess
V='/home/user/AnimeGenerate/spellduel-unity/Docs/story_video/videos/'
subs=json.load(open('subs.json'))
def dur(p): return float(subprocess.check_output(['ffprobe','-v','error','-show_entries','format=duration','-of','csv=p=0',p]))
for f,lines in subs.items():
    base=f[:-4]; vd=dur(V+f); shot=vd/len(lines)
    args=['ffmpeg','-y','-v','error','-i',V+f]; filt=[]
    for i in range(len(lines)):
        a=f'{base}_{i:02d}.mp3'; args+=['-i',a]
        d=dur(a); slot=shot-0.25; t=max(1.0,d/slot)
        filt.append(f'[{i+1}:a]atempo={t:.3f},adelay={int((i*shot+0.12)*1000)}|{int((i*shot+0.12)*1000)},volume=1.6[a{i}]')
    filt.append(''.join(f'[a{i}]' for i in range(len(lines)))+f'amix=inputs={len(lines)}:normalize=0,apad,atrim=0:{vd:.3f}[out]')
    out=f'{base}_v2.mp4'
    args+=['-filter_complex',';'.join(filt),'-map','0:v','-map','[out]','-c:v','copy','-c:a','aac','-b:a','96k','-ac','2','-ar','44100','-movflags','+faststart',out]
    subprocess.run(args,check=True)
    print(out, f'{dur(out):.2f}', 'shot', f'{shot:.2f}')
