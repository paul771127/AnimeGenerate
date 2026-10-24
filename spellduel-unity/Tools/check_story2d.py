#!/usr/bin/env python3
"""Validate the first Story2D art batch; Pillow and numpy required."""
from pathlib import Path
import json
import numpy as np
from PIL import Image, ImageFilter

ROOT=Path(__file__).resolve().parents[1]
ART=ROOT/'Assets/SpellDuel/Resources/Story2D'
DOC=ROOT/'Docs/story2d_stage1'
EXPRS=('normal','smile','angry','sad','surprised','serious')

def main():
    count=0
    for identity in ('erin','laoyan'):
        master=np.array(Image.open(ART/'chars'/f'{identity}_normal.png').convert('RGBA'))
        allowed=np.array(Image.open(DOC/f'{identity}_face_mask.png').filter(ImageFilter.MaxFilter(9)))>0
        for expression in EXPRS:
            path=ART/'chars'/f'{identity}_{expression}.png'
            with Image.open(path) as image:
                assert image.size==(768,1152),path
                assert image.mode=='RGBA',path
                pixels=np.array(image)
            assert pixels[:,:,3].min()==0 and pixels[:,:,3].max()>240,path
            changed=np.any(pixels!=master,axis=2)
            assert not np.any(changed & ~allowed),(path,'non-face pixels changed')
            assert expression=='normal' or changed.sum()>20,(path,'expression is missing')
            assert path.with_suffix('.png.meta').is_file(),path
            count+=1
    with Image.open(ART/'bg/village.jpg') as image:
        assert image.size==(1080,1920) and image.mode=='RGB'
    size=sum(p.stat().st_size for p in ART.rglob('*') if p.suffix.lower() in ('.png','.jpg','.jpeg'))
    assert size<=40*1024*1024,size
    print(f'{count} transparent sprites + village: dimensions, expressions, unchanged non-face pixels passed; {size/1024/1024:.2f} MiB')

if __name__=='__main__':main()
