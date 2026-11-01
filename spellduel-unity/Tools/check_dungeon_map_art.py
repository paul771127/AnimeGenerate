"""Check the dungeon art delivery; --native requires Unity evidence."""
from pathlib import Path
import json,re,sys,subprocess
r=Path('spellduel-unity/Assets/SpellDuel/Runtime')
for p in r.glob('ExploreArt.Dungeon*.cs'):
 s=p.read_text(encoding='utf-8-sig')
 assert not re.search(r'AddComponent\s*<\s*[^>]*Collider',s),p
 assert '.AddProp(' not in s and '.cleared =' not in s and '.cleared=' not in s,p
 assert 'UnityEngine.Random' not in s,p
surfaces=(r/'ExploreArt.DungeonSurfaces.cs').read_text(encoding='utf-8')
assert 'map.GateOpen(x,z)' in surfaces
assert 'ConditionalWeakTable<ExploreMap,DungeonArtLayout>' in (r/'ExploreArt.Dungeons.cs').read_text(encoding='utf-8')
print('PASS: art has no colliders, map writes, prop obstacles, or gameplay random calls')
if '--native' in sys.argv:
 from PIL import Image
 root=Path('spellduel-unity/Docs/pending_preview/dungeon_maps')
 audit=json.loads((root/'dungeon_maps_audit.json').read_text())
 assert audit['cellSize']==4 and audit['noColliders'] and audit['wallBounds'] and audit['gateAnimation']
 assert len(audit['views'])==6 and len(audit['maps'])>=7
 for row in audit['maps']:
  assert row['deterministic'] and row['unchanged'] and row['cache'] and row['baselineUnreachable']==0,row
 for row in audit['views']:
  assert row['cells']<=169,row
  assert Image.open(root/('dungeon_'+row['theme']+'_top.png')).size==(1400,1400)
  assert Image.open(root/('dungeon_'+row['theme']+'_first.png')).size==(1080,1440)
 if '--require-mobile' in sys.argv:
  ci=json.loads((root/'platform_ci.json').read_text())
  jobs={j['name']:j['conclusion'] for j in ci['jobs']}
  assert all(jobs.get(k)=='success' for k in ['android','ios-export','ios-ipa']),jobs
  native=json.loads((root/'native_ci.json').read_text())
  assert native['conclusion']=='success' and native['head_sha']==ci['head'], 'Native/mobile revisions differ'
  for relative in [
   'Runtime/ExploreArt.Dungeons.cs','Runtime/ExploreArt.DungeonSurfaces.cs',
   'Runtime/ExploreArt.Paths.cs','Runtime/ExploreArt.Atmosphere.cs','Runtime/ExploreArt.Landmarks.cs',
   'Runtime/SoloBattle.Explore.cs','Resources/Shaders/DungeonExploreFloor.shader','Editor/DungeonMapArtPreview.cs']:
   path='spellduel-unity/Assets/SpellDuel/'+relative
   expected=subprocess.check_output(['git','rev-parse',ci['head']+':'+path],text=True).strip()
   actual=subprocess.check_output(['git','hash-object',path],text=True).strip()
   assert actual==expected, 'Art changed since native/mobile validation: '+path
 print('PASS: native themes, unchanged routes, cache, bounds, gates and mobile evidence')
