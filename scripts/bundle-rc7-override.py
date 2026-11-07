"""Package RC7's explicit output-hash exception UI without changing mod data."""
import hashlib
import json
from pathlib import Path
import shutil
import zipfile

ROOT = Path(__file__).resolve().parents[1]
OLD = ROOT/'artifacts/rc7-reconstruction-diagnostics'
OUT = ROOT/'artifacts/rc7-explicit-output-exceptions'
BUNDLE = OUT/'Trilex-SingularityVR-V7'


def sha(p):
    with p.open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest().upper()


assert not OUT.exists(), 'Fresh output required; previous packages stay intact'
ui = json.loads((ROOT/'artifacts/rc7-override-ui/ui-report.json').read_text())
assert ui['status']=='PASS' and not ui['bindingErrors'] and len(ui['rows'])==8
assert all('reconstruction' in row for row in ui['rows'])
tests = (ROOT/'private/reconstruction-override-tests.log').read_text(encoding='utf-8-sig')
assert '"passed":72,"failed":0' in tests
oldbundle = OLD/BUNDLE.name
shutil.copytree(oldbundle, BUNDLE)
shutil.copyfile(ROOT/'artifacts/rc7-override-publish/TrilexModInstaller.exe', BUNDLE/'TrilexModInstaller.exe')
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
evidence['installerBuildSource'] = '8a5245d69ba66ce8a0a2fa75e68992656c6eb7f5'
evidence['previousExeSha256'] = evidence['exeSha256']
evidence['previousZipSha256'] = evidence['zipSha256']
evidence['exeSha256'] = sha(BUNDLE/'TrilexModInstaller.exe')
evidence['zipSha256'] = sha(target)
evidence['zipBytes'] = target.stat().st_size
evidence['coreTests'] = '72 PASS / 0 FAIL'
evidence['installerRevision'] = 'rc7-explicit-output-exceptions-20261102'
evidence['explicitOutputExceptions'] = {
    'approval':'Per generated file, native Yes/No warning; No is default; no persistent global bypass',
    'scope':'Only final local output mismatch; original/decoded sources, package, game and recipe checks remain strict',
    'receipt':'Expected and actual hash + identity stored; installed hash is actual; exception completion/verify status',
    'transactionTests':'PASS: approve/decline, reapproval for repair/reinstall, cancellation, later refusal before writes, rollback after accepted output was written, exact uninstall originals',
    'publishedExeUi':'8 PL/EN layouts and warning/status producers PASS; native modal interaction not automated',
    'bundleComparison':'PASS: only EXE, release notes and checksum list differ; catalog and all mod files unchanged',
    'lifecycle':'Prior full RC7 lifecycle retained as historical evidence; new isolated real transaction fixtures passed; full 373-target lifecycle not repeated',
    'gogSteamCompatibility':'Mismatch cause not established; accepting output does not prove game compatibility',
    'steamAccess':'NONE',
}
(OUT/'installer-rc7-evidence.json').write_text(json.dumps(evidence,indent=2)+'\n',encoding='utf-8')
(OUT/'ZIP-SHA256SUMS.txt').write_text(sha(target)+'  '+target.name+'\n',encoding='utf-8')
print(json.dumps({k:evidence[k] for k in ('exeSha256','zipSha256','zipBytes','coreTests','explicitOutputExceptions')},indent=2))
