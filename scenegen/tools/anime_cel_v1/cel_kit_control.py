"""Independent API v1 extension for repainting an existing modular kit.

This deliberately does not change control.py's village/chapter/dungeon API.
python cel_kit_control.py --request job/request.json --response job/response.json
"""
import argparse
import hashlib
import json
import math
import os
import pathlib
import struct
import subprocess
import sys
import time

ROOT = pathlib.Path(__file__).resolve().parent
BUILDER = ROOT / 'extensions' / 'anime_cel_v1' / 'cel_kit_builder.py'
CLEANER = ROOT / 'extensions' / 'anime_cel_v1' / 'clean_cel_exports.py'
BLENDER = pathlib.Path(os.environ.get('BLENDER_PATH', str(ROOT / 'tools' / 'blender-4.5.15-windows-x64' / 'blender.exe')))
STYLE = 'anime_cel_volcano_v1'
LAVA_STYLE = 'anime_cel_volcano_lava_v2'
LAVA_BUILDER = ROOT / 'extensions' / 'anime_cel_lava_v2' / 'cel_lava_builder.py'
LAVA_AUDIT = ROOT / 'extensions' / 'anime_cel_lava_v2' / 'validate_exports.py'


class RequestError(Exception):
    def __init__(self, code, message):
        self.code = code
        super(RequestError, self).__init__(message)


def read(path):
    return json.loads(pathlib.Path(path).read_text(encoding='utf-8-sig'))


def write(path, value):
    path = pathlib.Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes((json.dumps(value, ensure_ascii=False, indent=2) + '\n').encode('utf-8'))


def resolve(value, base):
    if not isinstance(value, str) or not value.strip():
        raise RequestError('invalid_request', 'Expected a nonempty path string')
    p = pathlib.Path(value)
    return (p if p.is_absolute() else base / p).resolve()


def sha(path):
    h = hashlib.sha256()
    with pathlib.Path(path).open('rb') as f:
        for block in iter(lambda: f.read(1024 * 1024), b''):
            h.update(block)
    return h.hexdigest()


def png_size(path):
    with pathlib.Path(path).open('rb') as f:
        header = f.read(24)
    if header[:8] != b'\x89PNG\r\n\x1a\n' or len(header) != 24:
        raise RequestError('invalid_config', 'surfaceAtlas must be a PNG image')
    return struct.unpack('>II', header[16:24])


def load_config(request, base):
    if ('config' in request) == ('configFile' in request):
        raise RequestError('invalid_request', 'Provide exactly one of config/configFile')
    if 'configFile' in request:
        path = resolve(request['configFile'], base)
        cfg = read(path)
        cfgbase = path.parent
    else:
        cfg = dict(request['config'])
        cfgbase = base
    style = cfg.get('styleId', STYLE)
    if style not in (STYLE, LAVA_STYLE):
        raise RequestError('invalid_config', 'Unsupported styleId')
    quality = cfg.get('quality', 'mobile')
    if quality not in ('mobile', 'draft'):
        raise RequestError('invalid_config', 'quality must be mobile or draft')
    kitdir = resolve(cfg.get('sourceKit'), cfgbase)
    atlas = resolve(cfg.get('surfaceAtlas'), cfgbase)
    if not (kitdir / 'kit.json').is_file() or not atlas.is_file():
        raise RequestError('invalid_config', 'sourceKit/kit.json and surfaceAtlas must exist')
    w, h = png_size(atlas)
    if w != h or min(w, h) < 1024:
        raise RequestError('invalid_config', 'Expected a square 2x2 atlas, at least 1024 pixels')
    kit = read(kitdir / 'kit.json')
    required = {'wall_straight', 'wall_corner_out', 'wall_corner_in', 'wall_end', 'gate', 'cave_entrance', 'backdrop_rock'}
    if not required.issubset({p['id'] for p in kit['parts']}):
        raise RequestError('invalid_config', 'sourceKit does not contain the required volcano kit parts')
    files = []
    for part in kit['parts']:
        model = (kitdir / part['file']).resolve()
        if model.parent != kitdir or not model.is_file():
            raise RequestError('invalid_config', 'Missing/unsafe part file: ' + str(part['file']))
        for value in part['bounds']['min'] + part['bounds']['max']:
            if not math.isfinite(value):
                raise RequestError('invalid_config', 'Nonfinite source bounds')
        files.append({'file': part['file'], 'sha256': sha(model)})
    if style == LAVA_STYLE and kit.get('styleId') != STYLE:
        raise RequestError('invalid_config', 'Lava v2 requires the protected cel-v1 source kit')
    return dict(styleId=style, sourceKit=str(kitdir), surfaceAtlas=str(atlas), quality=quality), kit, files


def plan(cfg, kit, files):
    lava = cfg['styleId'] == LAVA_STYLE
    return {'styleId': cfg['styleId'], 'quality': cfg['quality'], 'parts': len(kit['parts'])+(2 if lava else 0),
            'sourceTriangles': sum(p['triangles'] for p in kit['parts']),
            'sourcePartFiles': files, 'sourceKitSha256': sha(pathlib.Path(cfg['sourceKit']) / 'kit.json'),
            'surfaceAtlasSha256': sha(cfg['surfaceAtlas']),
            'atlasSizes': {'main': 4096 if cfg['quality'] == 'mobile' else 1024,
                           'backdrop': 1024 if cfg['quality'] == 'mobile' else 512,
                           'emissive': 1024 if cfg['quality'] == 'mobile' else 256, 'ground': 1024},
            'lavaFeatures': (['6-10% physical-area wall veins','wall_lavafall a/b <=700 triangles with lava_flow child','geometric door seal and cave interior glow','two cratered volcano cones','separate backdrop emission mask','no baked wall-foot underglow'] if lava else []),
            'warnings': ['Baked shading is part-local; rotated instances do not relight automatically.',
                         'Existing 2D obstacle sprites keep their former style until separately restyled.',
                         'This extension makes visuals and an example layout; game tracking/combat/controllers are external.',
                         'Mobile FPS, heat and camera overhead require device profiling.']}


def inspect(folder):
    if not folder.is_dir():
        raise RequestError('output_missing', str(folder))
    for name in ('kit.json', 'inspect.json', 'level.glb', 'level.json', 'previews/player_view.png', 'previews/hero_view.png'):
        if not (folder / name).is_file():
            raise RequestError('invalid_output', 'Missing output ' + name)
    kit = read(folder / 'kit.json')
    report = read(folder / 'inspect.json')
    if not report.get('allChecksPassed') or report.get('quick'):
        raise RequestError('invalid_output', 'Full-quality kit checks must pass')
    names = set()
    for part in kit['parts']:
        names.add('fbx/' + part['file'])
        for name in (part.get('separateFiles') or {}).values():
            names.add('fbx/' + name)
        if part['triangles'] > part['triangleBudget']:
            raise RequestError('invalid_output', 'Triangle budget exceeded')
    for key in ('albedo', 'backdropAlbedo', 'emissive', 'ground', 'groundCrust'):
        names.add(kit['textures'][key])
    if kit.get('styleId') == LAVA_STYLE:
        names.add(kit['textures']['backdropEmissive'])
        if kit.get('version') != 5 or report.get('footUnderglowBaked') is not False:
            raise RequestError('invalid_output', '005 requires kit version 5 and no baked foot underglow')
        if not report.get('exportAudit',{}).get('ok'):
            raise RequestError('invalid_output', '005 requires the independent exported-FBX audit')
    names.add('kit.json')
    inventory = []
    for name in sorted(names):
        p = (folder / name).resolve()
        try:
            p.relative_to(folder.resolve())
        except ValueError:
            raise RequestError('invalid_output', 'Asset escapes output directory: ' + name)
        if not p.is_file():
            raise RequestError('invalid_output', 'Missing ' + name)
        inventory.append({'file': name, 'bytes': p.stat().st_size, 'sha256': sha(p)})
    return {'valid': True, 'styleId': kit.get('styleId',STYLE), 'outputFolder': str(folder), 'statistics': report['totals'],
            'budgetChecks': report['budgets'], 'assetInventory': inventory,
            'assetBytes': sum(i['bytes'] for i in inventory), 'phoneFpsMeasured': False}


def respond(request, base=None):
    base = pathlib.Path(base or '.').resolve()
    response = {'apiVersion': 1, 'requestId': request.get('requestId', ''), 'ok': False}
    try:
        if request.get('apiVersion') != 1:
            raise RequestError('unsupported_version', 'apiVersion must be 1')
        action = request.get('action')
        if action == 'capabilities':
            result = {'extension': 'anime_cel_modular_kit', 'styleIds': [STYLE,LAVA_STYLE], 'apiVersion': 1,
                      'actions': ['capabilities', 'plan', 'validate', 'generate', 'inspect'],
                      'input': 'Existing volcano v3-compatible kit + original 2x2 surface PNG atlas',
                      'output': 'FBX part kit, baked albedo/mask, sample GLB level, actual 3D previews',
                      'coreApiChanged': False}
        elif action == 'inspect':
            result = inspect(resolve(request.get('outputFolder'), base))
        elif action in ('plan', 'validate', 'generate'):
            cfg, kit, files = load_config(request, base)
            result = plan(cfg, kit, files)
            if action == 'validate':
                result['valid'] = True
            if action == 'generate':
                folder = resolve(request.get('outputFolder'), base)
                try:
                    folder.mkdir(parents=True, exist_ok=False)
                except FileExistsError:
                    raise RequestError('output_exists', str(folder))
                write(folder / 'config.json', cfg)
                write(folder / 'status.json', {'status': 'running', 'requestId': response['requestId'], 'input': result})
                blender = resolve(request['blender'], base) if 'blender' in request else BLENDER
                builder = LAVA_BUILDER if cfg['styleId']==LAVA_STYLE else BUILDER
                cleaner = LAVA_AUDIT if cfg['styleId']==LAVA_STYLE else CLEANER
                if not blender.is_file() or not builder.is_file() or not cleaner.is_file():
                    write(folder / 'status.json', {'status': 'failed', 'reason': 'blender_missing'})
                    raise RequestError('blender_missing', 'Blender or cel builder unavailable')
                cmd = [str(blender), '-b', '--python-exit-code', '1', '--python', str(builder), '--',
                       '--out', str(folder), '--v2', cfg['sourceKit'], '--surface-atlas', cfg['surfaceAtlas']]
                if cfg['quality'] == 'draft':
                    cmd.append('--quick')
                cleanup = [str(blender), '-b', '--python-exit-code', '1', '--python', str(cleaner), '--', '--run', str(folder)]
                write(folder / 'build-command.json', {'argv': cmd, 'builderSha256': sha(builder),
                                                       'cleanupArgv': cleanup, 'cleanerSha256': sha(cleaner),
                                                       'extraSources':{p.name:sha(p) for p in builder.parent.glob('*.py')}})
                start = time.time()
                try:
                    with (folder / 'generation.log').open('wb') as log:
                        proc = subprocess.run(cmd, stdout=log, stderr=subprocess.STDOUT,
                                              timeout=request.get('timeoutSeconds', 1200))
                    if proc.returncode:
                        raise RequestError('generation_failed', 'Blender exited ' + str(proc.returncode))
                    if cfg['quality'] == 'mobile':
                        with (folder / 'generation.log').open('ab') as log:
                            proc = subprocess.run(cleanup, stdout=log, stderr=subprocess.STDOUT,
                                                  timeout=max(1,request.get('timeoutSeconds',1200)-(time.time()-start)))
                        if proc.returncode:
                            raise RequestError('generation_failed', 'FBX cleanup/audit exited ' + str(proc.returncode))
                    result = inspect(folder) if cfg['quality'] == 'mobile' else {'draft': True, 'outputFolder': str(folder)}
                except (RequestError, subprocess.TimeoutExpired) as exc:
                    write(folder / 'status.json', {'status': 'failed', 'reason': str(exc)})
                    if isinstance(exc, subprocess.TimeoutExpired):
                        raise RequestError('timeout', 'Blender generation timed out')
                    raise
                write(folder / 'status.json', {'status': 'complete', 'elapsedSeconds': round(time.time() - start, 2),
                                                'requestId': response['requestId'], 'styleId': cfg['styleId'],
                                                'exportCleanupComplete':cfg['quality']=='mobile'})
        else:
            raise RequestError('unknown_action', str(action))
        response.update(ok=True, result=result)
    except RequestError as exc:
        response['error'] = {'code': exc.code, 'message': str(exc)}
    except (KeyError, TypeError, ValueError, OSError) as exc:
        response['error'] = {'code': 'invalid_request', 'message': str(exc)}
    return response


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--request', required=True)
    parser.add_argument('--response')
    args = parser.parse_args()
    path = pathlib.Path(args.request).resolve()
    response = respond(read(path), path.parent)
    if args.response:
        write(args.response, response)
    print(json.dumps(response, ensure_ascii=True))
    return 0 if response['ok'] else 2


if __name__ == '__main__':
    sys.exit(main())
