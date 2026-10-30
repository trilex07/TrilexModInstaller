"""Publish compact evidence only after real signed bundle lifecycle checks pass."""
import hashlib
import json
from pathlib import Path

root=Path(__file__).resolve().parents[1]
build=root/'artifacts/Trilex-SingularityVR-1.0.0-rc.5'
def read(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest().upper()
lifecycles={}
for name in ['clean','upgrade']:
    report=read(root/f'artifacts/rc5-{name}-lifecycle/exercise-report.json')
    assert report['status']=='PASS' and report['catalog']=='1.0.0-rc.5'
    assert all(f['sha256']==f['expected'] for f in report['installed'])
    lifecycles[name]={k:report[k] for k in ['status','catalog','predecessor','modules','targets','originalsRestored','steps','steamAccess','physicalAcceptance']}
ui=read(root/'artifacts/rc5-ui-smoke/ui-report.json')
assert ui['status']=='PASS' and not ui['bindingErrors'] and len(ui['rows'])==8
core=read(root/'artifacts/rc5-core-fixtures/test-report.json')
assert core['passed']==64 and core['failed']==0
evidence=dict(runtimeSource='e814f137675b8ab075c4adb10c1fc9fb1eca5f4a',
    gameplayDllSha256='147C3297211CFA6CA6B964E29FBBD79538B7EE69D7BC155861B66F67B24BDC96',
    zipSha256=sha(build/'Trilex-SingularityVR-V7-complete-win-x64.zip'),
    exeSha256=sha(build/'Trilex-SingularityVR-V7/TrilexModInstaller.exe'),
    signedCatalogSha256=sha(build/'catalog.signed.json'),
    coreTests='64 PASS',producerTests='14 PASS',publishedExeLayouts='8 PL/EN PASS',
    lifecycle=lifecycles,resourcePins=204,changedResources=25,
    gameplayHudAcceptance='USER CONFIRMED on 2026-10-30',cleanInstallHmdAcceptance='PENDING',
    steamWrites='NONE',authenticode='UNSIGNED')
(root/'docs/releases/installer-rc5-evidence.json').write_text(json.dumps(evidence,indent=2)+'\n')
print(json.dumps(evidence,indent=2))
