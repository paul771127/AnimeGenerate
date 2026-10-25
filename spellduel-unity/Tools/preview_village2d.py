#!/usr/bin/env python3
"""Compose village art handoff mockups; never writes production backgrounds."""
import json
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
ROOT=Path(__file__).resolve().parents[1];ART=ROOT/'Assets/SpellDuel/Resources/Village2D';DOC=ROOT/'Docs/village2d'
NAMES={'smith':'⚒ 鐵匠鋪','shop':'🧪 道具店','guild':'🏛 職業工會','dungeon':'🏰 副本入口','training':'📅 訓練場','bounty':'🔥 討伐告示板','watch':'👀 觀戰席','warp':'🌀 傳送陣','gate':'🚪 村口','belltower':'🔔 晨鐘塔','mayorhome':'🏠 村長家'}
def fonts(size):return ImageFont.truetype('C:/Windows/Fonts/msjh.ttc',size),ImageFont.truetype('C:/Windows/Fonts/seguiemj.ttf',size)
def rich(draw,xy,text,size=26,fill='#fff4dd',center=False):
    regular,emoji=fonts(size);parts=[(c,emoji if ord(c)>=0x1f000 or 0x2600<=ord(c)<=0x27ff else regular) for c in text if c not in '\ufe0f\u200d'];width=sum(draw.textlength(c,font=f) for c,f in parts);x,y=xy
    if center:x-=width/2;y-=size*.6
    for c,f in parts:draw.text((x,y),c,font=f,fill=fill,embedded_color=True);x+=draw.textlength(c,font=f)
def map_preview(town):
    p=ART/f'map_{town}.json'
    if not p.exists():return
    data=json.loads(p.read_text(encoding='utf-8'));im=Image.open(ART/f'map_{town}_fit.png').convert('RGBA');d=ImageDraw.Draw(im);scale=1080/2048
    for key,v in data.items():
        if key=='npc':continue
        x=round(v['label_x']*scale);y=round(v['label_y']*scale+150)
        from preview_village_facilities import frame
        if not frame(im,'name_plate',[x-112,y-27,224,54]):d.rounded_rectangle((x-112,y-27,x+112,y+27),radius=10,fill='#674730',outline='#ebca83',width=2)
        d=ImageDraw.Draw(im);rich(d,(x,y),NAMES[key],24,fill='#4d3523',center=True)
    for identity,v in data['npc'].items():
        path=ART/f'npc_{identity}.png'
        if not path.exists():continue
        npc=Image.open(path).convert('RGBA').resize((84,127),Image.Resampling.LANCZOS);im.alpha_composite(npc,(round(v['x']*scale-42),round(v['y']*scale+150-127)))
    d=ImageDraw.Draw(im);rich(d,(35,50),'📋 清單　｜　🚶 走動　｜　村莊地圖',28,fill='#4d3523');out=DOC/('batch1' if town=='flame' else 'maps');out.mkdir(parents=True,exist_ok=True);im.convert('RGB').save(out/f'map_{town}_labels.jpg',quality=92,optimize=True)
    d=ImageDraw.Draw(im)
    for key,v in data.items():
        if key=='npc':continue
        box=(v['x']*scale,v['y']*scale+150,(v['x']+v['w'])*scale,(v['y']+v['h'])*scale+150);d.rectangle(box,outline='#ff664d',width=3);rich(d,(box[0]+4,box[1]+4),key,19,fill='#fff5ba')
    im.convert('RGB').save(out/f'map_{town}_hotspots.jpg',quality=90,optimize=True)
def main():
    for town in ['flame','forest','harbor','desert','snow']:map_preview(town)
if __name__=='__main__':main()
