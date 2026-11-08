"""Stage/finalize the existing release with the locally reconstructed hands worker."""
import base64
import hashlib
import json
from pathlib import Path
import shutil
import sys
import zipfile

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / 'artifacts/installer-1.1.0-psvr2'
OUT = ROOT / 'artifacts/installer-1.1.0-hands'


def sha(path):
    with path.open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest().upper()


def manifest(root):
    (root / 'SHA256SUMS.txt').write_text(''.join(
        f'{sha(p)}  {p.relative_to(root).as_posix()}\n' for p in sorted(root.rglob('*'))
        if p.is_file() and p.name != 'SHA256SUMS.txt'), encoding='utf-8')


if sys.argv[1:] == ['prepare']:
    OUT.mkdir()
    shutil.copytree(BASE / 'bundle', OUT / 'bundle')
    shutil.copytree(BASE / 'release-assets', OUT / 'release-assets')
    catalog = json.loads(base64.b64decode(json.loads((BASE / 'bundle/catalog.signed.json').read_text())['payload']))
    catalog['version'] = '1.1.2'
    core = next(m for g in catalog['games'] if g['id'] == 'darkness2' for m in g['modules'] if m['id'] == 'vr-core')
    core['version'] = '1.1.0-20261108-hands'
    (OUT / 'catalog.unsigned.json').write_text(json.dumps(catalog, indent=2, ensure_ascii=False)+'\n', encoding='utf-8')
    print('Prepared catalog marker; game packages unchanged. Sign, build, then finish.')
elif sys.argv[1:] == ['finish']:
    bundle, public = OUT / 'bundle', OUT / 'release-assets'
    shutil.copyfile(OUT / 'publish/TrilexModInstaller.exe', bundle / 'TrilexModInstaller.exe')
    for target in [bundle, public]:
        shutil.copyfile(OUT / 'catalog.signed.json', target / 'catalog.signed.json')
    for name in ['DARKNESS-HANDS.md', 'PSVR2-TOOLKIT.md']:
        shutil.copyfile(ROOT / 'docs' / name, bundle / 'docs' / name)
    shutil.copyfile(ROOT / 'docs/releases/installer-1.1.0.md', bundle / 'docs/releases/installer-1.1.0.md')
    # Include the updated worker's corresponding source in the existing notices directory.
    for source in (ROOT / 'tools/darkness-textures').glob('*.py'):
        shutil.copyfile(source, bundle / 'licenses/darkness-textures' / source.name)
    with (bundle / 'RELEASE-NOTES.md').open('a', encoding='utf-8') as f:
        f.write('\n\n'+(ROOT / 'docs/DARKNESS-HANDS.md').read_text(encoding='utf-8'))
    for path in (BASE / 'bundle/packages').rglob('*'):
        if path.is_file():
            assert sha(path) == sha(bundle / path.relative_to(BASE / 'bundle')), path
    manifest(bundle)
    archive = public / 'Trilex-Mod-Installer-1.1.0-win-x64.zip'
    with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED) as zip:
        for path in sorted(bundle.rglob('*')):
            if path.is_file():
                zip.write(path, path.relative_to(bundle).as_posix())
    with zipfile.ZipFile(archive) as zip:
        assert zip.testzip() is None
        for item in zip.infolist():
            with zip.open(item) as f:
                assert hashlib.file_digest(f, 'sha256').hexdigest().upper() == sha(bundle / item.filename)
    manifest(public)
    (OUT / 'receipt.json').write_text(json.dumps(dict(zipSha256=sha(archive),exeSha256=sha(bundle/'TrilexModInstaller.exe'),
        catalogSha256=sha(bundle/'catalog.signed.json'),gamePackages='all unchanged, SHA256 verified',zipContents='all entries verified'), indent=2))
    print((OUT / 'receipt.json').read_text())
else:
    raise SystemExit('Usage: refresh-1.1.0-hands.py prepare|finish')
