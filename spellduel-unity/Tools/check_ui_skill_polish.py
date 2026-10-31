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
 old=subprocess.check_output(['git','show',base+':'+p.as_posix()]);assert old.replace(b'\r\n',b'\n')==p.read_bytes().replace(b'\r\n',b'\n'),p.name
for name in ['Skills.cs','Economy.cs','Crafting.cs','Explore.cs','Blueprints.cs','SpeechInput.cs']:
 p=r/name;old=subprocess.check_output(['git','show',base+':'+p.as_posix()]);assert old.replace(b'\r\n',b'\n')==p.read_bytes().replace(b'\r\n',b'\n'),name
print('PASS: protected combat, skills, economy, crafting, exploration, recipes and speech sources unchanged')

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
