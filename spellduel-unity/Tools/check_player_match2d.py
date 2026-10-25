"""Check all 16 comparison cards, procedural geometry budgets and skeleton regression data."""
from pathlib import Path
import argparse,json
from PIL import Image
ROOT=Path(__file__).resolve().parents[1];DOC=ROOT/'Docs/match2d_preview'
IDS='novice swordsman archer mage assassin priest paladin berserker marksman ranger archmage summoner ninja shadowdancer bishop inquisitor'.split()
def main():
 p=argparse.ArgumentParser();p.add_argument('--require-unity',action='store_true');args=p.parse_args()
 for key in IDS:
  assert Image.open(DOC/(key+'.jpg')).size==(1080,680),key
  for view in ('front','45'):assert Image.open(DOC/'full'/(key+'_'+view+'.jpg')).size==(480,760),(key,view)
 assert Image.open(DOC/'all_16.jpg').size==(2160,1360)
 report=json.loads((DOC/'geometry_validation.json').read_text(encoding='utf-8-sig'));assert {r['id'] for r in report['classes']}==set(IDS)
 for r in report['classes']:
  assert r['ratio']<=1.5 and r['allRenderersRatio']<=1.5,(r['id'],'triangle budget')
  assert r['maxSocketLocalDelta']<=.000001 and abs(r['beforeSoleY']-r['afterSoleY'])<=.00001,(r['id'],'anchors')
 audit=json.loads((DOC/'contract_audit.json').read_text(encoding='utf-8-sig'));assert audit['failures']==0
 assert all(r['torsoMeshCached'] and r['gearVisible'] and r['crownPreserved'] and r['neckJoined'] for r in audit['classes'])
 if args.require_unity:
  ci=json.loads((DOC/'ci_validation.json').read_text(encoding='utf-8-sig'))
  assert all(ci[k]['conclusion']=='success' for k in ('android','ios-export','snapshots')),ci
  native=json.loads((DOC/'unity/native_audit.json').read_text(encoding='utf-8-sig'))
  images=json.loads((DOC/'unity/validation.json').read_text(encoding='utf-8-sig'))
  assert images['verifiedCommit']==ci['verifiedCommit']
  assert native['unityVersion'].startswith('2022.3.') and native['failures']==0
  assert {r['id'] for r in native['classes']}==set(IDS)
  assert all(r['neckJoined'] and r['torsoMeshCached'] and r['gearVisible'] and r['crownPreserved'] for r in native['classes'])
  for key in IDS:
   assert Image.open(DOC/'unity'/('match_'+key+'.png')).size==(1536,768),key
  assert Image.open(DOC/'unity/match_all_16.png').size==(1536,768)
 print('16 comparisons + overview + 32 full views; triangle budgets, skeleton anchors, soles, mesh caches and gear checks passed')
if __name__=='__main__':main()
