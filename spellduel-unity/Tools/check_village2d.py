#!/usr/bin/env python3
"""Validate Village2D image specifications, hotspot geometry and handoff assets."""
import argparse,json,re
from pathlib import Path
from PIL import Image,ImageFilter
import numpy as np
ROOT=Path(__file__).resolve().parents[1];ART=ROOT/'Assets/SpellDuel/Resources/Village2D';DOC=ROOT/'Docs/village2d'
TOWNS={'flame':['laoyan','xiaotie','mila'],'forest':['cui','feng','xiaoye'],'harbor':['haido','nagi','achao'],'desert':['leo','xiaosha','tieniang'],'snow':['laotuo','aling','shi']}
KEYS=['smith','shop','guild','dungeon','training','bounty','watch','warp','gate'];UI=['name_plate','list_frame','list_row','list_row_sel','speech_bubble','btn_big','btn_big_press']
def main():
    p=argparse.ArgumentParser();p.add_argument('--partial',action='store_true');a=p.parse_args();pending=[];checked=[]
    def exists(path):
        if path.exists():return True
        if a.partial:pending.append(path.name);return False
        raise AssertionError(('missing',str(path)))
    def picture(path,size,alpha=False):
        if not exists(path):return
        with Image.open(path) as im:
            assert im.size==size,(path,im.size,size)
            if alpha:assert im.mode=='RGBA' and im.getchannel('A').getextrema()[0]==0 and im.getchannel('A').getextrema()[1]>200,(path,'RGBA alpha')
        meta=path.with_suffix(path.suffix+'.meta');assert meta.is_file(),path;t=meta.read_text();assert 'nPOTScale: 0' in t and 'enableMipMap: 0' in t,path
        if alpha:assert 'alphaSource: 1' in t,path
        checked.append(path.relative_to(ROOT).as_posix())
    for town,npcs in TOWNS.items():
        picture(ART/f'map_{town}.png',(2048,3072));picture(ART/f'map_{town}_fit.png',(1080,1920));path=ART/f'map_{town}.json'
        if exists(path):
            data=json.loads(path.read_text(encoding='utf-8'));assert all(k in data for k in KEYS+['belltower','mayorhome','npc']),path
            for key,v in data.items():
                if key=='npc':continue
                assert all(k in v for k in ['x','y','w','h','label_x','label_y']),(path,key)
                assert v['w']>0 and v['h']>0 and 0<=v['x']<=2048-v['w'] and 0<=v['y']<=3072-v['h'],(path,key)
                assert 0<=v['label_x']<=2048 and 0<=v['label_y']<=3072,(path,key)
            assert set(data['npc'])==set(npcs),(path,'NPC IDs')
            for id,v in data['npc'].items():assert 0<=v['x']<=2048 and 0<=v['y']<=3072,(path,id)
            rects=[(k,v) for k,v in data.items() if k!='npc']
            for i,(ka,ra) in enumerate(rects):
                for kb,rb in rects[i+1:]:
                    overlap=max(0,min(ra['x']+ra['w'],rb['x']+rb['w'])-max(ra['x'],rb['x']))*max(0,min(ra['y']+ra['h'],rb['y']+rb['h'])-max(ra['y'],rb['y']))
                    assert overlap==0,(town,ka,kb,'ambiguous overlapping hitboxes')
        for id in npcs:picture(ART/f'npc_{id}.png',(160,240),True)
    for key in KEYS:picture(ART/f'in_{key}_flame.jpg',(1080,1920))
    for i in range(6):picture(ART/f'anvil_{i}.png',(512,384),True)
    for key in UI:
        path=ART/'ui'/f'{key}.png'
        if exists(path):
            with Image.open(path) as im:size=im.size
            picture(path,size,True)
    castpath=ART/'cast.json'
    if exists(castpath):
        for id,spec in json.loads(castpath.read_text(encoding='utf-8')).items():
            master=None
            for expr in spec['expressions']:
                path=ROOT/f'Assets/SpellDuel/Resources/Story2D/chars/{id}_{expr}.png';picture(path,(768,1152),True)
                if not path.exists():continue
                pixels=np.array(Image.open(path))
                if not spec['poseVariants']:
                    if master is None:master=pixels
                    else:
                        mask=DOC/'staff_masks'/f'{id}.png';assert mask.exists(),mask
                        allowed=np.array(Image.open(mask).convert('L').filter(ImageFilter.MaxFilter(9)))>0
                        delta=np.any(pixels!=master,axis=2);assert not (delta&~allowed).any(),(id,expr,'outside-face pixel drift');assert delta.sum()>15,(id,expr,'identical expression')
    for key in KEYS:
        batch=2 if key=='smith' else 3 if key in ['shop','guild'] else 4
        path=DOC/f'batch{batch}/{key}_layout.json'
        if exists(path):
            spec=json.loads(path.read_text(encoding='utf-8'));assert spec['canvas']==[1080,1920],path
            ids=[]
            for v in spec['elements']:
                ids.append(v['id']);x,y,w,h=v['rect'];assert 0<=x and 0<=y and w>0 and h>0 and x+w<=1080 and y+h<=1920,(key,v)
            assert len(ids)==len(set(ids)),(key,'duplicate control IDs');assert 'return' in ids and 'npc' in ids,(key,'missing common controls')
    for name,section in [('lines_smith.txt',['success','failure']),('lines_shop.txt',['sale','insufficient','sell'])]:
        path=ART/name
        if exists(path):
            groups={};current=None
            for line in path.read_text(encoding='utf-8').splitlines():
                if line.startswith('['):current=line.strip('[]');groups[current]=[]
                elif line.strip():assert current;groups[current].append(line)
            assert all(k in groups for k in section),path
            if name=='lines_smith.txt':assert all(len(groups[k])>=8 for k in section),path
    slices=ART/'ui/slices.json'
    if exists(slices):
        data=json.loads(slices.read_text(encoding='utf-8'));assert set(data)==set(UI)
        for key,v in data.items():
            t=(ART/'ui'/f'{key}.png.meta').read_text();assert 'textureType: 8' in t and 'spriteBorder:' in t,(key,'9slice import')
    frames=[ART/f'anvil_{i}.png' for i in range(6)]
    if all(p.exists() for p in frames):
        lower=Image.open(frames[0]).crop((0,192,512,384)).tobytes()
        assert all(Image.open(p).crop((0,192,512,384)).tobytes()==lower for p in frames),'anvil base drift'
    guids=[]
    for path in ART.rglob('*.meta'):
        m=re.search(r'^guid: ([0-9a-f]{32})$',path.read_text(),re.M);assert m,path;guids.append(m[1])
    assert len(guids)==len(set(guids)),'duplicate asset GUID'
    DOC.mkdir(parents=True,exist_ok=True);(DOC/'validation.json').write_text(json.dumps({'complete':not pending,'checked':checked,'pending':pending},indent=2),encoding='utf-8');print(len(checked),'images validated;',len(pending),'pending')
if __name__=='__main__':main()
