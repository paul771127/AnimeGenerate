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
changed=subprocess.check_output(['git','diff','--name-only'],text=True).splitlines()
for f in changed:
 if '/Runtime/SoloBattle' not in f:continue
 old=subprocess.check_output(['git','show',base+':'+f]).decode('utf-8-sig');new=Path(f).read_text(encoding='utf-8-sig')
 for name in ['GUI.Button','UiKit.Button','UiKit.NextTag']:
  assert calls(old,name)==calls(new,name),(f,name,'interaction expression changed')
for f in ['UiLock.cs','SoloBattle.Walkthrough.cs','SoloBattle.Dash.cs']:
 old=subprocess.check_output(['git','show',base+':'+str(r/f).replace('\\','/')]);assert old.replace(b'\r\n',b'\n')==(r/f).read_bytes().replace(b'\r\n',b'\n'),f
helper=(r/'UiKit.BattleArt.cs').read_text(encoding='utf-8')
assert not re.search(r'new\s+(Texture2D|Material)\s*\(',helper)
assert helper.count('class ResourceTrail')==1
print('PASS: button/tag expressions unchanged; tutorial lock and dash unchanged; HUD art allocates no textures/materials')

