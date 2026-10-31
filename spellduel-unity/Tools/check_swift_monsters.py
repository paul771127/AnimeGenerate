#!/usr/bin/env python3
"""Verify the ten swift variants using native Unity geometry and motion audits."""
import argparse
import json
import struct
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--require-mobile', action='store_true')
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1] / 'Docs/pending_preview/rare_monsters'
    report = json.loads((root / 'swift_audit.json').read_text(encoding='utf-8'))
    rows = report['enemies']
    assert [r['id'] for r in rows] == list(range(171, 181))
    assert [r['baseKind'] for r in rows] == [2, 0, 8, 11, 13, 16, 2, 8, 0, 2]
    for r in rows:
        assert 0 < r['meshTriangles'] < r['totalTriangles'] <= 2500, r
        assert r['totalTriangles'] == r['meshTriangles'] + r['maximumFxTriangles'] + 2, r
        assert r['colourCount'] > 0 and r['eyeCount'] > 0, r
        assert all(r[key] for key in ['cached', 'pivotsUnchanged', 'hit', 'charge', 'death',
                                     'movingTrail', 'stoppedTrail', 'deadTrail']), r
        for view in ['before', 'front', 'side', 'rear45', 'run', 'attack']:
            p = root / f"swift_{r['id']}_{view}.png"
            data = p.read_bytes()
            assert data[:8] == b'\x89PNG\r\n\x1a\n', p
            assert struct.unpack('>II', data[16:24]) == (960, 960), p
    for chapter in range(5):
        data = (root / f'swift_lineup_{chapter}.png').read_bytes()
        assert struct.unpack('>II', data[16:24]) == (1800, 900)
    native = json.loads((root / 'swift_native_ci.json').read_text())
    assert native['conclusion'] == 'success'
    if args.require_mobile:
        mobile = json.loads((root / 'swift_platform_ci.json').read_text())
        assert native['head'] == mobile['head'], 'Native and mobile must test the same code'
        for name in ['android', 'ios-export', 'ios-ipa']:
            assert any(j['name'] == name and j['conclusion'] == 'success' for j in mobile['jobs']), name
    print('PASS: 10 swift variants, 2500 triangle cap including maximum effects, cache, pivots, colours, motion trails, 65 native views')


if __name__ == '__main__':
    main()
