"""Validate the native Unity elite/world boss delivery, not generated concept art."""
import json
from pathlib import Path
from PIL import Image

DOC=Path(__file__).resolve().parents[1]/'Docs/elite_preview'
def main():
    audit=json.loads((DOC/'elite_audit.json').read_text(encoding='utf-8-sig'))
    assert audit['unity'].startswith('2022.3.') and audit['testedSkills']==50
    assert {r['kind'] for r in audit['classes']}==set(range(23,48))
    for r in audit['classes']:
        assert r['triangles']<=r['baseline']*2
        assert all(r[k] for k in ('soul','hostWarning','remoteWarning','remoteStrike','vertexColors'))
        for view in ('front','45'):
            assert Image.open(DOC/f"elite_{r['kind']}_{view}.png").size==(640,640)
        for slot in range(2):
            for phase in ('warn','impact'):
                assert Image.open(DOC/f"elite_{r['kind']}_skill{slot}_{phase}.png").size==(800,640)
    for chapter in range(1,6):
        for view in ('front','45'):assert Image.open(DOC/f'elite_chapter{chapter}_{view}.png').size==(1600,320)
    world=json.loads((DOC/'world_boss_audit.json').read_text(encoding='utf-8-sig'))
    assert {b['id'] for b in world['bosses']}=={'wb_furnace','wb_canopy','wb_leviathan'}
    for b in world['bosses']:
        assert b['phases']==3 and b['skills']==6 and b['brokenAppearance'] and b['vertexColors'] and b['triangles']<=20000
        key=b['id']
        for p in range(1,4):
            for view in ('front','45'):Image.open(DOC/f'{key}_phase{p}_{view}.png').verify()
        for part in ('intact','broken'):Image.open(DOC/f'{key}_part_{part}.png').verify()
        for skill in ('sweep','quake','lance','scatter','gather','transition'):
            for state in ('warn','impact'):Image.open(DOC/f'{key}_{skill}_{state}.png').verify()
        Image.open(DOC/f'{key}_player_scale.png').verify()
    ci=json.loads((DOC/'ci_validation.json').read_text(encoding='utf-8-sig'))
    assert all(ci[k]['conclusion']=='success' for k in ('android','ios-export','snapshots'))
    assert ci['verifiedCommit']==json.loads((DOC/'native_source.json').read_text(encoding='utf-8'))['commit']
    print('25 elites / 50 skills / 3 world bosses / native previews / Android and iOS evidence passed')
if __name__=='__main__':main()
