#!/usr/bin/env python3
"""Package image_gen class portraits and compose full/cropped review sheets."""
import argparse,json,re,uuid,hashlib,sys
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
from prepare_story2d import save_rgba
ROOT=Path(__file__).resolve().parents[1];ART=ROOT/'Assets/SpellDuel/Resources/Story2D';DOC=ROOT/'Docs/story2d_classes_stage2'
IDS='novice swordsman archer mage assassin priest paladin berserker marksman ranger archmage summoner ninja shadowdancer bishop inquisitor'.split()
NAMES='初心者 劍士 獵人 法師 刺客 牧師 聖騎士 劍仙 神射手 遊俠 大魔導士 召喚師 忍者 影舞者 主教 審判者'.split()
def main():
    p=argparse.ArgumentParser();p.add_argument('--sources',required=True);p.add_argument('--offsets');a=p.parse_args();sources=json.loads(Path(a.sources).read_text(encoding='utf-8-sig'));offsets=json.loads(Path(a.offsets).read_text()) if a.offsets else {};DOC.mkdir(parents=True,exist_ok=True);out=ART/'classes';out.mkdir(exist_ok=True);records={};prompts={}
    for identity,spec in sources.items():
        assert identity in IDS,identity
        im=Image.open(spec['path']).convert('RGBA').resize((768,1152),Image.Resampling.LANCZOS);offset=offsets.get(identity,[-28,61]);frame=Image.new('RGBA',(768,1152));frame.alpha_composite(im,tuple(offset));alpha=frame.getchannel('A');rgba=frame.convert('RGB').quantize(colors=256,method=Image.Quantize.MEDIANCUT,dither=Image.Dither.NONE).convert('RGBA');rgba.putalpha(alpha);dest=out/f'{identity}.png';save_rgba(rgba,dest)
        meta=dest.with_suffix('.png.meta')
        if not meta.exists():meta.write_text(re.sub(r'guid: [0-9a-f]+','guid: '+uuid.uuid4().hex,(ART/'chars/erin_normal.png.meta').read_text()),encoding='utf-8')
        records[identity]={'size':[768,1152],'offset':offset,'bytes':dest.stat().st_size,'sha256':hashlib.sha256(dest.read_bytes()).hexdigest()};prompts[identity]=spec['prompt'];print(identity,dest.stat().st_size,'bytes',flush=True)
    (DOC/'assets.json').write_text(json.dumps(records,indent=2),encoding='utf-8');(DOC/'prompts.json').write_text(json.dumps(prompts,indent=2),encoding='utf-8');font=ImageFont.truetype('C:/Windows/Fonts/msjh.ttc',20)
    for start in range(0,len(IDS),4):
        sheet=Image.new('RGB',(1200,720),'#192537');d=ImageDraw.Draw(sheet)
        for col,identity in enumerate(IDS[start:start+4]):
            path=out/f'{identity}.png'
            if not path.exists():continue
            im=Image.open(path).convert('RGBA');tile=Image.new('RGBA',(300,450),'#dce4ed');tile.alpha_composite(im.resize((300,450),Image.Resampling.LANCZOS));sheet.paste(tile.convert('RGB'),(col*300,35));crop=im.crop((230,23,538,346)).resize((180,189),Image.Resampling.LANCZOS);tile=Image.new('RGBA',crop.size,'#dce4ed');tile.alpha_composite(crop);sheet.paste(tile.convert('RGB'),(col*300+60,495));d.text((col*300+8,5),NAMES[IDS.index(identity)]+' / '+identity,font=font,fill='#f7dba4');d.text((col*300+55,690),'實際小頭像裁切',font=font,fill='#e9dfc9')
        sheet.save(DOC/f'portraits_{start//4+1}.jpg',quality=92,optimize=True)
    sheet=Image.new('RGB',(1200,1840),'#192537');d=ImageDraw.Draw(sheet)
    for i,identity in enumerate(IDS):
        path=out/f'{identity}.png'
        if not path.exists():continue
        im=Image.open(path).convert('RGBA').resize((300,450),Image.Resampling.LANCZOS);tile=Image.new('RGBA',im.size,'#dce4ed');tile.alpha_composite(im);x=(i%4)*300;y=(i//4)*460;sheet.paste(tile.convert('RGB'),(x,y));d.rectangle((x,y+420,x+300,y+459),fill='#192537');d.text((x+8,y+428),NAMES[i]+' / '+identity,font=font,fill='#f7dba4')
    sheet.save(DOC/'all_classes.jpg',quality=92,optimize=True)
if __name__=='__main__':main()
