"""Repackage RC7 installer only; preserve signed catalog, packages and tools."""
import hashlib,json,subprocess,zipfile
from pathlib import Path

root=Path(__file__).resolve().parents[1]
old=root/'artifacts/rc7-explicit-output-exceptions'
base=old/'Trilex-SingularityVR-V7'
out=root/'artifacts/rc7-unverified-game-warning'
out.mkdir(exist_ok=False)
exe=root/'artifacts/unsupported-build-publish/TrilexModInstaller.exe'
report=json.loads((root/'tests/Trilex.Installer.Tests/bin/Release/net10.0/fixtures/test-report.json').read_text())
ui=json.loads((root/'artifacts/unsupported-build-portable-ui/ui-report.json').read_text())
assert report['passed']==75 and report['failed']==0
assert ui['status']=='PASS' and len(ui['rows'])==8 and not ui['bindingErrors']
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest().upper()
replacements={'TrilexModInstaller.exe':exe,'RC7-RELEASE-NOTES.md':root/'docs/releases/installer-rc7.md'}
paths={p.relative_to(base).as_posix():p for p in base.rglob('*') if p.is_file() and p.name!='SHA256SUMS.txt'}
paths.update(replacements)
hashes={name:sha(p) for name,p in paths.items()}
target=out/'Trilex-SingularityVR-V7-complete-win-x64.zip'
with zipfile.ZipFile(target,'x',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
    for name,p in sorted(paths.items()):z.write(p,'Trilex-SingularityVR-V7/'+name)
    z.writestr('Trilex-SingularityVR-V7/SHA256SUMS.txt',''.join(f'{h}  {n}\n' for n,h in sorted(hashes.items())))
with zipfile.ZipFile(target) as z:
    assert z.testzip() is None
    for name,expected in hashes.items():
        with z.open('Trilex-SingularityVR-V7/'+name) as f:assert hashlib.file_digest(f,'sha256').hexdigest().upper()==expected,name
    for name,p in paths.items():
        if name not in replacements:assert hashes[name]==sha(base/name),name
commit=subprocess.check_output(['git','-c',f'safe.directory={root.as_posix()}','rev-parse','HEAD'],cwd=root,text=True).strip()
evidence=json.loads((old/'installer-rc7-evidence.json').read_text())
evidence.update(previousInstallerBuildSource=evidence['installerBuildSource'],previousExeSha256=evidence['exeSha256'],previousZipSha256=evidence['zipSha256'])
evidence.update(exeSha256=sha(exe),zipSha256=sha(target),zipBytes=target.stat().st_size,coreTests='75 PASS / 0 FAIL',installerRevision='rc7-unverified-executable-consent-20261103',
    lifecycleScope='Retained earlier signed-content lifecycle results; unverified-build transaction tests are included in current 75 core tests; no real disc/GOG validation')
evidence.update(installerBuildSource=commit,installerSha256=sha(exe),completeZipSha256=sha(target),
    executableWarning={'status':'OFFLINE PASS; real GOG/disc gameplay NOT RUN','consent':'PL/EN modal, default No; exact directory and EXE SHA256; logged and receipted',
    'coreTests':'75 PASS / 0 FAIL','portableUi':'8 PL/EN layouts PASS; zero binding errors; modal click not automated',
    'protected':'catalog/package/source checks, backups, journal, uninstall; no EXE patch or Steam writes'},
    unchangedBundleFiles=len(paths)-len(replacements))
(out/'installer-rc7-evidence.json').write_text(json.dumps(evidence,indent=2)+'\n')
(out/'ZIP-SHA256SUMS.txt').write_text(f'{sha(target)}  {target.name}\n')
print(json.dumps({'source':commit,'exeSha256':sha(exe),'zipSha256':sha(target),'unchangedFiles':len(paths)-len(replacements)},indent=2))
