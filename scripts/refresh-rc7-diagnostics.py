"""Refresh the RC7 installer only; keep the previous bundle for rollback."""
import hashlib
import json
from pathlib import Path
import shutil
import zipfile

ROOT = Path(__file__).resolve().parents[1]
OLD = ROOT/'artifacts/Trilex-SingularityVR-1.0.0-rc.7'
OUT = ROOT/'artifacts/rc7-reconstruction-diagnostics'
BUNDLE = OUT/'Trilex-SingularityVR-V7'


def sha(p):
    with p.open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest().upper()


assert not OUT.exists(), 'Fresh output required; previous releases stay intact'
ui = json.loads((ROOT/'artifacts/rc7-diagnostics-ui/ui-report.json').read_text())
assert ui['status']=='PASS' and not ui['bindingErrors'] and len(ui['rows'])==8
tests = (ROOT/'private/reconstruction-diagnostics-tests.log').read_text(encoding='utf-8-sig')
assert '"passed":67,"failed":0' in tests
oldbundle = OLD/BUNDLE.name
shutil.copytree(oldbundle, BUNDLE)
shutil.copyfile(ROOT/'artifacts/rc7-diagnostics-publish/TrilexModInstaller.exe', BUNDLE/'TrilexModInstaller.exe')
shutil.copyfile(ROOT/'docs/releases/installer-rc7.md', BUNDLE/'RC7-RELEASE-NOTES.md')
changed = {'TrilexModInstaller.exe', 'RC7-RELEASE-NOTES.md', 'SHA256SUMS.txt'}
for p in oldbundle.rglob('*'):
    if p.is_file() and p.relative_to(oldbundle).as_posix() not in changed:
        assert sha(p)==sha(BUNDLE/p.relative_to(oldbundle)), p
rows = [sha(p)+'  '+p.relative_to(BUNDLE).as_posix() for p in sorted(BUNDLE.rglob('*'))
        if p.is_file() and p.name!='SHA256SUMS.txt']
(BUNDLE/'SHA256SUMS.txt').write_text('\n'.join(rows)+'\n', encoding='utf-8')
target = OUT/'Trilex-SingularityVR-V7-complete-win-x64.zip'
with zipfile.ZipFile(target, 'x', zipfile.ZIP_DEFLATED, compresslevel=6) as z:
    for p in sorted(BUNDLE.rglob('*')):
        if p.is_file(): z.write(p, str(Path(BUNDLE.name)/p.relative_to(BUNDLE)))
with zipfile.ZipFile(target) as z:
    assert z.testzip() is None
    assert hashlib.sha256(z.read(BUNDLE.name+'/TrilexModInstaller.exe')).hexdigest().upper()==sha(BUNDLE/'TrilexModInstaller.exe')
evidence = json.loads((OLD/'installer-rc7-evidence.json').read_text())
evidence['previousInstallerBuildSource'] = evidence['installerBuildSource']
evidence['installerBuildSource'] = '6820320e1a88c08c994d4c0e85cd308525e9ac31'
evidence['previousExeSha256'] = evidence['exeSha256']
evidence['previousZipSha256'] = evidence['zipSha256']
evidence['exeSha256'] = sha(BUNDLE/'TrilexModInstaller.exe')
evidence['zipSha256'] = sha(target)
evidence['zipBytes'] = target.stat().st_size
evidence['coreTests'] = '67 PASS / 0 FAIL, includes both reconstruction output paths'
evidence['diagnosticRefresh'] = {
    'date':'2026-11-02',
    'scope':'Output mismatch context only; source checks and recipes unchanged',
    'bundleComparison':'PASS: only installer EXE, release notes and checksum list differ',
    'newExeUi':'8 PL/EN layouts PASS; no binding errors',
    'lifecycle':'Prior RC7 lifecycle evidence retained; not rerun for error-message-only change',
    'gogCompatibility':'UNVERIFIED; not claimed fixed',
}
(OUT/'installer-rc7-evidence.json').write_text(json.dumps(evidence,indent=2)+'\n',encoding='utf-8')
(OUT/'ZIP-SHA256SUMS.txt').write_text(sha(target)+'  '+target.name+'\n',encoding='utf-8')
print(json.dumps({k:evidence[k] for k in ('exeSha256','zipSha256','zipBytes','coreTests','diagnosticRefresh')},indent=2))
