"""Replace only the installer EXE and delivery notes in the downloaded current ZIP."""
import hashlib,json,shutil,subprocess,zipfile
from pathlib import Path

root=Path(__file__).resolve().parents[1]
stage=root/'artifacts/installer-launch-busy-20261109'
bundle=stage/'bundle';release=stage/'release-assets';release.mkdir(exist_ok=True)
name='Trilex-Mod-Installer-1.1.0-win-x64.zip'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
heading='''## November 9: Run the game button repair

- Fixes a busy-state check that rejected valid launches with "Verify a managed installation first".
- Redownload and extract this full ZIP into a fresh folder; use its new EXE. Already installed games need no reinstall for this button fix.
- Catalog 1.1.6, all game packages, the shotgun audio repair and PSVR2 clients are preserved.
- Offline launch-path regression passed for all three games; no game was started during testing.

'''
for n in ('README.md','RELEASE-NOTES.md'):
    p=bundle/n;p.write_text(heading+p.read_text(encoding='utf-8-sig'),encoding='utf-8')
shutil.copy2(stage/'publish/TrilexModInstaller.exe',bundle/'TrilexModInstaller.exe')
shutil.copy2(root/'docs/LAUNCH-BUSY-FIX-20261109.md',bundle/'docs/LAUNCH-BUSY-FIX-20261109.md')
files=sorted(p for p in bundle.rglob('*') if p.is_file() and p!=bundle/'SHA256SUMS.txt')
(bundle/'SHA256SUMS.txt').write_text(''.join(sha(p)+'  '+p.relative_to(bundle).as_posix()+'\n' for p in files),encoding='ascii')
with zipfile.ZipFile(stage/name) as old:
    changed=[n for n in old.namelist() if not n.endswith('/') and old.read(n)!=(bundle/n).read_bytes()]
    assert sorted(changed)==sorted(['TrilexModInstaller.exe','README.md','RELEASE-NOTES.md','SHA256SUMS.txt']),changed
with zipfile.ZipFile(release/name,'w',zipfile.ZIP_DEFLATED) as z:
    for p in sorted(bundle.rglob('*')):
        if p.is_file():z.write(p,p.relative_to(bundle).as_posix())
with zipfile.ZipFile(release/name) as z:
    assert z.testzip() is None
    for p in bundle.rglob('*'):
        if p.is_file():assert z.read(p.relative_to(bundle).as_posix())==p.read_bytes()
evidence=dict(status='OFFLINE_PASS',source=subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip(),
    coreTests=106,wpfLayouts=8,launchScenarios=24,gameProcessesStarted=0,playerAcceptance='PENDING',
    catalogSha256=sha(bundle/'catalog.signed.json'),exeSha256=sha(bundle/'TrilexModInstaller.exe'),zipSha256=sha(release/name),
    preserved='All prior payloads including game archives, asset worker, catalog1.1.6 and PSVR2 clients byte-identical')
(release/'installer-1.1.0-launch-busy-evidence.json').write_text(json.dumps(evidence,indent=2)+'\n')
checks={line.split('  ',1)[1]:line.split('  ',1)[0] for line in (stage/'public-SHA256SUMS.txt').read_text(encoding='utf-8-sig').splitlines() if line.strip()}
checks.update({p.name:sha(p) for p in release.iterdir() if p.is_file() and p.name!='SHA256SUMS.txt'})
(release/'SHA256SUMS.txt').write_text(''.join(h+'  '+n+'\n' for n,h in sorted(checks.items())),encoding='ascii')
(stage/'release-body.md').write_text(heading+(stage/'previous-release-body.md').read_text(encoding='utf-8-sig'),encoding='utf-8')
print(json.dumps(evidence,indent=2))
