#!/usr/bin/env python3
"""Validate class-selection art; stage one is deliberately limited to three approved candidates."""
import argparse,json,re,hashlib
from pathlib import Path
from PIL import Image
ROOT=Path(__file__).resolve().parents[1];ART=ROOT/'Assets/SpellDuel/Resources/Story2D/classes';DOC=ROOT/'Docs/story2d_classes_stage1'
IDS='novice swordsman archer mage assassin priest paladin berserker marksman ranger archmage summoner ninja shadowdancer bishop inquisitor'.split()
def main():
    p=argparse.ArgumentParser();p.add_argument('--all',action='store_true');a=p.parse_args();expected=IDS if a.all else ['novice','swordsman','mage'];records=[];guids=[]
    assert not {f.stem for f in ART.glob('*.png')}-set(IDS),'unknown class ID'
    for identity in expected:
        path=ART/f'{identity}.png'
        with Image.open(path) as im:
            assert im.size==(768,1152) and im.mode=='RGBA',(identity,im.size,im.mode)
            alpha=im.getchannel('A');assert alpha.getextrema()==(0,255),(identity,'transparent alpha missing')
            crop=im.crop((230,23,538,346));assert crop.getchannel('A').getbbox(),(identity,'empty thumbnail crop')
        text=path.with_suffix('.png.meta').read_text(encoding='utf-8');assert all(s in text for s in ['enableMipMap: 0','maxTextureSize: 2048','alphaSource: 1','nPOTScale: 0']),identity
        guid=re.search(r'^guid: ([0-9a-f]{32})$',text,re.M);assert guid,identity;guids.append(guid[1]);records.append({'id':identity,'size':[768,1152],'mode':'RGBA','bytes':path.stat().st_size})
    assert len(guids)==len(set(guids)),'duplicate GUID'
    approved=json.loads((DOC/'assets.json').read_text(encoding='utf-8'))
    for identity,record in approved.items():assert hashlib.sha256((ART/f'{identity}.png').read_bytes()).hexdigest()==record['sha256'],(identity,'approved first-batch art changed')
    if a.all:
        doc=ROOT/'Docs/story2d_classes_stage2'
        for name in ['all_classes.jpg']+[f'portraits_{i}.jpg' for i in range(1,5)]:assert (doc/name).is_file(),name
    report={'stage':'all' if a.all else 'first three, awaiting user style confirmation','assets':records,'pending':[i for i in IDS if not (ART/f'{i}.png').exists()],'totalBytes':sum(x['bytes'] for x in records)};doc=ROOT/'Docs/story2d_classes_stage2' if a.all else DOC;doc.mkdir(parents=True,exist_ok=True);(doc/'validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8');print(f'{len(records)} class portraits passed; {report["totalBytes"]:,} bytes; pending {len(report["pending"])}')
if __name__=='__main__':main()
