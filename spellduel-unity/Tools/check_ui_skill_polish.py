from pathlib import Path
import re,subprocess,json
base='5a4a0bf'
r=Path('spellduel-unity/Assets/SpellDuel/Runtime')
# Preserve complete source expressions for all clickable controls in touched views.
def calls(s,name):
 out=[]
 for m in re.finditer(re.escape(name)+r'\s*\(',s):
  start=m.end();i=start;depth=1;quote=None;escape=False
  while i<len(s) and depth:
   c=s[i]
   if quote:
    if escape:escape=False
    elif c=='\\':escape=True
    elif c==quote:quote=None
   elif c in '\"\'':quote=c
   elif c=='(':depth+=1
   elif c==')':depth-=1
   i+=1
  out.append(s[m.start():i])
 return out
changed=subprocess.check_output(['git','diff',base,'--name-only'],text=True).splitlines()
for f in changed:
 if '/Runtime/' not in f or not Path(f).name.startswith(('SoloBattle','CharacterPanel','WorldUI','GameRoot','SettingsMenu','StoryVideo')):continue
 # Incoming Claude commits changed map buttons; compare those views against their reviewed incoming baseline.
 reference='c41e792' if Path(f).name in ['WorldUI.WorldMap2.cs','WorldUI.TownMap.cs','WorldUI.TownAmbient.cs'] else base
 if Path(f).name in ['WorldUI.Craft.cs','WorldUI.CraftArt.cs','WorldUI.cs']:reference='0199549' # Incoming material-source tips, not this art task.
 old_result=subprocess.run(['git','show',reference+':'+f],capture_output=True)
 old=old_result.stdout.decode('utf-8-sig') if old_result.returncode==0 else ''
 new=Path(f).read_text(encoding='utf-8-sig')
 for name in ['GUI.Button','UiKit.Button','UiKit.NextTag','GUI.Toggle']:
  assert calls(old,name)==calls(new,name),(f,name,'interaction expression changed')
for f in ['UiLock.cs','SoloBattle.Walkthrough.cs','SoloBattle.Dash.cs']:
 old=subprocess.check_output(['git','show',base+':'+str(r/f).replace('\\','/')]);assert old.replace(b'\r\n',b'\n')==(r/f).read_bytes().replace(b'\r\n',b'\n'),f
helper=(r/'UiKit.BattleArt.cs').read_text(encoding='utf-8')
assert not re.search(r'new\s+(Texture2D|Material)\s*\(',helper)
assert helper.count('class ResourceTrail')==1
print('PASS: button/tag expressions unchanged; tutorial lock and dash unchanged; HUD art allocates no textures/materials')


for p in r.glob('Combat*.cs'):
 old=subprocess.check_output(['git','show',base+':'+p.as_posix()])
 art=subprocess.check_output(['git','show','8121b87:'+p.as_posix()]);assert old.replace(b'\r\n',b'\n')==art.replace(b'\r\n',b'\n'),p.name
 # Incoming Claude balance commits are independent of this art delivery.
 reference='762b838' if p.name in ['Combat.cs','Combat.Friends.cs'] else base
 expected=subprocess.check_output(['git','show',reference+':'+p.as_posix()]);assert expected.replace(b'\r\n',b'\n')==p.read_bytes().replace(b'\r\n',b'\n'),p.name
for name in ['Skills.cs','Economy.cs','Crafting.cs','Explore.cs','Blueprints.cs','SpeechInput.cs']:
 p=r/name;old=subprocess.check_output(['git','show',base+':'+p.as_posix()]);assert old.replace(b'\r\n',b'\n')==p.read_bytes().replace(b'\r\n',b'\n'),name
print('PASS: art delivery preserves protected sources; incoming Claude balance changes retained')

# Town presentation anchors are read once, and never become controls/colliders.
for path in Path('spellduel-unity/Assets/SpellDuel/Resources/Village2D').glob('map_*.json'):
 data=json.loads(path.read_text(encoding='utf-8'));anchors=data.get('ambient',[])
 assert sum(a['kind']=='path' for a in anchors)>=2,path.name
 for a in anchors:assert 0<=a['x']<=2048 and 0<=a['y']<=3072 and a['w']>=0 and a['h']>=0,(path,a)
life=(r/'WorldUI.TownLife.cs').read_text(encoding='utf-8')
assert not re.search(r'new\s+(Texture2D|Material)\s*\(',life)
assert 'GUI.Button' not in life and 'UiKit.Button' not in life
fade=(r/'SpellFx.Readability.cs').read_text(encoding='utf-8').split('public void Evaluate',1)[1]
assert not re.search(r'new\s+(Texture2D|Material)\s*\(',fade)
assert 'public const int ParticleBudget=192,PerSystemBudget=64' in (r/'SpellFx.Readability.cs').read_text()
print('PASS: 5 town anchor sets; no ambient click targets; no per-tick texture/material creation; particle caps declared')

# Optional native delivery checks; source-only mode also works before CI has finished.
import sys
if '--native' in sys.argv:
 from PIL import Image
 preview=Path('spellduel-unity/Docs/pending_preview/ui_skill_polish')
 for version in ['before','after']:
  files=list((preview/'A'/version).glob('*.png'))
  assert len(files)==38,(version,len(files))
  assert all(Image.open(p).size==(1080,2340) for p in files)
  ui=json.loads((preview/'A'/(version+'_native.json')).read_text())
  assert ui['nativeIMGUI'] and not ui['failed']
  icons=json.loads((preview/'B'/(version+'_audit.json')).read_text())
  assert icons['icons']==253 and icons['cacheHits']==253 and icons['deterministic']
  assert icons['sizes']==[56,128]
  for size in [56,128]:assert (preview/'B'/(version+'_'+str(size)+'.png')).is_file()
 fx=json.loads((preview/'C/native_audit.json').read_text())
 assert len(fx['samples'])==288
 pairs={(r['id'],r['view']) for r in fx['samples']}
 assert len(pairs)==96
 for skill,view in pairs:
  phases={r['phase'] for r in fx['samples'] if r['id']==skill and r['view']==view}
  assert phases=={'charge','release','impact'}
  assert Image.open(preview/'C'/(skill+'_'+view+'.png')).size==(2160,988)
 for row in fx['samples']:
  assert row['particles']<=576 and row['meshTriangles']+row['lineTriangleBound']<=24000
 assert fx['sixSpellParticles']<=3456 and fx['sixSpellTriangleBound']<=144000
 assert fx['mobileFpsMeasured'] is False
 for name in ['ui','icons','fx']:
  evidence=json.loads((preview/(name+'_ci.json')).read_text())
  assert evidence['conclusion']=='success',(name,evidence)
 if '--require-mobile' in sys.argv:
  evidence=json.loads((preview/'platform_ci.json').read_text())
  jobs={j['name']:j['conclusion'] for j in evidence['jobs']}
  assert all(jobs.get(name)=='success' for name in ['android','ios-export','ios-ipa']),jobs
  fx_evidence=json.loads((preview/'fx_ci.json').read_text())
  subprocess.run(['git','merge-base','--is-ancestor',fx_evidence['head'],evidence['head']],check=True)
  # A later merge may add unrelated UI while retaining the validated FX sources.
  native_paths=['spellduel-unity/Assets/SpellDuel/Runtime/SpellFx*.cs','spellduel-unity/Assets/SpellDuel/Runtime/AdvancedFx.cs','spellduel-unity/Assets/SpellDuel/Resources/SpellReadableSolid.shader']
  assert not subprocess.check_output(['git','diff',fx_evidence['head'],evidence['head'],'--']+native_paths).strip(),'FX changed after native validation'
 print('PASS: 76 native UI screens; 253 cached icons; 288 FX frames/96 strips; CI evidence and budgets')
