"""Pack image-generated drawings into SpriteEnemyView's unchanged 168-slot contract.

Only crops, uniform resampling, placement and QA composites; no painted artwork
is synthesized here. Source PNGs and prompts are retained for reproducibility.
"""
from pathlib import Path
import hashlib, json, statistics, io, subprocess
from PIL import Image, ImageDraw
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
SOURCE = Path(__file__).parent / 'painted_sources'
OUT = ROOT / 'Assets/SpellDuel/Resources/SpriteEnemies'
PREVIEW = ROOT / 'Docs/castle_2d_preview/enemy_redesign'
IDS = ['skeleton','knight','archer','ghost','bat','pumpkin','gargoyle','mage']
GROUPS = [('idle',4,0),('walk',6,4),('combat',5,10),('reaction',6,15)]

def bounds(im):
    # Ignore sub-visible alpha only for measuring; preserve the source alpha.
    return im.getchannel('A').point(lambda a: 255 if a >= 16 else 0).getbbox()

def split(im, cols, rows):
    # Generated sheets have regular ordering but not pixel-perfect grid seams.
    # Find transparent gutters near each nominal seam before cropping so a
    # neighbour's mace/hat is never pulled into the next animation cell.
    def seams(values,n):
        size=len(values);step=size/n;result=[0]
        zero=np.flatnonzero(values==0)
        runs=np.split(zero,np.flatnonzero(np.diff(zero)!=1)+1)
        gaps=[r for r in runs if len(r)>1 and r[0]>0 and r[-1]<size-1]
        if len(gaps)==n-1:
            return [0]+[int((r[0]+r[-1])//2) for r in gaps]+[size]
        for k in range(1,n):
            lo=round((k-.32)*step);hi=round((k+.32)*step)
            v=values[lo:hi];candidates=np.flatnonzero(v==v.min())+lo
            result.append(int(candidates[np.argmin(abs(candidates-k*step))]))
        return result+[size]
    alpha=np.array(im.getchannel('A'))>=16
    ys=seams(alpha.sum(axis=1),rows);cells=[]
    for r in range(rows):
        xs=seams(alpha[ys[r]:ys[r+1]].sum(axis=0),cols)
        for c in range(cols):cells.append(im.crop((xs[c],ys[r],xs[c+1],ys[r+1])))
    return cells

def source_cells(name, group, rows):
    path = SOURCE/name/(group+'.png')
    im = Image.open(path).convert('RGBA')
    if name == 'archer' and group == 'idle':
        seven = split(im,7,4)
        extra = split(Image.open(SOURCE/name/'idle_back_left.png').convert('RGBA'),2,2)
        # Normalize supplement canvas to the same body-height scale, one factor
        # across its four frames (never resize each pose to its own bounding box).
        h = statistics.median(bounds(seven[r*7+4])[3]-bounds(seven[r*7+4])[1] for r in range(4))
        eh = statistics.median(bounds(x)[3]-bounds(x)[1] for x in extra)
        factor = h/eh
        extra = [x.convert('RGBa').resize((round(x.width*factor),round(x.height*factor)),Image.Resampling.LANCZOS).convert('RGBA') for x in extra]
        return [extra[r] if d==5 else seven[r*7+(d if d<5 else d-1)] for r in range(4) for d in range(8)]
    return split(im,8,rows)

def pack():
    PREVIEW.mkdir(parents=True,exist_ok=True)
    report = {'baseline':'221757e','generator':'built-in image_gen','cell':128,'slots':168,'enemies':{}}
    roster=Image.new('RGB',(1024,330),(30,27,43));rd=ImageDraw.Draw(roster)
    for ni,name in enumerate(IDS):
        sheets={g:source_cells(name,g,r) for g,r,_ in GROUPS}
        atlas=Image.new('RGBA',(2048,2048)); frames={}; scales={}
        # Normalize groups using their first, standing/recoiling row, then apply
        # one global safety factor so all wings, hats and weapons fit the atlas.
        for g,rows,first in GROUPS:
            cells=sheets[g]
            reference=statistics.median(bounds(x)[3]-bounds(x)[1] for x in cells[:8])
            scales[g]=(82 if name=='bat' else 100)/reference
        maxw=max((bounds(x)[2]-bounds(x)[0])*scales[g] for g in sheets for x in sheets[g])
        maxh=max((bounds(x)[3]-bounds(x)[1])*scales[g] for g in sheets for x in sheets[g])
        safety=min(1,116/maxw,112/maxh)
        for g,rows,first in GROUPS:
            scale=scales[g]*safety
            for k,im in enumerate(sheets[g]):
                b=bounds(im)
                # A tight source crop prevents faint background pixels reaching
                # adjacent slots; all actual edge alpha is preserved inside it.
                crop=im.crop((max(0,b[0]-1),max(0,b[1]-1),min(im.width,b[2]+1),min(im.height,b[3]+1)))
                size=(max(1,round(crop.width*scale)),max(1,round(crop.height*scale)))
                crop=crop.convert('RGBa').resize(size,Image.Resampling.LANCZOS).convert('RGBA')
                cell=Image.new('RGBA',(128,128))
                cell.alpha_composite(crop,((128-size[0])//2,120-size[1]))
                slot=first*8+k;atlas.paste(cell,((slot%16)*128,(slot//16)*128));frames[slot]=cell
        target=OUT/f'castle_{name}.png';atlas.save(target,optimize=True)
        contact=Image.new('RGB',(1024,21*142),(30,27,43));draw=ImageDraw.Draw(contact)
        for frame in range(21):
            for d in range(8):contact.paste(frames[frame*8+d],(d*128,frame*142+14),frames[frame*8+d])
            draw.text((3,frame*142),str(frame),fill='white')
        contact.save(PREVIEW/(name+'_all_frames.jpg'),quality=90)
        animated=[]
        for f in list(range(4))+list(range(4,10))*2+list(range(10,21)):
            canvas=Image.new('RGB',(1024,150),(30,27,43));draw=ImageDraw.Draw(canvas)
            draw.text((8,4),f'{name}  frame {f} / directions 0..7',fill='white')
            for d in range(8):canvas.paste(frames[f*8+d],(128*d,20),frames[f*8+d])
            animated.append(canvas)
        animated[0].save(PREVIEW/(name+'_animation.gif'),save_all=True,append_images=animated[1:],duration=160,loop=0)
        old=Image.open(io.BytesIO(subprocess.check_output(['git','show','221757e:spellduel-unity/Assets/SpellDuel/Resources/SpriteEnemies/castle_'+name+'.png'],cwd=ROOT.parent))).convert('RGBA').crop((0,0,128,128))
        roster.paste(old,(ni*128,30),old);roster.paste(frames[0],(ni*128,190),frames[0]);rd.text((ni*128+4,165),name,fill='white')
        report['enemies'][name]={'sha256':hashlib.sha256(target.read_bytes()).hexdigest(),'groups':{g:{'rows':r,'scale':scales[g]*safety} for g,r,_ in GROUPS},'sources':{p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted((SOURCE/name).glob('*.png'))}}
    rd.text((4,4),'BEFORE',fill='white');rd.text((4,180),'REDESIGN',fill='white');roster.save(PREVIEW/'before_after.jpg',quality=94)
    (PREVIEW/'atlas_manifest.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
    print('Packed 8 atlases / 1344 frames; source alpha preserved.')

if __name__=='__main__':pack()
