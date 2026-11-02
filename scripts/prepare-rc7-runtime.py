"""Package the accepted A16FC192 runtime, without reading or writing Steam.

Reuse RC6's exact reviewed reconstruction recipes and private assets. Repack only
the authored runtime and portable preset; preserve rights declarations unchanged.
Signing, publish, lifecycle verification and release upload are separate gates.
"""
import configparser
import copy
import hashlib
import json
import os
from pathlib import Path
import shutil
import struct
import subprocess
import zipfile

ROOT = Path(__file__).resolve().parents[1]
OLD = ROOT / 'artifacts/Trilex-SingularityVR-1.0.0-rc.6'
OUT = ROOT / 'artifacts/Trilex-SingularityVR-1.0.0-rc.7'
SOURCE = ROOT.parent / 'SingularityVR-gaze-cinema'
TAG = 'installer-1.0.0-rc.7'
DLL_SHA = 'A16FC1927B215A1A306DBE56CE24B1CB03A042A541EC7C8C4A4CBA1BF6A4A75A'
BRIDGE_SHA = 'C4D002A3F09258F311EFF9CFB4FF20ABAD4CEB9CF3967E25A80212DFAF335271'
RUNTIME_SOURCE = '6c38abea084023e8e5f7702385e21b37e71adc9f'
FLAGS = {
    'Render/CinemaMutantAimAssist': '1',
    'Render/CutsceneStereoCinema': '1',
    'Haptics/DamagePulses': '1',
    'Testing/GrantKasimovCenturion': '0',
    'EyeTracking/Enabled': '1',
    'EyeTracking/WeaponWheel': '1',
    'EyeTracking/NisSharpen': '1',
}


def read(p):
    return json.loads(p.read_text(encoding='utf-8-sig'))


def write(p, value):
    p.write_text(json.dumps(value, indent=2) + '\n', encoding='utf-8')


def sha(p):
    with p.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest().upper()


def machine(p):
    data = p.read_bytes()
    offset = struct.unpack_from('<I', data, 60)[0]
    assert data[offset:offset+4] == b'PE\0\0'
    return struct.unpack_from('<H', data, offset+4)[0]


def prepare():
    if OUT.exists():
        raise ValueError('Fresh RC7 output required')
    stage = SOURCE / 'build/next-weapon-reload/mutant-registry-20261102'
    dll = stage / 'd3d9.dll'
    bridge = SOURCE / 'build/next-weapon-reload/gaze-damage-20261102/SingularityPsvr2Bridge.exe'
    assert sha(dll) == DLL_SHA and machine(dll) == 0x14c
    assert sha(bridge) == BRIDGE_SHA and machine(bridge) == 0x8664
    provenance = read(stage / 'd3d9.build.json')
    assert provenance['commit'] == RUNTIME_SOURCE and provenance['optimization'] == '/O2'
    preset = configparser.ConfigParser(interpolation=None, strict=False)
    preset.optionxform = str
    preset.read(stage / 'SingularityVR.ini', encoding='utf-8-sig')
    values = {s+'/'+k: v for s in preset.sections() for k, v in preset[s].items()}
    old = read(OLD / 'catalog.json')
    catalog = copy.deepcopy(old)
    catalog['version'] = '1.0.0-rc.7'
    catalog['expiresUtc'] = '2027-04-26T00:00:00Z'
    OUT.mkdir()
    packages = OUT / 'packages'
    packages.mkdir()
    changed = []
    tools = ROOT / 'src/Trilex.Installer.Tools/bin/Release/net10.0/Trilex.Installer.Tools.dll'
    for m in catalog['games'][0]['modules']:
        mid = m['id']
        m['downloadUrl'] = f'https://github.com/trilex07/TrilexModInstaller/releases/download/{TAG}/{mid}.zip'
        if mid in ('vr-core', 'portable-settings'):
            m['version'] = 'v7-20261102-rc7'
            module_stage = OUT / (mid+'-stage')
            module_stage.mkdir()
            with zipfile.ZipFile(OLD / 'packages' / (mid+'.zip')) as archive:
                archive.extractall(module_stage)
            if mid == 'vr-core':
                additions = {
                    'Binaries/d3d9.dll': dll,
                    'Binaries/SingularityPsvr2Bridge.exe': bridge,
                    'Binaries/vr-assets/licenses/SingularityVR-GAZE-NOTICES.txt': SOURCE / 'docs/THIRD-PARTY-GAZE.md',
                }
                for path, source in additions.items():
                    target = module_stage / path
                    target.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copyfile(source, target)
                    f = next((f for f in m['files'] if f['path'] == path), None)
                    if f is None:
                        f = dict(path=path, entry=path, kind='copy', ownership='licensed')
                        m['files'].append(f)
                    f['sha256'] = sha(target)
            else:
                f = next(f for f in m['files'] if f['path'] == 'Binaries/SingularityVR.ini')
                delta = {k:v for k,v in values.items() if f['iniValues'].get(k) != v}
                assert delta == FLAGS, ('Unexpected preset differences', delta)
                assert not (set(f['iniValues']) - set(values)), 'Unexpected removed setting'
                f['iniValues'].update(FLAGS)
            manifest = OUT / (mid+'.input.json')
            write(manifest, m)
            subprocess.run(['dotnet', str(tools), 'pack', str(manifest), str(module_stage), str(packages / (mid+'.zip'))], check=True)
            m.update(read(packages / (mid+'.zip.module.json')))
            changed.append(mid)
        else:
            # Immutable shared package bytes; never write through this hardlink.
            os.link(OLD / 'packages' / (mid+'.zip'), packages / (mid+'.zip'))
            assert sha(packages / (mid+'.zip')) == m['archiveSha256']
    write(OUT / 'catalog.json', catalog)
    write(OUT / 'runtime-content-evidence.json', dict(
        status='PACKED_NOT_RELEASED', source=RUNTIME_SOURCE,
        integratedMain='92fe119c187076defb2f5038e1678ec15c0da137',
        gameplayDllSha256=DLL_SHA, bridgeSha256=BRIDGE_SHA,
        changedModules=changed, unchangedRc6Modules=10, settings=FLAGS,
        steamWrites='NONE', gameLaunch='NOT RUN',
        physical='Mutant hit assistance accepted by user; no fresh clean-install HMD pass',
        limitations=['Retail compatibility unverified', 'Seeker physical reload remains disabled',
                    'Bink movies remain mono; stereo cinema applies to engine-rendered scenes',
                    'Gaze requires compatible PSVR2 Toolkit installation; no driver bundled',
                    'No FPS or 90Hz guarantee']))
    print('PASS accepted runtime+bridge, seven exact preset deltas and unchanged RC6 asset closure')


if __name__ == '__main__':
    prepare()
