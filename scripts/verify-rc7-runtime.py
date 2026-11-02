"""Verify actual packed RC7 bytes and configuration against the RC6 baseline."""
import hashlib
import json
from pathlib import Path
import zipfile

ROOT = Path(__file__).resolve().parents[1]
OLD = ROOT / 'artifacts/Trilex-SingularityVR-1.0.0-rc.6'
NEW = ROOT / 'artifacts/Trilex-SingularityVR-1.0.0-rc.7'


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def verify():
    old = {m['id']: m for m in read(OLD/'catalog.json')['games'][0]['modules']}
    new = {m['id']: m for m in read(NEW/'catalog.json')['games'][0]['modules']}
    assert new.keys() == old.keys()
    for mid, m in new.items():
        with (NEW/'packages'/(mid+'.zip')).open('rb') as f:
            assert hashlib.file_digest(f, 'sha256').hexdigest().upper() == m['archiveSha256']
        assert m['rightsVerified'] == old[mid]['rightsVerified']
        assert m.get('publisherReview') == old[mid].get('publisherReview')
        assert m['dependencies'] == old[mid]['dependencies']
        if mid not in ('vr-core', 'portable-settings'):
            assert m['archiveSha256'] == old[mid]['archiveSha256']
            assert m['files'] == old[mid]['files']
    core = new['vr-core']
    with zipfile.ZipFile(NEW/'packages/vr-core.zip') as archive:
        for f in core['files']:
            if f['kind'] == 'copy':
                assert hashlib.sha256(archive.read(f['entry'])).hexdigest().upper() == f['sha256']
        assert hashlib.sha256(archive.read('Binaries/d3d9.dll')).hexdigest().upper() == 'A16FC1927B215A1A306DBE56CE24B1CB03A042A541EC7C8C4A4CBA1BF6A4A75A'
        assert hashlib.sha256(archive.read('Binaries/SingularityPsvr2Bridge.exe')).hexdigest().upper() == 'C4D002A3F09258F311EFF9CFB4FF20ABAD4CEB9CF3967E25A80212DFAF335271'
        assert b'MIT' in archive.read('Binaries/vr-assets/licenses/SingularityVR-GAZE-NOTICES.txt')
    settings = next(f for f in new['portable-settings']['files'] if f['path'] == 'Binaries/SingularityVR.ini')['iniValues']
    expected = {'Render/CutsceneStereoCinema':'1', 'Render/CinemaMutantAimAssist':'1',
                'EyeTracking/Enabled':'1', 'EyeTracking/WeaponWheel':'1',
                'EyeTracking/NisSharpen':'1', 'Haptics/DamagePulses':'1',
                'Testing/GrantKasimovCenturion':'0', 'Upscaling/Mode':'NISSharpen',
                'Upscaling/Sharpness':'1.00', 'OLED/Preset':'2',
                'Render/EquipmentWristSuppressHealth':'1',
                'Render/EquipmentWristPreviewNoDepth':'0'}
    for key, value in expected.items():
        assert settings[key] == value, (key, settings.get(key))
    before = {f['path']: f for f in old['vr-core']['files']}
    for f in core['files']:
        if f['path'] in before and f['path'] not in ('Binaries/d3d9.dll', 'Binaries/SingularityPsvr2Bridge.exe'):
            assert f == before[f['path']], f['path']
    print('PASS actual RC7 archive hashes, runtime/bridge bytes, settings, rights, dependencies and 10 unchanged RC6 modules')


if __name__ == '__main__':
    verify()
