#!/usr/bin/env python3
"""Compose art-review sheets and dialogue layout mockups; does not change game art."""
import json,re
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont,ImageEnhance,ImageOps
ROOT=Path(__file__).resolve().parents[1];ART=ROOT/'Assets/SpellDuel/Resources/Story2D';DOC=ROOT/'Docs/story2d_stage2'
FONT='C:/Windows/Fonts/msjh.ttc'
def font(size):
    try:return ImageFont.truetype(FONT,size)
    except OSError:return ImageFont.truetype('DejaVuSans.ttf',size)
def labels():
    cs=(ROOT/'Assets/SpellDuel/Runtime/Story2D.cs').read_text(encoding='utf-8-sig').split('public static readonly string[] Exprs')[0]
    return {v:k for k,v in re.findall(r'\["([^\"]+)"\]\s*=\s*"([^\"]+)"',cs)}
def wrapped(draw,text,xy,width,size=38,fill='white'):
    x,y=xy;line='';f=font(size)
    for char in text:
        if draw.textlength(line+char,font=f)>width:
            draw.text((x,y),line,font=f,fill=fill);y+=round(size*1.55);line=''
        line+=char
    draw.text((x,y),line,font=f,fill=fill)
def dialogue(key,bg,left,right,who,text):
    if any(not (ART/'chars'/f'{p}.png').exists() for p in [left,right]):return
    scene=Image.open(ART/'bg'/f'{bg}.jpg').convert('RGBA');W,H=scene.size;box_y=1423;box_h=480
    for identity,is_left,active in [(right,False,False),(left,True,True)]:
        sprite=Image.open(ART/'chars'/f'{identity}.png').convert('RGBA');scale=1 if active else .94;h=round(H*.66*scale);w=round(h*sprite.width/sprite.height);sprite=sprite.resize((w,h),Image.Resampling.LANCZOS)
        if not is_left:sprite=ImageOps.mirror(sprite)
        if not active:
            a=sprite.getchannel('A');rgb=ImageEnhance.Brightness(sprite.convert('RGB')).enhance(.44).convert('RGBA');rgb.putalpha(a);sprite=rgb
        x=round(W*(.33 if is_left else .67)-w/2);y=round(box_y+box_h*.35-h);scene.alpha_composite(sprite,(x,y))
    overlay=Image.new('RGBA',scene.size);d=ImageDraw.Draw(overlay);d.rectangle((0,box_y,W,H),fill=(8,11,25,238));d.rectangle((0,box_y,W,box_y+4),fill='#efd18c');d.polygon([(50,box_y-50),(360,box_y-50),(340,box_y+25),(50,box_y+25)],fill='#315d79');d.text((75,box_y-37),who,font=font(40),fill='white');wrapped(d,text,(58,box_y+75),W-116);d.text((W-65,H-72),'▼',font=font(28),fill='#efd18c');d.text((40,34),'美術排版示意・非 Unity 實機截圖',font=font(23),fill='#fff4db');d.text((W-385,34),'自動 ▶     紀錄     跳過',font=font(25),fill='white');scene=Image.alpha_composite(scene,overlay);scene.convert('RGB').save(DOC/f'dialogue_{key}.jpg',quality=88,optimize=True)
def main():
    DOC.mkdir(parents=True,exist_ok=True);names=labels();ids=sorted(names);cols=7;cw,ch=170,290;sheet=Image.new('RGB',(cols*cw,((len(ids)+cols-1)//cols)*ch),'#1c2636');d=ImageDraw.Draw(sheet)
    for i,k in enumerate(ids):
        p=ART/'chars'/f'{k}_normal.png'
        if not p.exists():continue
        im=Image.open(p).convert('RGBA');im.thumbnail((cw,240));tile=Image.new('RGBA',(cw,240),'#dce5ec');tile.alpha_composite(im,((cw-im.width)//2,0));x=i%cols*cw;y=i//cols*ch;sheet.paste(tile.convert('RGB'),(x,y));d.text((x+5,y+243),names[k],font=font(20),fill='#ffe4a5');d.text((x+5,y+267),k,font=font(14),fill='#c5d3de')
    sheet.save(DOC/'cast_lineup.jpg',quality=90,optimize=True)
    bgs=sorted((ART/'bg').glob('*.jpg'));cols=6;cw,ch=200,390;sheet=Image.new('RGB',(cols*cw,((len(bgs)+cols-1)//cols)*ch),'#1c2636');d=ImageDraw.Draw(sheet)
    for i,p in enumerate(bgs):
        im=Image.open(p).resize((200,356),Image.Resampling.LANCZOS);x=i%cols*cw;y=i//cols*ch;sheet.paste(im,(x,y));d.text((x+4,y+360),p.stem,font=font(16),fill='#ffe4a5')
    sheet.save(DOC/'backgrounds.jpg',quality=90,optimize=True)
    dialogue('mine','dungeon','xiaotie_serious','foreman_sad','小鐵','阿岩哥！大家保護他，怪物會先撲向受傷的人！')
    dialogue('forest','town_forest','cui_smile','erin_normal','翠','森之里留一份名冊。我請米菈先送另一份給海斗，他是白帆號船長的後人。')
    dialogue('sea','boss_sea','mio_smile','haido_serious','澪','岸有燈，杯有茶……海斗，船長說，你把家守得很好。')
    dialogue('arena','boss_arena','leo_sad','bronze_normal','雷歐','活得很好。只是每天都在想，那天如果換我站在你的位置。')
    dialogue('canyon','boss_canyon','aling_smile','laotuo_normal','阿鈴','爹，我聽見了。驛站的鈴，會一直響下去的。')
    dialogue('guild','guild','guildmaster_serious','towerkeeper_normal','公會長','這一戰，我無法給你們任何建議。只有一句：誰倒下了，就去把他扶起來。')
    print('Preview sheets and available dialogue layouts written to',DOC)
if __name__=='__main__':main()
