#!/usr/bin/env python3
"""Validate dialogue cast/backgrounds; class-selection art has its own validator and size report."""
import argparse,json,re,hashlib
from pathlib import Path
import numpy as np
from PIL import Image,ImageFilter
ROOT=Path(__file__).resolve().parents[1]
ART=ROOT/'Assets/SpellDuel/Resources/Story2D'
DOC=ROOT/'Docs/story2d_stage2'
EXPRS=('normal','smile','angry','sad','surprised','serious')
BOSSES={'golem','treeking','kraken','bronze','rockfiend','overlord','lavashade','treeshade','tideshade'}
GHOSTS={'mio','lavashade','treeshade','tideshade'}
BACKGROUNDS={'village','town_forest','town_port','town_arena','town_canyon','guild','tower','dungeon'}|{p+'_'+t for p in ('stage','boss') for t in ('volcano','forest','sea','arena','canyon')}

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--partial',action='store_true',help='check complete existing batches during staged production');args=parser.parse_args()
    cs=(ROOT/'Assets/SpellDuel/Runtime/Story2D.cs').read_text(encoding='utf-8-sig').split('public static readonly string[] Exprs')[0]
    cast=dict(re.findall(r'\["([^\"]+)"\]\s*=\s*"([^\"]+)"',cs));ids=set(cast.values());pending=[];records=[];present=set()
    for identity in sorted(ids):
        expressions=('normal','angry') if identity in BOSSES else EXPRS
        files=[ART/'chars'/f'{identity}_{e}.png' for e in expressions]
        if args.partial and not any(p.exists() for p in files):pending.append(identity);continue
        assert all(p.is_file() for p in files),(identity,'missing', [p.name for p in files if not p.exists()])
        with Image.open(files[0]) as image:
            assert image.mode=='RGBA',files[0]
            size=image.size;master=np.array(image)
        assert size in ((768,1152),(640,960)),(identity,size)
        mask=DOC/'masks'/f'{identity}.png'
        if not mask.exists():mask=ROOT/'Docs/story2d_stage1'/f'{identity}_face_mask.png'
        assert mask.is_file(),(identity,'missing face mask')
        im=Image.open(mask).convert('L');im=im.resize(size,Image.Resampling.LANCZOS) if im.size!=size else im
        allowed=np.array(im.filter(ImageFilter.MaxFilter(9)))>0;changes=[]
        for expr,path in zip(expressions,files):
            with Image.open(path) as image:
                assert image.size==size and image.mode=='RGBA',path
                pixels=np.array(image)
            alpha=pixels[:,:,3];assert alpha.min()==0 and alpha.max()>(60 if identity in GHOSTS else 240),(path,'alpha')
            diff=np.any(pixels!=master,axis=2);assert not (diff&~allowed).any(),(path,'outside-face pixels changed')
            assert expr=='normal' or diff.sum()>15,(path,'expression is identical to normal')
            changes.append({'expression':expr,'changedPixels':int(diff.sum())});assert path.with_suffix('.png.meta').is_file(),path
        present.add(identity);records.append({'id':identity,'size':size,'outsideFacePixelsIdentical':True,'changes':changes})
    backgrounds=[]
    for identity in sorted(BACKGROUNDS):
        path=ART/'bg'/f'{identity}.jpg'
        if args.partial and not path.exists():pending.append('bg/'+identity);continue
        with Image.open(path) as image:assert image.size==(1080,1920) and image.mode=='RGB',path
        backgrounds.append(identity);assert path.with_suffix('.jpg.meta').is_file(),path
    expected={f'{identity}_{expr}.png' for identity in ids for expr in (('normal','angry') if identity in BOSSES else EXPRS)}
    village_cast=ROOT/'Assets/SpellDuel/Resources/Village2D/cast.json'
    if village_cast.exists():
        for identity,spec in json.loads(village_cast.read_text(encoding='utf-8')).items():
            expected.update(f'{identity}_{e}.png' for e in spec['expressions'])
    assert not {p.name for p in (ART/'chars').glob('*.png')}-expected,'unknown sprites'
    assert not {p.stem for p in (ART/'bg').glob('*.jpg')}-BACKGROUNDS,'unknown backgrounds'
    guids=[]
    for meta in ART.rglob('*.meta'):
        text=meta.read_text(encoding='utf-8');assert 'nPOTScale: 0' in text and 'maxTextureSize: 2048' in text and 'enableMipMap: 0' in text,meta
        guid=re.search(r'^guid: ([0-9a-f]{32})$',text,re.M);assert guid,meta;guids.append(guid[1])
    assert len(guids)==len(set(guids)),'duplicate texture GUID'
    annotations=0
    integrity=json.loads((DOC/'script_integrity.json').read_text(encoding='utf-8'))
    for name in ('story.txt','dungeons.txt'):
        script=(ROOT/'Assets/SpellDuel/Resources'/name).read_text(encoding='utf-8')
        canonical='\n'.join(re.sub(r'［[^］]+］(?=：)','',l) for l in script.splitlines() if not l.startswith('背景：'))
        assert hashlib.sha256(canonical.encode()).hexdigest()==integrity[name]['canonicalSha256'],(name,'original text/settings changed')
        for no,line in enumerate(script.splitlines(),1):
            if line.startswith('背景：'):
                key=line.split('：',1)[1].strip();assert key in BACKGROUNDS,(name,no,key)
                assert key in backgrounds,(name,no,'background not supplied',key)
                continue
            match=re.match(r'^([^：]+)［([^］]+)］：',line)
            if match:
                who=re.split(r'[（(]',match[1])[0].split('・')[-1];expr=match[2];assert expr in EXPRS,(name,no,expr)
                if who in cast:
                    identity=cast[who];allowedExpr=('normal','angry') if identity in BOSSES else EXPRS
                    assert expr in allowedExpr,(name,no,'unsupported boss expression')
                    if not args.partial:assert identity in present,(name,no,'sprite not supplied',identity)
                annotations+=1
            elif not args.partial and '：' in line:
                who=re.split(r'[（(]',line.split('：',1)[0])[0].split('・')[-1]
                assert who not in cast,(name,no,'unmarked cast line',who)
    size=sum(p.stat().st_size for p in ART.rglob('*') if p.suffix.lower() in ('.png','.jpg','.jpeg'))
    class_size=sum(p.stat().st_size for p in (ART/'classes').glob('*.png'))
    dialogue_size=size-class_size
    if not args.partial:assert dialogue_size<=40*1024*1024,('dialogue art exceeds 40 MiB',dialogue_size)
    if not args.partial:
        for identity in ids:assert (DOC/f'{identity}_expressions.png').is_file(),(identity,'missing expression overview')
        for key in ('mine','forest','sea','arena','canyon','guild'):assert (DOC/f'dialogue_{key}.jpg').is_file(),(key,'missing dialogue mockup')
        for name in ('cast_lineup.jpg','backgrounds.jpg'):assert (DOC/name).is_file(),name
    report={'complete':not pending,'characters':records,'backgrounds':backgrounds,'pending':pending,'totalBytes':size,'dialogueBytes':dialogue_size,'classPortraitBytes':class_size,'combinedExceeds40MiB':size>40*1024*1024,'annotatedLines':annotations}
    DOC.mkdir(parents=True,exist_ok=True);(DOC/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    print(f'{len(records)} characters / {sum(len(r["changes"]) for r in records)} sprites, {len(backgrounds)} backgrounds; dialogue {dialogue_size/1024/1024:.2f} MiB + classes {class_size/1024/1024:.2f} MiB = {size/1024/1024:.2f} MiB; dialogue checks passed; pending {len(pending)}')
    if size>40*1024*1024:print('Combined dialogue + class art exceeds the original 40 MiB target; see the separate class-art report.')
if __name__=='__main__':main()
