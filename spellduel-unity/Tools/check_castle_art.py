"""Validate castle assets; Unity performs geometry, route and cache checks separately."""
from pathlib import Path
import json, subprocess
from PIL import Image
root=Path(__file__).resolve().parents[1]
repo=root.parent
assets=root/'Assets/SpellDuel/Resources'
errors=[]
def check(ok,msg):
 if not ok: errors.append(msg)
for name in ['skeleton','knight','archer','ghost','bat','pumpkin','gargoyle','mage']:
 p=assets/'SpriteEnemies'/('castle_'+name+'.png')
 im=Image.open(p)
 check(im.size==(2048,2048) and im.mode=='RGBA',str(p)+' format')
 for slot in range(168):
  x=slot%16*128;y=slot//16*128
  check(im.getchannel('A').crop((x,y,x+128,y+128)).getbbox() is not None,f'{name} slot {slot} empty')
 original=subprocess.check_output(['git','show','8c3b218:'+p.relative_to(repo).as_posix()],cwd=repo)
 check(original==p.read_bytes(),name+' existing animation changed')
for name in ['walls','props','surfaces']:
 p=assets/'CastleScene'/(name+'.png');im=Image.open(p)
 check(max(im.size)<=2048,name+' atlas too large')
 if name=='props':check(im.mode=='RGBA' and im.getchannel('A').getextrema()==(0,255),'props alpha')
 meta=Path(str(p)+'.meta').read_text()
 for value in ['enableMipMap: 0','maxTextureSize: 2048','nPOTScale: 0']:check(value in meta,name+' '+value)
# Presentation scope: gameplay/data/enemy animation sources stay byte-identical.
protected=['Assets/SpellDuel/Runtime/Combat*.cs','Assets/SpellDuel/Runtime/SoloBattle*.cs','Assets/SpellDuel/Runtime/Explore.cs','Assets/SpellDuel/Runtime/ExploreDungeon.cs','Assets/SpellDuel/Resources/dungeons.txt']
for pattern in protected:
 for p in root.glob(pattern):
  prior=subprocess.check_output(['git','show','8c3b218:'+p.relative_to(repo).as_posix()],cwd=repo)
  check(prior.replace(b'\r\n',b'\n')==p.read_bytes().replace(b'\r\n',b'\n'),str(p)+' protected source changed')
preview=root/'Docs/castle_2d_preview'
audit=preview/'castle_audit.json'
if audit.exists():
 data=json.loads(audit.read_text());check(data['rooms']==5 and data['theme']==6,'native rooms/theme')
 for flag in ['routes','mapUnchanged','cache','noColliders','wallBounds','gateAnimation','enclosedBackdrop']:check(data[flag],flag)
 for room in ['hall','gallery','library','vault','throne']:
  for view in ['first','top']:check((preview/f'castle_castle_{room}_{view}.png').exists(),room+' '+view)
 for name in ['castle_ceiling','castle_lower_hemisphere']:
  p=preview/(name+'.png');check(p.exists(),name)
  if p.exists():
   im=Image.open(p).convert('RGB');pixels=list(im.getdata());magenta=sum(r>230 and b>230 and g<30 for r,g,b in pixels)
   check(magenta/len(pixels)<.001,name+' exposed clear background')
 print('Native Unity evidence checked')
else:print('Native Unity evidence pending; asset validation only')
# CI may run on separate commits with identical art trees (workflow-only changes).
for filename,headkey in [('native_ci.json','head_sha'),('platform_ci.json','head')]:
 evidence=preview/filename
 if not evidence.exists():continue
 ci=json.loads(evidence.read_text());check(ci['status']=='completed' and ci['conclusion']=='success',filename+' failed/incomplete')
 head=ci[headkey]
 changed=subprocess.check_output(['git','diff','--name-only','8c3b218',head,'--','spellduel-unity/Assets/SpellDuel'],cwd=repo,text=True).splitlines()
 for rel in changed:
  local=repo/rel
  if not local.exists():check(False,rel+' missing');continue
  tested=subprocess.check_output(['git','show',head+':'+rel],cwd=repo)
  check(tested.replace(b'\r\n',b'\n')==local.read_bytes().replace(b'\r\n',b'\n'),filename+' stale '+rel)
 if filename=='platform_ci.json':
  jobs={j['name']:j for j in ci['jobs']}
  for name in ['android','ios-export','ios-ipa']:check(name in jobs and jobs[name]['conclusion']=='success',name+' compilation')
print(f'{len(errors)} errors; 8 enemies / 1344 occupied animation cells / 3 castle atlases')
for e in errors: print(e)
raise SystemExit(bool(errors))
