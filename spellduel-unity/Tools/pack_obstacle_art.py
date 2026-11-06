"""Pack painted obstacle assets; no runtime or collision changes.
Usage: python Tools/pack_obstacle_art.py SOURCE_MANIFEST
"""
from pathlib import Path
import json,re,uuid,sys
import numpy as np
from PIL import Image
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Assets/SpellDuel/Resources/StageArt/Obstacles'
DOC=ROOT/'Docs/stage_art_preview/obstacles'
def clean(im):
 a=np.array(im.convert('RGBA'));a[a[:,:,3]<=12]=0
 return Image.fromarray(a)
def resize(im,size):
 return im.convert('RGBa').resize(size,Image.Resampling.LANCZOS).convert('RGBA')
def fit(im,size,bottom,margin=.04,maxheight=None):
 im=clean(im);im=im.crop(im.getchannel('A').getbbox())
 w,h=size;factor=min((w*(1-margin*2))/im.width,(maxheight or h*(1-margin*2))/im.height)
 im=resize(im,(max(1,round(im.width*factor)),max(1,round(im.height*factor))))
 out=Image.new('RGBA',size);out.alpha_composite(im,((w-im.width)//2,bottom-im.height))
 return clean(out)
def main():
 records=json.loads(Path(sys.argv[1]).read_text(encoding='utf-8-sig'));OUT.mkdir(parents=True,exist_ok=True);DOC.mkdir(parents=True,exist_ok=True)
 template=(ROOT/'Assets/SpellDuel/Resources/StageArt/volcano_0_props.png.meta').read_text()
 notes=[]
 for r in records:
  name=r['name'];im=Image.open(r['source']);source_size=im.size
  if '_jump_' in name:im=fit(im,(2048,512),492,.025,210)
  elif '_duck_' in name:
   im=clean(im);a=np.array(im)[:,:,3]
   columns=range(int(im.width*.35),int(im.width*.65))
   beam=round(float(np.median([np.flatnonzero(a[:,x]>100)[-1]+1 for x in columns])))
   base=im.getchannel('A').getbbox()[3]
   # Map painted beam underside to y=55%, base to y=96%.
   result=Image.new('RGBA',(2048,1024))
   for y0,y1,t0,t1 in [(0,beam,0,563),(beam,base,563,983),(base,im.height,983,1024)]:
    if y1>y0:result.alpha_composite(resize(im.crop((0,y0,im.width,y1)),(2008,t1-t0)),(20,t0))
   im=clean(result)
  elif '_side_' in name:im=fit(im,(1024,1024),983,.04,920)
  elif name=='volcano_vent_erupt':
   result=Image.new('RGBA',(1024,1024))
   for k in range(4):
    cell=clean(im.crop((round(k*im.width/4),0,round((k+1)*im.width/4),im.height)))
    # Preserve the temporal heights relative to shared full canvas, not each bounds.
    cell=clean(resize(cell,(256,1024)))
    b=cell.getchannel('A').getbbox()
    if b and b[3]!=1024:
     shifted=Image.new('RGBA',cell.size);shifted.alpha_composite(cell,(0,1024-b[3]));cell=shifted
    result.alpha_composite(cell,(256*k,0))
   im=clean(result)
  elif name=='exit_pillar':im=im.convert('RGB').resize((256,1024),Image.Resampling.LANCZOS)
  elif name.startswith('volcano_vent_'):im=clean(resize(im,(512,512)))
  else:
   n=1024 if name=='exit_circle' else 512
   im=fit(im,(n,n),round(n*.94),.06,round(n*.88))
  dest=OUT/(name+'.png');im.save(dest,optimize=True)
  meta=dest.with_suffix('.png.meta')
  prior=meta.read_text() if meta.exists() else ''
  guid=re.search(r'guid: (\w+)',prior)
  text=re.sub(r'guid: \w+','guid: '+(guid[1] if guid else uuid.uuid4().hex),template)
  meta.write_text(text,encoding='utf-8')
  notes.append({'file':dest.name,'source_size':source_size,'output_size':im.size,'tool':'built-in image_gen','prompt':r['prompt']})
 (DOC/'generation_manifest.json').write_text(json.dumps(notes,ensure_ascii=False,indent=2),encoding='utf-8')
if __name__=='__main__':main()
