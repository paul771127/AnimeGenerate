"""Validate the gallery content and optional native Unity audit evidence."""
import argparse,json,subprocess,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
p=argparse.ArgumentParser();p.add_argument('--require-native',action='store_true');args=p.parse_args()
subprocess.run([sys.executable,str(ROOT/'Tools/check_dungeons.py')],check=True)
text=(ROOT/'Assets/SpellDuel/Resources/dungeons.txt').read_text(encoding='utf-8')
new=text.split('[dungeon thousand_trap_gallery]',1)[1]
for key in ['time_limit = 25','traps = on','unlock = 4-6']:
 assert key in new,key
for name in ['lingyin','ark','wheat','oldsmith','merchant']:
 assert '[npc '+name+']' in new,name
src=(ROOT/'Assets/SpellDuel/Runtime/Combat.Traps.cs').read_text(encoding='utf-8')
assert 'source==0' in src and 'warn=3.5f' in src
native=ROOT/'Docs/trap_dungeon_preview';report=native/'trap_dungeon_audit.json'
if report.exists():
 audit=json.loads(report.read_text(encoding='utf-8'));assert audit['paths']==2 and audit['combatCases']>=65
 assert all(audit[k] for k in ['clock','save','oldSave','exclusive','optionalEscort','lever','hostClientRoutes'])
 assert audit['lootSamples']==10000 and 220<audit['mythic']<380 and 2800<audit['setPieces']<3200
 expected=['trap_'+k+'_'+state+'.png' for k in ['Rock','Flame','Arrows','Log'] for state in ['warn','impact']]
 expected+=['vault_enemy_'+str(k)+'_phase1.png' for k in range(48,53)]
 expected+=['vault_enemy_53_phase'+str(k)+'.png' for k in range(1,4)]+['vault_mythic_card.png']
 for name in expected:
  path=native/name;assert path.exists(),name
  data=path.read_bytes();assert data[:8]==b'\x89PNG\r\n\x1a\n',name
  size=(int.from_bytes(data[16:20],'big'),int.from_bytes(data[20:24],'big'))
  required=(900,800) if name.startswith('trap_') else (768,960) if name=='vault_mythic_card.png' else (800,900)
  assert size==required,(name,size,required)
 print('Native Unity gallery audit and 17 requested previews passed.')
elif args.require_native:raise SystemExit('Native Unity evidence is missing; run Snapshot.Render in Unity first.')
else:print('Content passed; native Unity evidence not present yet.')
