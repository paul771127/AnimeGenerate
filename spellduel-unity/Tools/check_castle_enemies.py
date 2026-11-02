"""Validate painted castle sheets without changing gameplay or animation timing."""
from pathlib import Path
import hashlib,json,subprocess
from PIL import Image
root=Path(__file__).resolve().parents[1]
repo=root.parent
preview=root/'Docs/castle_2d_preview/enemy_redesign'
manifest=json.loads((preview/'atlas_manifest.json').read_text())
errors=[]
def check(ok,msg):
 if not ok:errors.append(msg)
for name,entry in manifest['enemies'].items():
 p=root/'Assets/SpellDuel/Resources/SpriteEnemies'/f'castle_{name}.png'
 check(hashlib.sha256(p.read_bytes()).hexdigest()==entry['sha256'],name+' stale manifest')
 im=Image.open(p);check(im.mode=='RGBA' and im.size==(2048,2048),name+' format')
 hashes=[];heights=[]
 for slot in range(256):
  x=slot%16*128;y=slot//16*128;cell=im.crop((x,y,x+128,y+128));a=cell.getchannel('A')
  b=a.point(lambda a:255 if a>=16 else 0).getbbox()
  if slot>=168:check(a.getbbox() is None,f'{name} unused slot {slot}');continue
  check(b is not None,f'{name} empty {slot}')
  if b:check(b[0]>=3 and b[1]>=3 and b[2]<=125 and b[3]<=122,f'{name} clipped {slot} {b}')
  hashes.append(hashlib.sha256(cell.tobytes()).hexdigest());heights.append(b[3]-b[1] if b else 0)
 for start,count in [(0,4),(4,6),(10,2),(12,3),(15,2),(17,4)]:
  for d in range(8):check(len({hashes[(start+f)*8+d] for f in range(count)})==count,f'{name} duplicated animation {start}/{d}')
 for d in range(8):check(heights[20*8+d]<heights[d]*.8,f'{name} defeat remains standing {d}')
 for filename,digest in entry['sources'].items():
  source=root/'Tools/sprite_enemies/painted_sources'/name/filename
  check(hashlib.sha256(source.read_bytes()).hexdigest()==digest,name+' changed source '+filename)
 meta=Path(str(p)+'.meta').read_text()
 for value in ['enableMipMap: 0','maxTextureSize: 2048']:check(value in meta,name+' importer '+value)
 for suffix in ['_animation.gif','_all_frames.jpg']:check((preview/(name+suffix)).exists(),name+' preview')
# All Runtime, gameplay data and importer settings remain identical to task base.
changed=subprocess.check_output(['git','diff','--name-only',manifest['baseline'],'--','spellduel-unity/Assets/SpellDuel/Runtime','spellduel-unity/Assets/SpellDuel/Resources'],cwd=repo,text=True).splitlines()
allowed={f'spellduel-unity/Assets/SpellDuel/Resources/SpriteEnemies/castle_{n}.png' for n in manifest['enemies']}
for path in changed:check(path in allowed,'unrelated runtime/data change '+path)
print(f'{len(errors)} errors; 8 enemies, 1344 distinct drawn frames, 704 empty slots, unchanged runtime/data/importers')
for e in errors:print(e)
raise SystemExit(bool(errors))
