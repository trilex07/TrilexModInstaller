"""Prepare signed-catalog inputs from a verified public bundle and explicit payloads.

Does not write a game installation, sign, upload or run any payload.
"""
import argparse
import copy
import hashlib
import io
import json
from pathlib import Path
import zipfile

import pefile


def sha(data):
    return hashlib.sha256(data).hexdigest().upper()


def probes(path, rvas):
    pe = pefile.PE(str(path), fast_load=True)
    assert pe.FILE_HEADER.Machine == 0x8664
    result = []
    for rva in rvas:
        section = next(s for s in pe.sections if s.VirtualAddress <= rva and
                       rva + 64 <= s.VirtualAddress + s.SizeOfRawData)
        assert section.Characteristics & 0x20000000
        data = pe.get_data(rva, 64)
        assert len(data) == 64
        result.append(dict(rva=rva, length=64, sha256=sha(data)))
    pe.close()
    return result


def main():
    p = argparse.ArgumentParser()
    for arg in ('stage', 'steam-dll', 'epic-overlay', 'nvidia', 'steam-exe', 'epic-exe'):
        p.add_argument('--' + arg, type=Path, required=True)
    a = p.parse_args()
    old = json.loads((a.stage / 'old-catalog.json').read_text('utf-8'))
    cat = copy.deepcopy(old)
    cat['version'] = '1.1.10'
    cat['expiresUtc'] = '2027-05-16T00:00:00Z'
    game = next(g for g in cat['games'] if g['id'] == 'wonderlands')
    baseline = game['modules'][0]
    bundle = a.stage / 'bundle'
    original = (bundle / 'packages/wonderlands/vr-core.zip').read_bytes()
    assert sha(original) == baseline['archiveSha256'].upper()
    steam = a.steam_dll.read_bytes()
    assert sha(steam) == '780C91D61C0730E947BC238A9AF00F0BCD78A5AC0803980B46AA347EE0165D78'
    assert sha(a.epic_overlay.read_bytes()) == 'E170410C242EE16681C16114F77FFC95A11150028A1EF22436303CE05183A719'
    with zipfile.ZipFile(a.epic_overlay) as z:
        epic = z.read('xinput1_3.dll')
    assert sha(epic) == '535502EED9003AC7207CFE109BACF7C6A746FDEF3DD11D437782120453878DDB'
    nv = json.loads((a.nvidia / 'nvidia-runtime-manifest.json').read_text('utf-8'))
    modules = {}
    for profile, dll in [('steam', steam), ('epic', epic)]:
        mod = copy.deepcopy(baseline)
        out = io.BytesIO()
        with zipfile.ZipFile(io.BytesIO(original)) as src, zipfile.ZipFile(out, 'w', zipfile.ZIP_DEFLATED) as dst:
            for entry in src.infolist():
                data = src.read(entry)
                spec = next((f for f in mod['files'] if f['kind'] == 'copy' and f['entry'] == entry.filename), None)
                if spec:
                    assert sha(data) == spec['sha256'].upper()
                    if spec['path'].endswith('/xinput1_3.dll'):
                        data = dll
                        spec['sha256'] = sha(data)
                dst.writestr(entry, data)
            for f in nv['files']:
                rel = f['entry'].removeprefix('nvidia-runtime/')
                data = (a.nvidia / rel).read_bytes()
                assert sha(data) == f['sha256'].upper()
                dst.writestr(f['entry'], data)
                mod['files'].append(dict(f, ownership='licensed'))
        values = next(f['iniValues'] for f in mod['files'] if f['kind'] == 'ini')
        values.update({'Simulator/Enabled': '0', 'Simulator/RuntimeJson': '', 'Simulator/DataDir': '',
                       'Performance/DLSSRuntimeDir': '', 'Performance/DLSSMode': '0',
                       'FrameGeneration/Enabled': '0'})
        if profile == 'steam':
            values.update({'Weapon/OpticalScope': '1', 'Weapon/ScopeWorldObstruction': '1',
                           'Weapon/ScopeEye': 'Auto', 'Weapon/ScopeDot': '0',
                           'Weapon/FastScopeGripRelease': '1', 'Weapon/BeamGunOrigin': '1',
                           'Weapon/RebaseWeaponParticles': '1'})
        # Keep the known Epic behavior; do not advertise the new Steam optical path there.
        data = out.getvalue()
        name = 'wonderlands-vr-' + profile + '-20261116.zip'
        (a.stage / name).write_bytes(data)
        mod.update(version='1.1.3-20261116', archiveSha256=sha(data), downloadBytes=len(data),
                   downloadUrl='https://github.com/trilex07/TrilexModInstaller/releases/download/installer-1.1.3/' + name)
        with zipfile.ZipFile(io.BytesIO(data)) as z:
            assert z.testzip() is None
            mod['expandedBytes'] = sum(f.file_size for f in z.infolist())
        if profile == 'epic':
            (bundle / 'packages/wonderlands' / (sha(data) + '.zip')).write_bytes(data)
        if profile == 'steam':
            (bundle / 'packages/wonderlands/vr-core.zip').write_bytes(data)
        modules[profile] = [mod]
    game['modules'] = modules['steam']
    game['exeSha256'] = [sha(a.steam_exe.read_bytes()), sha(a.epic_exe.read_bytes())]
    game['buildProfiles'] = [
        dict(id='steam', probes=probes(a.steam_exe, [0x28C6FF0, 0x28D5720, 0x173FB80]), modules=modules['steam']),
        dict(id='epic', probes=probes(a.epic_exe, [0x28BD860, 0x28CBF90, 0x1736600]), modules=modules['epic'])]
    assert [g for g in cat['games'] if g['id'] != 'wonderlands'] == [g for g in old['games'] if g['id'] != 'wonderlands']
    (a.stage / 'catalog.unsigned.json').write_text(json.dumps(cat, ensure_ascii=False, indent=2) + '\n', 'utf-8')
    print('PASS: distinct Steam/Epic packages, exact prior Epic overlay, NVIDIA files, other games preserved')


if __name__ == '__main__':
    main()
