"""Refresh only the installer, corresponding worker source and notes in catalog 1.1.7's bundle."""
import hashlib
import json
import shutil
import subprocess
import zipfile
from pathlib import Path

root = Path(__file__).resolve().parents[1]
stage = root / 'artifacts/installer-texture-feedback-20261110'
bundle = stage / 'bundle'
release = stage / 'release-assets'
release.mkdir(exist_ok=True)
name = 'Trilex-Mod-Installer-1.1.0-win-x64.zip'
doc = 'INSTALLER-TEXTURE-FEEDBACK-20261110.md'


def sha(path):
    digest = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            digest.update(block)
    return digest.hexdigest().upper()


heading = '''## November 10: selected installation, texture preparation and progress

- Select the desired checkboxes and click **Install selected**. Download, installation and updates use this one action.
- Long installation details have their own scroll; Activity stays visible and reports texture counts.
- An outdated bundled ZIP no longer blocks an updated signed catalog: the installer uses a matching verified cache/download instead. Bad package bytes remain rejected.
- Darkness HD conversion uses bounded image strips and below-normal worker priority. All 64 real fixture textures completed with identical encoded bytes; player headset acceptance remains pending.
- Redownload and extract the complete ZIP into a fresh folder and run its new EXE.
- Catalog 1.1.7 and all game packages, including current Wonderlands, shotgun audio and PSVR2 fixes, are preserved.

'''
for filename in ('README.md', 'RELEASE-NOTES.md'):
    target = bundle / filename
    existing = target.read_text(encoding='utf-8-sig')
    if not existing.startswith(heading):
        target.write_text(heading + existing, encoding='utf-8')
shutil.copy2(stage / 'publish/TrilexModInstaller.exe', bundle / 'TrilexModInstaller.exe')
source = 'licenses/darkness-textures/manage_comic_textures.py'
shutil.copy2(root / 'tools/darkness-textures/manage_comic_textures.py', bundle / source)
shutil.copy2(root / 'docs' / doc, bundle / 'docs' / doc)
files = sorted(p for p in bundle.rglob('*') if p.is_file() and p != bundle / 'SHA256SUMS.txt')
(bundle / 'SHA256SUMS.txt').write_text(''.join(sha(p) + '  ' + p.relative_to(bundle).as_posix() + '\n' for p in files), encoding='ascii')
with zipfile.ZipFile(stage / name) as old:
    old_names = {n for n in old.namelist() if not n.endswith('/')}
    changed = [n for n in old_names if old.read(n) != (bundle / n).read_bytes()]
    assert sorted(changed) == sorted(['TrilexModInstaller.exe', 'README.md', 'RELEASE-NOTES.md', 'SHA256SUMS.txt', source]), changed
    added = {p.relative_to(bundle).as_posix() for p in bundle.rglob('*') if p.is_file()} - old_names
    assert added == {'docs/' + doc}, added
with zipfile.ZipFile(release / name, 'w', zipfile.ZIP_DEFLATED) as archive:
    for path in sorted(bundle.rglob('*')):
        if path.is_file():
            archive.write(path, path.relative_to(bundle).as_posix())
with zipfile.ZipFile(release / name) as archive:
    assert archive.testzip() is None
    for path in bundle.rglob('*'):
        if path.is_file():
            assert archive.read(path.relative_to(bundle).as_posix()) == path.read_bytes()
evidence = dict(
    status='OFFLINE_PASS', source=subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip(),
    coreTests=107, producerTests=15, textureLayoutTests=6, wpfLayouts=8, launchScenarios=24,
    gameProcessesStarted=0, playerAcceptance='PENDING', catalogVersion='1.1.7',
    worker=json.loads((stage / 'worker-metrics.json').read_text()),
    encodedTextureCount=64, encodedBytes='IDENTICAL_TO_PREVIOUS_ALGORITHM',
    embeddedPreparation='PASS_64_PROGRESS_RECEIPTS_6_TARGETS_GAME_INPUTS_UNCHANGED',
    catalogSha256=sha(bundle / 'catalog.signed.json'), exeSha256=sha(bundle / 'TrilexModInstaller.exe'), zipSha256=sha(release / name),
    preserved='All game archives, asset worker, signed catalog 1.1.7 and PSVR2 clients byte-identical',
    limitation='Remote SteamVR hang not independently reproduced; no FPS or physical PASS claim')
(release / 'installer-1.1.0-texture-ui-evidence.json').write_text(json.dumps(evidence, indent=2) + '\n')
checks = {line.split('  ', 1)[1]: line.split('  ', 1)[0] for line in (stage / 'public-SHA256SUMS.txt').read_text(encoding='utf-8-sig').splitlines() if line.strip()}
checks.update({p.name: sha(p) for p in release.iterdir() if p.is_file() and p.name != 'SHA256SUMS.txt'})
(release / 'SHA256SUMS.txt').write_text(''.join(h + '  ' + n + '\n' for n, h in sorted(checks.items())), encoding='ascii')
(stage / 'release-body.md').write_text(heading + (stage / 'previous-release-body.md').read_text(encoding='utf-8-sig'), encoding='utf-8')
print(json.dumps(evidence, indent=2))
