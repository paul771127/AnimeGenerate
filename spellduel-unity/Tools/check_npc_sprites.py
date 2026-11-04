"""Validate NPC atlas delivery. --partial checks finished batches only."""
import sys,json,re,hashlib
from pathlib import Path
from PIL import Image
import numpy as np
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'Tools/sprite_npcs'))
from pack_npcs import IDS,GROUPS,OUT,PREVIEW
def main():
 errors=[];guids=set();count=0
 manifest=json.loads((PREVIEW/'atlas_manifest.json').read_text())['npcs']
 names=list(manifest) if '--partial' in sys.argv else IDS
 for name in names:
  if name not in manifest:errors.append(name+': missing');continue
  for action,rows in GROUPS.items():
   path=OUT/f'{name}_{action}.png'
   if not path.exists():errors.append(str(path)+': missing');continue
   im=Image.open(path)
   if im.mode!='RGBA' or im.size!=(2048,rows*256):errors.append(name+action+': format')
   if hashlib.sha256(path.read_bytes()).hexdigest()!=manifest[name]['files'][action]['sha256']:errors.append(name+action+': hash')
   seen=set()
   for r in range(rows):
    for d in range(8):
     c=im.crop((d*256,r*256,(d+1)*256,(r+1)*256));a=np.array(c.getchannel('A'));ys,xs=np.where(a>=16)
     if not len(xs):errors.append(f'{name}/{action}/{r}/{d}: empty');continue
     if xs.min()<5 or xs.max()>250 or ys.min()<5 or ys.max()>242:errors.append(f'{name}/{action}/{r}/{d}: edge')
     if (a>=16).mean()>.65:errors.append(name+action+': opaque background')
     seen.add(hashlib.sha256(c.tobytes()).hexdigest());count+=1
   if len(seen)!=rows*8:errors.append(name+action+': duplicate frames')
   meta=Path(str(path)+'.meta').read_text();guid=re.search(r'guid: ([0-9a-f]{32})',meta).group(1)
   if guid in guids:errors.append(name+action+': duplicate guid')
   guids.add(guid)
   for key,val in {'enableMipMap':0,'maxTextureSize':2048,'filterMode':1,'wrapU':1,'wrapV':1,'textureType':0,'alphaIsTransparency':1,'sRGBTexture':1,'textureCompression':1,'compressionQuality':50}.items():
    if not re.search(r'\b'+key+r': '+str(val)+r'\b',meta):errors.append(name+action+': '+key)
  for suffix in ('directions.jpg','walk.gif','gesture.gif','comparison.jpg'):
   if not (PREVIEW/f'{name}_{suffix}').exists():errors.append(name+': missing '+suffix)
  if manifest[name]['source_edge_contacts']:errors.append(name+': source edge contact')
 print(f'{len(names)} NPCs, {count} cells, {len(errors)} errors')
 for e in errors:print(e)
 return bool(errors)
if __name__=='__main__':sys.exit(main())
