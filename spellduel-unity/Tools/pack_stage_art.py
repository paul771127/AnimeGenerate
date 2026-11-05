"""Pack generated stage paintings into runtime texture/atlas formats.
Only production operations: resize, periodic edge blend, alpha-safe atlas packing.
Usage: python Tools/pack_stage_art.py path/to/source_manifest.json
"""
from pathlib import Path
import json,re,uuid,sys
import numpy as np
from PIL import Image
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Assets/SpellDuel/Resources/StageArt'
DOC=ROOT/'Docs/stage_art_preview/volcano'
SIZES={'far':(4096,512),'mid':(4096,384),'ground':(1024,1024),'decals':(2048,2048),'props':(2048,2048)}
def resize(im,size):
 return im.convert('RGBa').resize(size,Image.Resampling.LANCZOS).convert('RGBA') if im.mode=='RGBA' else im.resize(size,Image.Resampling.LANCZOS)
def periodic(im,axis,band):
 # Couple the two outer edges with a smooth cosine ramp; interior stays untouched.
 a=np.asarray(im).astype(float)
 if axis==0:a=np.swapaxes(a,0,1)
 original=a.copy()
 for k in range(band):
  t=(1+np.cos(np.pi*k/band))*.25
  a[:,k]=original[:,k]*(1-t)+original[:,-1-k]*t
  a[:,-1-k]=original[:,-1-k]*(1-t)+original[:,k]*t
 if axis==0:a=np.swapaxes(a,0,1)
 return Image.fromarray(np.uint8(np.clip(np.rint(a),0,255)),im.mode)
def clean(im):
 a=np.array(im.convert('RGBA'));a[a[:,:,3]<=12]=0
 return Image.fromarray(a)
def pack_atlas(im,layer):
 out=Image.new('RGBA',(2048,2048))
 for row in range(4):
  for col in range(4):
   cell=clean(im.crop((round(col*im.width/4),round(row*im.height/4),round((col+1)*im.width/4),round((row+1)*im.height/4))))
   box=cell.getchannel('A').getbbox()
   if not box:raise ValueError('Empty atlas cell')
   cell=cell.crop(box)
   # Keep relative source prop heights; fit cell safely, align all prop bases.
   factor=min(448/cell.width,448/cell.height,512/(im.width/4))
   cell=resize(cell,(round(cell.width*factor),round(cell.height*factor)))
   x=col*512+(512-cell.width)//2
   y=row*512+(491-cell.height if layer=='props' else (512-cell.height)//2)
   out.alpha_composite(cell,(x,y))
 return clean(out)
def main():
 OUT.mkdir(parents=True,exist_ok=True);DOC.mkdir(parents=True,exist_ok=True)
 records=json.loads(Path(sys.argv[1]).read_text(encoding='utf-8-sig'))
 template=(ROOT/'Assets/SpellDuel/Resources/CastleScene/props.png.meta').read_text()
 prompts=[]
 for rec in records:
  v,layer=rec['v'],rec['layer'];im=Image.open(rec['source'])
  if layer in ('props','decals'):im=pack_atlas(im,layer)
  else:
   if layer=='mid':
    # Preserve architectural proportions instead of stretching a 3:1 painting.
    strip=clean(resize(im,(round(im.width*384/im.height),384)))
    pixels=np.array(strip)
    fade=np.minimum(1,np.minimum(np.arange(strip.width),np.arange(strip.width)[::-1])/40)
    pixels[:,:,3]=np.uint8(pixels[:,:,3]*fade[None,:])
    strip=Image.fromarray(pixels)
    im=Image.new('RGBA',SIZES[layer])
    for offset in (96,1480,2832):im.alpha_composite(strip,(offset,0))
   else:im=resize(im,SIZES[layer])
   im=periodic(im,1,128 if layer!='ground' else 48)
   if layer=='ground':im=periodic(im,0,48)
  dest=OUT/f'volcano_{v}_{layer}.png';im.save(dest,optimize=True)
  meta=dest.with_suffix('.png.meta')
  old=meta.read_text() if meta.exists() else ''
  guid=re.search(r'guid: (\w+)',old)
  text=re.sub(r'guid: \w+','guid: '+(guid[1] if guid else uuid.uuid4().hex),template)
  maximum=4096 if layer in ('far','mid') else 2048
  text=text.replace('maxTextureSize: 2048',f'maxTextureSize: {maximum}')
  if layer in ('far','mid','ground'):
   for axis in 'UVW':text=text.replace(f'wrap{axis}: 1',f'wrap{axis}: 0')
  meta.write_text(text,encoding='utf-8')
  if layer=='far':im.save(DOC/f'volcano_{v}_far_strip.png',optimize=True)
  prompts.append({'file':dest.name,'prompt':rec['prompt'],'mode':'built-in image_gen','source_size':Image.open(rec['source']).size})
 (DOC/'generation_manifest.json').write_text(json.dumps(prompts,ensure_ascii=False,indent=2),encoding='utf-8')
if __name__=='__main__':main()
