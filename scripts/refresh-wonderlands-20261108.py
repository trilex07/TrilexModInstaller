"""Refresh only Wonderlands, preserving the published hands/Toolkit bundle."""
import base64
import configparser
import hashlib
import importlib.util
import json
from pathlib import Path
import shutil
import sys
import zipfile

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT.parent / 'TrilexModInstaller/artifacts/installer-1.1.0-hands'
OUT = ROOT / 'artifacts/wonderlands-20261108'
GAME = Path("C:/Program Files (x86)/Steam/steamapps/common/Tiny Tina's Wonderlands/OakGame/Binaries/Win64")
EXPECTED = 'DF9A05B1A3CA8881DB69DAF0E36C49EA03EB200465C72519D7526ACDC3F69878'
NAME = 'wonderlands-vr-core-20261108-evening.zip'

def sha(path):
    with path.open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest().upper()

def manifest(root):
    (root/'SHA256SUMS.txt').write_text(''.join(
        f'{sha(p)}  {p.relative_to(root).as_posix()}\n' for p in sorted(root.rglob('*'))
        if p.is_file() and p.name != 'SHA256SUMS.txt'), encoding='utf-8')

def verify(root):
    for line in (root/'SHA256SUMS.txt').read_text(encoding='utf-8-sig').splitlines():
        digest, name = line.split('  ', 1)
        p = (root/name).resolve()
        assert p.is_relative_to(root.resolve()) and sha(p) == digest.upper(), name

if sys.argv[1:] == ['prepare']:
    verify(BASE/'bundle')
    verify(BASE/'release-assets')
    assert sha(GAME/'xinput1_3.dll') == EXPECTED
    assert sha(Path('C:/Users/Trilex/Tiny Tina VR/build/Release/xinput1_3.dll')) == EXPECTED
    OUT.mkdir(parents=True, exist_ok=True)
    shutil.copytree(BASE/'bundle', OUT/'bundle', dirs_exist_ok=True)
    shutil.copytree(BASE/'release-assets', OUT/'release-assets', dirs_exist_ok=True)
    spec = importlib.util.spec_from_file_location('refresh', ROOT/'scripts/refresh-1.1.0-runtimes.py')
    refresh = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(refresh)
    cat = json.loads(base64.b64decode(json.loads((BASE/'bundle/catalog.signed.json').read_text())['payload']))
    before = json.loads(json.dumps(cat))
    core = refresh.module(cat, 'wonderlands', 'vr-core')
    archive = refresh.repack(core, (BASE/'bundle/packages/wonderlands/vr-core.zip').read_bytes(),
        {'OakGame/Binaries/Win64/xinput1_3.dll': (GAME/'xinput1_3.dll').read_bytes()})
    profile = configparser.ConfigParser(interpolation=None, strict=False)
    profile.optionxform = str
    profileBytes = (GAME/'WonderlandsVR.ini').read_bytes()
    profile.read_string(profileBytes.decode('utf-8-sig'))
    values = {f'{s}/{k}':v for s in profile.sections() for k,v in profile[s].items()}
    assert len(values) >= 99
    values.update({'Simulator/Enabled':'0','Simulator/RuntimeJson':'','Simulator/DataDir':''})
    # The DX12 path is still experimental; retain its upstream default-off gate.
    values['VR/D3D12'] = '0'
    next(f for f in core['files'] if f['kind'] == 'ini')['iniValues'] = values
    core['version'] = '1.1.0-20261108-evening'
    core['downloadUrl'] = refresh.RELEASE + NAME
    cat['version'] = '1.1.3'
    assert cat['games'][:2] == before['games'][:2]
    (OUT/'bundle/packages/wonderlands/vr-core.zip').write_bytes(archive)
    (OUT/'release-assets'/NAME).write_bytes(archive)
    (OUT/'catalog.unsigned.json').write_text(json.dumps(cat, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    (OUT/'input-receipt.json').write_text(json.dumps(dict(dllSha256=EXPECTED,
        profileSha256=hashlib.sha256(profileBytes).hexdigest().upper(), catalogVersion=cat['version'],
        moduleVersion=core['version'], iniValues=values), indent=2), encoding='utf-8')
    print('Prepared Wonderlands refresh; other games unchanged.')
elif sys.argv[1:] == ['finish']:
    bundle, public = OUT/'bundle', OUT/'release-assets'
    shutil.copyfile(OUT/'publish/TrilexModInstaller.exe', bundle/'TrilexModInstaller.exe')
    for target in [bundle, public]:
        shutil.copyfile(OUT/'catalog.signed.json', target/'catalog.signed.json')
    shutil.copyfile(ROOT/'docs/releases/installer-1.1.0.md', bundle/'docs/releases/installer-1.1.0.md')
    for p in (BASE/'bundle/packages').rglob('*'):
        if p.is_file() and p.relative_to(BASE/'bundle').as_posix() != 'packages/wonderlands/vr-core.zip':
            assert sha(p) == sha(bundle/p.relative_to(BASE/'bundle')), p
    manifest(bundle)
    archive = public/'Trilex-Mod-Installer-1.1.0-win-x64.zip'
    with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED) as z:
        for p in sorted(bundle.rglob('*')):
            if p.is_file(): z.write(p, p.relative_to(bundle).as_posix())
    with zipfile.ZipFile(archive) as z:
        assert z.testzip() is None
        for item in z.infolist():
            with z.open(item) as f:
                assert hashlib.file_digest(f, 'sha256').hexdigest().upper() == sha(bundle/item.filename)
    manifest(public)
    verify(bundle)
    verify(public)
    (OUT/'receipt.json').write_text(json.dumps(dict(zipSha256=sha(archive),
        exeSha256=sha(bundle/'TrilexModInstaller.exe'),catalogSha256=sha(bundle/'catalog.signed.json'),
        zipContents='all entries verified', otherGamePackages='unchanged, verified'), indent=2))
    print((OUT/'receipt.json').read_text())
else:
    raise SystemExit('Usage: refresh-wonderlands-20261108.py prepare|finish')
