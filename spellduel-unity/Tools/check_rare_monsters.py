#!/usr/bin/env python3
"""Check delivered Unity rare-monster audits and native preview completeness."""
import argparse
import json
import struct
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--require-mobile', action='store_true')
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1] / 'Docs/pending_preview/rare_monsters'
    report = json.loads((root / 'rare_audit.json').read_text(encoding='utf-8'))
    rows = report['enemies']
    assert [row['kind'] for row in rows] == [19, 20, 21, 22]
    flags = ['sharedMeshes', 'pivotsUnchanged', 'hitChangesColour',
             'deathChangesColour', 'chargeChangesColour']
    for row in rows:
        assert -.05 <= row['sole'] <= .05, row
        assert 0 < row['triangles'] + 2 <= 4000, row  # projected contact shadow is 2 more
        assert all(row[flag] for flag in flags), row
        assert all(row[field] > 0 for field in
                   ['registeredColours', 'registeredEyes', 'chargedSurfaces']), row
        for axis in 'xyz':
            assert row['newSize'][axis] <= row['oldSize'][axis] * 1.25 + .08, row
        for version in ['before', 'after']:
            for view in ['front', 'side', 'rear45', 'windup', 'attack']:
                path = root / f"rare_{row['kind']}_{version}_{view}.png"
                data = path.read_bytes()
                assert data[:8] == b'\x89PNG\r\n\x1a\n', path
                assert struct.unpack('>II', data[16:24]) == (960, 960), path
    for version in ['before', 'after']:
        data = (root / f'rare_lineup_{version}.png').read_bytes()
        assert struct.unpack('>II', data[16:24]) == (2800, 900)
    native = json.loads((root / 'native_ci.json').read_text())
    assert native['conclusion'] == 'success', native
    if args.require_mobile:
        mobile = json.loads((root / 'platform_ci.json').read_text())
        assert mobile['head'] == native['head'], 'Proofs must test the same source commit'
        for name in ['android', 'ios-export', 'ios-ipa']:
            assert any(job['name'] == name and job['conclusion'] == 'success'
                       for job in mobile['jobs']), name
    print('PASS: four rare rigs, 4000 triangle limit including shadow, cache, pivots, colour states, 42 native views')


if __name__ == '__main__':
    main()
