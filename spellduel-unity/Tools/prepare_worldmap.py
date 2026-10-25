"""Package imagegen world-map sources, preserving alpha and recording provenance."""
import argparse,json,re,uuid,hashlib
from pathlib import Path
from PIL import Image,ImageOps
ROOT=Path(__file__).resolve().parents[1];ART=ROOT/'Assets/SpellDuel/Resources/WorldMap';DOC=ROOT/'Docs/worldmap';SOURCE=ROOT.parents[1]
def read(p):return json.loads(Path(p).read_text(encoding='utf-8-sig'))
def write(p,v):p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf-8')
def sources(pattern):
 out={}
 for p in SOURCE.glob(pattern):out.update(read(p))
 return out
def meta(p,alpha=False,maxsize=2048,border=None):
 m=Path(str(p)+'.meta')
 if m.exists():return
 s='fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n'
 if p.suffix in ['.json','.txt']:s+='TextScriptImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n'
 else:
  s+='TextureImporter:\n  externalObjects: {}\n  serializedVersion: 12\n  mipmaps:\n    enableMipMap: 0\n  nPOTScale: 0\n  maxTextureSize: '+str(maxsize)+'\n  textureType: '+('8' if border else '0')+'\n  alphaSource: '+('1' if alpha else '0')+'\n  sRGBTexture: 1\n'
  if border:s+='  spriteMode: 1\n  spritePixelsToUnits: 100\n  spritePivot: {x: 0.5, y: 0.5}\n  spriteBorder: {x: %d, y: %d, z: %d, w: %d}\n'%tuple(border)
 m.write_text(s,encoding='utf-8')
def record(p,s,manifest):
 im=Image.open(p);manifest[p.relative_to(ROOT).as_posix()]={'size':im.size,'mode':im.mode,'sourceSize':Image.open(s['path']).size,'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()}
def package(data):
 ART.mkdir(parents=True,exist_ok=True);DOC.mkdir(parents=True,exist_ok=True);manifest=read(DOC/'assets.json') if (DOC/'assets.json').exists() else {};prompts=read(DOC/'prompts.json') if (DOC/'prompts.json').exists() else {}
 for key,s in data.items():
  im=Image.open(s['path']).convert('RGBA');border=None
  if key.startswith('region_'):
   size=(2160,1920);im=ImageOps.fit(im,size,method=Image.Resampling.LANCZOS).convert('RGB');p=ART/(key+'.jpg');im.save(p,quality=92,optimize=True,subsampling=0);meta(p,maxsize=4096)
  else:
   if key.startswith('hero_'):size=(192,256)
   elif key.startswith('fog_'):size=(1024,1024)
   elif key.startswith('stars_'):size=(96,32)
   elif key=='label_plate':size=(512,128);border=[64,32,64,32]
   else:size=(160,160) if any(k in key for k in ['boss','town','tower']) else (128,128)
   if key.startswith('fog_'):im=im.resize(size,Image.Resampling.LANCZOS)
   else:
    bbox=im.getchannel('A').point(lambda v:255 if v>32 else 0).getbbox();im=im.crop(bbox)
    target=(184,232) if key.startswith('hero_') else (size[0]-4,size[1]-4);small=ImageOps.contain(im,target,Image.Resampling.LANCZOS);im=Image.new('RGBA',size);im.alpha_composite(small,((size[0]-small.width)//2,(252-small.height) if key.startswith('hero_') else (size[1]-small.height)//2))
   p=(ART if key.startswith('hero_') else ART/'ui')/(key+'.png');p.parent.mkdir(exist_ok=True);im.save(p,optimize=True);meta(p,True,border=border)
  record(p,s,manifest);prompts[key]=s['prompt'];print(key,flush=True)
 write(DOC/'assets.json',manifest);write(DOC/'prompts.json',prompts)
def main():
 p=argparse.ArgumentParser();p.add_argument('--region',nargs='*',type=int);p.add_argument('--class',dest='classes',nargs='*');p.add_argument('--ui',action='store_true');a=p.parse_args();data={}
 if a.region:
  for n in a.region:data.update(read(SOURCE/f'worldmap_region_{n}.json'))
 if a.classes:
  for c in a.classes:data.update(sources(f'worldmap_hero_{c}_*.json'))
 if a.ui:data.update(sources('worldmap_ui_*.json'))
 package(data)
if __name__=='__main__':main()
