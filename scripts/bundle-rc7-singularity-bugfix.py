"""Repackage the same RC7 with verified runtime, recipe and preset fixes."""
import hashlib,json,subprocess,zipfile
from pathlib import Path

root=Path(__file__).resolve().parents[1]
out=root/'artifacts/rc7-singularity-bugfix-20261103'
base=root/'artifacts/rc7-explicit-output-exceptions/Trilex-SingularityVR-V7'
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest().upper()
ui=json.loads((root/'artifacts/rc7-bugfix-ui/ui-report.json').read_text())
assert ui['status']=='PASS' and len(ui['rows'])==8 and not ui['bindingErrors']
content=json.loads((out/'content-verification.json').read_text())
assert content['status']=='PASS'
assert 'PASS combined owned-source transaction lifecycle; originals restored: 7' in (root/'private/rc7-bugfix-lifecycle.log').read_text(encoding='utf-8-sig')
paths={p.relative_to(base).as_posix():p for p in base.rglob('*') if p.is_file() and p.name!='SHA256SUMS.txt'}
paths.update({'TrilexModInstaller.exe':root/'artifacts/rc7-bugfix-publish/TrilexModInstaller.exe',
              'catalog.signed.json':out/'catalog.signed.json',
              'RC7-RELEASE-NOTES.md':root/'docs/releases/installer-rc7.md'})
for p in (out/'packages').glob('*.zip'):paths['packages/'+p.name]=p
hashes={n:sha(p) for n,p in paths.items()}
target=out/'Trilex-SingularityVR-V7-complete-win-x64.zip'
with zipfile.ZipFile(target,'x',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
    for n,p in sorted(paths.items()):z.write(p,'Trilex-SingularityVR-V7/'+n)
    z.writestr('Trilex-SingularityVR-V7/SHA256SUMS.txt',''.join(f'{h}  {n}\n' for n,h in sorted(hashes.items())))
with zipfile.ZipFile(target) as z:
    assert z.testzip() is None
    for n,h in hashes.items():
        with z.open('Trilex-SingularityVR-V7/'+n) as f:assert hashlib.file_digest(f,'sha256').hexdigest().upper()==h,n
source=subprocess.check_output(['git','-c','safe.directory=*','rev-parse','HEAD'],cwd=root,text=True).strip()
evidence={
    'status':'OFFLINE_RELEASE_CHECKS_PASS_PHYSICAL_SCOPE_PARTIAL',
    'installerVersion':'1.0.0-rc.7','installerBuildSource':source,
    'installerRevision':'rc7-singularity-bugfix-refresh-20261103',
    'exeSha256':hashes['TrilexModInstaller.exe'],'zipSha256':sha(target),'zipBytes':target.stat().st_size,
    'catalogSha256':hashes['catalog.signed.json'],
    'workerSha256':sha(root/'.build/asset-worker/Trilex.AssetWorker.exe'),
    'runtime':json.loads((out/'runtime-content-evidence.json').read_text()),
    'content':content,'coreTests':'75 PASS / 0 FAIL','producerTests':'15 PASS / 0 FAIL',
    'publishedUi':'8 PL/EN layouts PASS; zero binding errors; no modal-click automation',
    'audioWorker':'Actual packaged worker reproduces accepted bank B8126A75 byte-for-byte',
    'lifecycle':'Scoped three changed modules: install, verify, reinstall, damage, repair, uninstall PASS; seven original banks restored exactly. Fixture does not load game DLLs.',
    'fullLifecycle':'Earlier RC7 full-content lifecycle is historical; not repeated in this refresh',
    'completeZip':'Every member CRC and SHA256 verified against source',
    'physical':'User accepted current weapon attachment, world scale and binocular captions. Right-eye 2percent edge extension PENDING; no fresh whole clean-install PSVR2 test.',
    'compatibility':'Real disc/GOG gameplay NOT RUN; existing explicit consent and output-exception safeguards retained',
    'rights':'Existing declarations unchanged; third-party rights not independently verified',
    'steamWrites':'NONE','gameLaunch':'NONE','authenticode':'UNSIGNED; signed publisher catalog is separate',
}
(out/'installer-rc7-evidence.json').write_text(json.dumps(evidence,indent=2)+'\n')
(out/'ZIP-SHA256SUMS.txt').write_text(f'{sha(target)}  {target.name}\n')
print(json.dumps({k:evidence[k] for k in ('installerBuildSource','exeSha256','zipSha256','zipBytes','catalogSha256')},indent=2))
