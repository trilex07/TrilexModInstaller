"""Stage and pack the RaptureVR module (Everybody's Gone to the Rapture, Steam 417880) for the installer.

Reads the built RaptureVR checkout (dinput8.dll, openxr_loader.dll, the VR profile, panel art), stages the files under
runtime/, packs them with the installer's own `pack` tool (same consumer extractor audit), and writes an UNSIGNED
catalog with the Rapture game added. It never signs, uploads or touches a game folder.

    python scripts/package-rapture.py --mod D:/projekty/rapturevr --tag 20261121

Then (maintainer, after review): sign artifacts/rapture-<tag>/catalog.unsigned.json, attach the ZIP to the release
named in downloadUrl, and add packages/rapture/vr-core.zip to the portable bundle.
"""
import argparse, base64, hashlib, json, shutil, subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
EXE_SHA = '4B9E527DEA516E33C1C3C5EA10B99C3482B1B8BE20670F7F908C23C7FE4C1FEF'   # Rapture_Release.exe (Steam build RaptureVR targets)
UI_ART = ['panel_bg.png', 'header.png', 'button_normal.png', 'button_hover.png', 'button_selected.png', 'button_accent.png', 'controllers.png',
          'icon_walk.png', 'icon_turn.png', 'icon_snap.png', 'icon_eye.png', 'icon_screen.png', 'icon_recentre.png', 'icon_sharpen.png',
          'icon_vignette.png', 'icon_controls.png', 'icon_reset.png']
MIT = '''MIT License

Copyright (c) 2026 Trilex / trilex07

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

RaptureVR is a fan VR modification. It contains no game binaries or game assets;
"Everybody's Gone to the Rapture" belongs to its owners. The VR panel art in
RaptureVR-ui is original Trilex art.
'''

def sha(p): return hashlib.sha256(Path(p).read_bytes()).hexdigest().upper()

def main():
    ap = argparse.ArgumentParser(); ap.add_argument('--mod', type=Path, required=True); ap.add_argument('--tag', required=True)
    ap.add_argument('--base', type=Path, help='signed catalog to extend (default: newest catalog.signed.json under artifacts by version)')
    a = ap.parse_args()
    out = ROOT / 'artifacts' / f'rapture-{a.tag}'
    assert not out.exists(), f'Fresh output folder required: {out}'
    stage = out / 'stage'; rt = stage / 'runtime'; (rt / 'RaptureVR-ui').mkdir(parents=True)
    src = {'dinput8.dll': a.mod / 'build/Release/dinput8.dll', 'openxr_loader.dll': a.mod / 'build/Release/openxr_loader.dll',
           'RaptureVR.ini': a.mod / 'config/RaptureVR.vr-camera.ini'}
    for name, p in src.items(): shutil.copyfile(p, rt / name)
    ini = (rt / 'RaptureVR.ini').read_text('utf-8', errors='replace')
    for key in ('SafeMode=0', 'VrCamera=1', 'GameCfgPreset=1', 'RenderSizeFromRuntime=1'):
        assert key in ini, f'VR profile lacks {key}'
    dll = (rt / 'dinput8.dll').read_bytes()
    assert b'VCRUNTIME140' not in dll.upper() and b'MSVCP140' not in dll.upper(), 'proxy must use the static CRT'
    for n in UI_ART: shutil.copyfile(a.mod / 'assets/ui' / n, rt / 'RaptureVR-ui' / n)
    (rt / 'LICENSE-RaptureVR.txt').write_text(MIT, encoding='utf-8', newline='\r\n')
    openxr = next(ROOT.glob('artifacts/**/licenses/wonderlands/LICENSE-OpenXR.txt'))
    shutil.copyfile(openxr, rt / 'LICENSE-OpenXR.txt')

    files = []
    def add(target, entry, ownership):
        files.append({'path': target, 'entry': entry, 'sha256': sha(stage / entry), 'kind': 'copy', 'ownership': ownership})
    add('Bin64/dinput8.dll', 'runtime/dinput8.dll', 'mod-authored')
    add('Bin64/openxr_loader.dll', 'runtime/openxr_loader.dll', 'licensed')
    add('Bin64/RaptureVR.ini', 'runtime/RaptureVR.ini', 'mod-authored')
    add('Bin64/LICENSE-RaptureVR.txt', 'runtime/LICENSE-RaptureVR.txt', 'mod-authored')
    add('Bin64/LICENSE-OpenXR.txt', 'runtime/LICENSE-OpenXR.txt', 'licensed')
    for n in UI_ART: add('Bin64/RaptureVR-ui/' + n, 'runtime/RaptureVR-ui/' + n, 'mod-authored')
    package = f'rapture-vr-core-{a.tag}.zip'
    module = {
        'id': 'vr-core', 'labels': {'en': 'Native stereo VR, head tracking, controllers, VR panel', 'pl': 'Natywne VR stereo, śledzenie głowy, kontrolery, panel VR'},
        'version': f'0.1.0-{a.tag}', 'state': 'stable', 'required': True, 'dependencies': [],
        'downloadBytes': 0, 'downloadUrl': f'https://github.com/trilex07/TrilexModInstaller/releases/download/installer-1.1.0/{package}',
        'archiveSha256': '', 'expandedBytes': 0, 'rightsVerified': True,
        'rightsEvidence': 'Original Trilex runtime (MIT) and original Trilex panel art; OpenXR loader Apache-2.0 notice included; no game binaries or assets.',
        'files': files, 'reason': ''}
    (out / 'module.json').write_text(json.dumps(module, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    zip_path = out / package
    subprocess.run(['dotnet', 'run', '--project', str(ROOT / 'src/Trilex.Installer.Tools'), '-c', 'Release', '--', 'pack',
                    str(out / 'module.json'), str(stage), str(zip_path)], check=True, cwd=ROOT)
    packed = json.loads((Path(str(zip_path) + '.module.json')).read_text('utf-8-sig'))

    def version(path):
        c = json.loads(base64.b64decode(json.loads(Path(path).read_text('utf-8'))['payload']))
        return tuple(int(x) for x in c['version'].split('.'))
    base = a.base or max([p for p in ROOT.glob('artifacts/**/catalog.signed.json') if 'rapture-' not in str(p)] + [ROOT / 'catalog/bootstrap.json'], key=version)
    envelope = json.loads(Path(base).read_text('utf-8'))
    cat = json.loads(base64.b64decode(envelope['payload']))
    print('base catalog', cat['version'], base)
    v = [int(x) for x in cat['version'].split('.')]; v[-1] += 1; cat['version'] = '.'.join(map(str, v))
    assert all(g['id'] != 'rapture' for g in cat['games']), 'catalog already has Rapture'
    cat['games'].append({'id': 'rapture', 'name': "Everybody's Gone to the Rapture VR", 'state': 'preview', 'adapter': 'RaptureGameAdapter',
                         'steamAppId': '417880', 'executable': 'Bin64/Rapture_Release.exe', 'exeSha256': [EXE_SHA], 'modules': [packed]})
    (out / 'catalog.unsigned.json').write_text(json.dumps(cat, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({'package': str(zip_path), 'archiveSha256': packed['archiveSha256'], 'downloadBytes': packed['downloadBytes'],
                      'files': len(files), 'catalogVersion': cat['version'], 'gameState': 'preview', 'signed': False}, indent=1))

if __name__ == '__main__':
    main()
