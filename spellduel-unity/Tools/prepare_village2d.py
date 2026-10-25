#!/usr/bin/env python3
"""Package generated village artwork; coordinates use top-left origin and feet anchors."""
import argparse,json,re,uuid,hashlib
from pathlib import Path
from PIL import Image,ImageOps,ImageDraw,ImageFont
ROOT=Path(__file__).resolve().parents[1];ART=ROOT/'Assets/SpellDuel/Resources/Village2D';DOC=ROOT/'Docs/village2d'
def read(p):return json.loads(Path(p).read_text(encoding='utf-8-sig'))
def write(p,v):p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf-8')
def meta(path,alpha=True,max_size=2048):
    p=path.with_suffix(path.suffix+'.meta')
    if p.exists():return
    source=ROOT/('Assets/SpellDuel/Resources/Story2D/chars/erin_normal.png.meta' if alpha else 'Assets/SpellDuel/Resources/Story2D/bg/village.jpg.meta');t=source.read_text();t=re.sub(r'guid: [0-9a-f]+','guid: '+uuid.uuid4().hex,t).replace('maxTextureSize: 2048',f'maxTextureSize: {max_size}');p.write_text(t,encoding='utf-8')
def record(dest,source,manifest):
    with Image.open(dest) as im:size=im.size;mode=im.mode
    manifest[dest.relative_to(ROOT).as_posix()]={'size':size,'mode':mode,'sourceSize':Image.open(source['path']).size,'bytes':dest.stat().st_size,'sha256':hashlib.sha256(dest.read_bytes()).hexdigest()}
def main():
    p=argparse.ArgumentParser();p.add_argument('--sources',required=True);p.add_argument('--kind',choices=['map','npc','bg','pose','ui','anvil'],required=True);p.add_argument('--size',nargs=2,type=int);a=p.parse_args();sources=read(a.sources);ART.mkdir(parents=True,exist_ok=True);DOC.mkdir(parents=True,exist_ok=True);manifest=read(DOC/'assets.json') if (DOC/'assets.json').exists() else {};prompts=read(DOC/'prompts.json') if (DOC/'prompts.json').exists() else {}
    for key,s in sources.items():
        im=Image.open(s['path']).convert('RGBA');size=tuple(a.size) if a.size else {'map':(2048,3072),'npc':(160,240),'bg':(1080,1920),'pose':(768,1152),'ui':(384,96),'anvil':(512,384)}[a.kind]
        if a.kind=='map':
            im=im.convert('RGB').resize(size,Image.Resampling.LANCZOS);dest=ART/f'{key}.png';im.save(dest,optimize=True);meta(dest,False,4096);record(dest,s,manifest)
            fit=Image.new('RGB',(1080,1920),'#ddd5bc');fit.paste(im.resize((1080,1620),Image.Resampling.LANCZOS),(0,150));dest=ART/f'{key}_fit.png';fit.save(dest,optimize=True);meta(dest,False);record(dest,s,manifest)
        else:
            if a.kind=='npc':
                small=ImageOps.contain(im,(144,224),Image.Resampling.LANCZOS);im=Image.new('RGBA',size);im.alpha_composite(small,((160-small.width)//2,232-small.height))
            elif a.kind in ['anvil','ui']:
                small=ImageOps.contain(im,size,Image.Resampling.LANCZOS);im=Image.new('RGBA',size);im.alpha_composite(small,((size[0]-small.width)//2,(size[1]-small.height)//2))
            else:im=ImageOps.fit(im,size,method=Image.Resampling.LANCZOS)
            dest=(ROOT/'Assets/SpellDuel/Resources/Story2D/chars' if a.kind=='pose' else ART/'ui' if a.kind=='ui' else ART)/(key+('.jpg' if a.kind=='bg' else '.png'));dest.parent.mkdir(parents=True,exist_ok=True)
            if a.kind=='bg':im.convert('RGB').save(dest,quality=86,optimize=True,subsampling=2)
            else:im.save(dest,optimize=True)
            meta(dest,a.kind!='bg');record(dest,s,manifest)
        prompts[key]=s['prompt'];print(key,flush=True)
    write(DOC/'assets.json',manifest);write(DOC/'prompts.json',prompts)
if __name__=='__main__':main()
