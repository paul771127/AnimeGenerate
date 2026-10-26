"""Compose delivered native Unity PNGs without changing their aspect ratios.

The label overlay is explicitly a layout diagnostic using rectangles exported by
the same runtime placement function, not a captured Unity OnGUI frame.
"""
from pathlib import Path
import json
import shutil
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
NECK = ROOT / 'Docs/neck_hair_preview'
PENDING = ROOT / 'Docs/pending_preview'
IDS = 'novice swordsman archer mage assassin priest paladin berserker marksman ranger archmage summoner ninja shadowdancer bishop inquisitor'.split()
FONT_PATH = Path('C:/Windows/Fonts/msjh.ttc')
def font(size):
    if FONT_PATH.exists(): return ImageFont.truetype(str(FONT_PATH), size)
    return ImageFont.truetype('DejaVuSans.ttf', size)
def put(canvas, path, box):
    image = Image.open(path).convert('RGBA')
    x, y, w, h = box
    image.thumbnail((w, h), Image.Resampling.LANCZOS)
    canvas.paste(image, (x + (w-image.width)//2, y + (h-image.height)//2), image)
def label(canvas, at, text, size=22):
    ImageDraw.Draw(canvas).text(at, text, font=font(size), fill='#fff0c9')

def main():
    heads = Image.new('RGB', (1280, 1392), '#17243d')
    pairs = Image.new('RGB', (2048, 1744), '#17243d')
    views = ['front','side','45','overhead','back','low','full_front','full_45','gear_overhead']
    for n, identity in enumerate(IDS):
        x, y = (n % 4)*320, (n//4)*348
        put(heads, NECK/f'neck_{identity}_front.png', (x, y, 320, 320))
        label(heads, (x+8, y+320), identity)
        x, y = (n % 4)*512, (n//4)*436
        put(pairs, ROOT/f'Assets/SpellDuel/Resources/Story2D/classes/{identity}.png', (x,y+26,256,384))
        put(pairs, NECK/f'neck_{identity}_full_front.png', (x+256,y+26,256,384))
        label(pairs, (x+8,y+7), identity+' | 2D / Unity',18)
        sheet = Image.new('RGB', (960, 1092), '#17243d')
        label(sheet,(12,8),identity+' - native Unity views',26)
        for i, view in enumerate(views):
            xx, yy = (i%3)*320, 45+(i//3)*349
            put(sheet,NECK/f'neck_{identity}_{view}.png',(xx,yy,320,320))
            label(sheet,(xx+8,yy+320),view,20)
        sheet.save(NECK/f'{identity}_views.jpg',quality=89)
    heads.save(NECK/'all_16_heads.jpg',quality=91)
    pairs.save(NECK/'all_16_2d_3d.jpg',quality=91)
    compare = Image.new('RGB',(1280,2040),'#17243d')
    label(compare,(12,8),'Before 03e9233',28);label(compare,(652,8),'After / same native camera',28)
    for row, identity in enumerate(('novice','mage','bishop')):
        for col, phase in enumerate(('before','after')):
            put(compare,NECK/f'{phase}_{identity}_face.png',(col*640,42+row*666,640,640))
        label(compare,(12,682+row*666),identity,20)
    compare.save(NECK/'before_after.jpg',quality=92)
    states = Image.new('RGB',(1536,848),'#17243d')
    for i,state in enumerate(('alive','down','revive','offline')):
        x,y=(i%2)*768,(i//2)*424
        put(states,PENDING/f'pending_allies_six_{state}.png',(x,y+32,768,384))
        label(states,(x+10,y+6),state,22)
    states.save(PENDING/'allies_four_states.jpg',quality=91)
    overlay=Image.open(PENDING/'pending_allies_six_alive.png').convert('RGBA')
    layer=Image.new('RGBA',overlay.size);draw=ImageDraw.Draw(layer)
    report=json.loads((PENDING/'pending_ally_audit.json').read_text(encoding='utf-8'))
    for actor in report['allies']:
        r=actor['labelRect'];x,y,w,h=[r[k] for k in ('x','y','width','height')]
        draw.rounded_rectangle((x,y,x+w,y+h),radius=7,fill=(18,29,48,225),outline='#dbb778',width=2)
        draw.text((x+8,y+3),actor['id'],font=font(18),fill='white')
        draw.rectangle((x+w*.12,y+h-10,x+w*.88,y+h-4),fill='#60d698')
    overlay=Image.alpha_composite(overlay,layer).convert('RGB')
    label(overlay,(14,12),'Native 3D + runtime label rectangles (layout diagnostic)',22)
    overlay.save(PENDING/'allies_label_layout.jpg',quality=92)
    for source,target in [('all_16_heads.jpg','player_heads.jpg'),('all_16_2d_3d.jpg','players_2d_3d.jpg'),('before_after.jpg','player_before_after.jpg')]:shutil.copyfile(NECK/source,PENDING/target)
    print('16 native view sheets, 2D/3D lineup, before/after and six-ally diagnostics assembled.')
if __name__ == '__main__': main()
