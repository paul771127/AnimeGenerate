"""Composite imagegen seam repairs with a fixed feather mask, then split region edges."""
import argparse,hashlib
from PIL import Image,ImageOps
import numpy as np
from prepare_worldmap import ROOT,ART,DOC,SOURCE,read,write,meta
def main():
 p=argparse.ArgumentParser();p.add_argument('--through',type=int,required=True);end=p.parse_args().through;regions={}
 for n in range(1,end+1):
  s=read(SOURCE/f'worldmap_region_{n}.json')[f'region_{n}'];regions[n]=ImageOps.fit(Image.open(s['path']).convert('RGB'),(2160,1920),method=Image.Resampling.LANCZOS)
 reports=[];prompts=read(DOC/'prompts.json')
 for n in range(1,end):
  src=read(SOURCE/f'worldmap_seam_{n}.json')[f'seam_{n}'];original=Image.new('RGB',(768,1920));original.paste(regions[n].crop((1776,0,2160,1920)),(0,0));original.paste(regions[n+1].crop((0,0,384,1920)),(384,0));repair=ImageOps.fit(Image.open(src['path']).convert('RGB'),(768,1920),method=Image.Resampling.LANCZOS)
  weights=np.zeros(768,dtype=np.uint8);weights[192:288]=np.linspace(0,255,96).astype(np.uint8);weights[288:480]=255;weights[480:576]=np.linspace(255,0,96).astype(np.uint8);mask=Image.fromarray(np.tile(weights,(1920,1)));merged=Image.composite(repair,original,mask)
  regions[n].paste(merged.crop((0,0,384,1920)),(1776,0));regions[n+1].paste(merged.crop((384,0,768,1920)),(0,0));out=DOC/'seams';out.mkdir(exist_ok=True);merged.save(out/f'seam_{n}.jpg',quality=93,optimize=True,subsampling=0)
  old=np.array(original).astype(float);new=np.array(merged).astype(float);reports.append({'boundaryX':n*2160,'sourceSize':Image.open(src['path']).size,'beforeMeanEdgeDelta':float(abs(old[:,383]-old[:,384]).mean()),'afterMeanEdgeDelta':float(abs(new[:,383]-new[:,384]).mean()),'editableBand':[n*2160-192,n*2160+192],'method':'imagegen repair; fixed feather mask; all pixels outside band unchanged before JPG encoding'});prompts[f'seam_{n}']=src['prompt']
 manifest=read(DOC/'assets.json')
 for n,im in regions.items():
  path=ART/f'region_{n}.jpg';im.save(path,quality=92,optimize=True,subsampling=0);meta(path,maxsize=4096);key=path.relative_to(ROOT).as_posix();manifest[key]['bytes']=path.stat().st_size;manifest[key]['sha256']=hashlib.sha256(path.read_bytes()).hexdigest();manifest[key]['seamRepair']=True
 write(DOC/'seams/validation.json',reports);write(DOC/'prompts.json',prompts);write(DOC/'assets.json',manifest)
if __name__=='__main__':main()
