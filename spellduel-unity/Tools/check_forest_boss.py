"""Validate FF action atlas contract and preserve legacy fallback/runtime files."""
from pathlib import Path
import hashlib,json,re,subprocess,sys
from PIL import Image
import numpy as np
ROOT=Path(__file__).resolve().parents[1];REPO=ROOT.parent
OUT=ROOT/'Assets/SpellDuel/Resources/SpriteEnemies'
PREVIEW=ROOT/'Docs/castle_2d_preview/boss'
IDS=['castle_boss']
ACTIONS={'idle':4,'walk':6,'windup':2,'attack':3,'hurt':2,'die':4,'cleave_windup':2,'cleave':3,'wave_windup':2,'wave':3,'cast':3}
errors=[];guids=set();total=0;cells=0;heights={}
def check(ok,text):
 if not ok:errors.append(text)
def sha(b):return hashlib.sha256(b).hexdigest()
manifest=json.loads((PREVIEW/'atlas_manifest.json').read_text())
for name in IDS:
 for action,rows in ACTIONS.items():
  p=OUT/f'{name}_{action}.png';check(p.exists(),f'missing {p.name}')
  if not p.exists():continue
  total+=p.stat().st_size;im=Image.open(p);check(im.size==(2048,rows*256),f'{p.name} dimensions {im.size}');check(im.mode in ('RGBA','P'),f'{p.name} no alpha mode')
  check(sha(p.read_bytes())==manifest['enemies'][name]['files'][action]['sha256'],f'{p.name} stale manifest')
  im=im.convert('RGBA');hs=[]
  for d in range(8):
   hashes=[]
   for r in range(rows):
    c=im.crop((d*256,r*256,(d+1)*256,(r+1)*256));a=np.array(c.getchannel('A'));mask=a>=16;y,x=np.where(mask);cells+=1
    check(len(x)>100,f'{p.name} r{r} d{d} empty')
    if not len(x):continue
    check(x.min()>=5 and x.max()<=250 and y.min()>=5 and y.max()<=242,f'{p.name} r{r} d{d} touches margin')
    check(not a[:4].any() and not a[-8:].any() and not a[:,:4].any() and not a[:,-4:].any(),f'{p.name} r{r} d{d} alpha spill')
    check(mask.mean()<.68,f'{p.name} r{r} d{d} probable background')
    hashes.append(sha(c.tobytes()))
    if r==rows-1:hs.append(int(y.max()-y.min()+1))
   check(len(set(hashes))==rows,f'{p.name} d{d} duplicate poses')
  heights[name,action]=hs
  meta=Path(str(p)+'.meta').read_text();g=re.search(r'guid: ([0-9a-f]{32})',meta)
  check(g is not None and g[1] not in guids,f'{p.name} GUID');guids.add(g[1] if g else '')
  for key,value in {'enableMipMap':0,'maxTextureSize':2048,'filterMode':1,'wrapU':1,'wrapV':1,'wrapW':1,'nPOTScale':0,'textureType':0,'alphaIsTransparency':1,'sRGBTexture':1,'textureCompression':1,'compressionQuality':50}.items():check(re.search(r'^ *'+key+r': '+str(value)+r'$',meta,re.M),f'{p.name} importer {key}')
 old=OUT/'castle_knight.png';baseline=subprocess.check_output(['git','show',manifest['baseline']+':'+old.relative_to(REPO).as_posix()],cwd=REPO)
 check(sha(old.read_bytes())==sha(baseline),f'{name} legacy fallback modified')
 for suffix in ('directions.jpg','walk.gif','attack.gif','all_frames.jpg'):check((PREVIEW/f'{name}_{suffix}').exists(),f'{name} missing preview {suffix}')
 for d,(idle,die) in enumerate(zip(heights[name,'idle'],heights[name,'die'])):check(die<idle*.72,f'{name} d{d} final death still too tall {die}/{idle}')
# Art-only task: do not mix gameplay or other unrequested Runtime edits.
changed=subprocess.check_output(['git','diff','--name-only','--','spellduel-unity/Assets/SpellDuel/Runtime'],cwd=REPO,text=True).strip()
check(not changed,'Runtime changed: '+changed)
result={'errors':errors,'atlases':len(guids),'cells':cells,'pngMiB':round(total/1048576,2),'runtimeUnchanged':not changed,'legacyFallbackUnchanged':not any('fallback' in x for x in errors)}
(PREVIEW/'validation.json').write_text(json.dumps(result,indent=2))
print(json.dumps(result,indent=2));sys.exit(bool(errors))
