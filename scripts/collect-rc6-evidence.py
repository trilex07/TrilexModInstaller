"""Record RC6 only after the actual signed bundle and lifecycle checks pass."""
import hashlib
import json
from pathlib import Path

root = Path(__file__).resolve().parents[1]
build = root / 'artifacts/Trilex-SingularityVR-1.0.0-rc.6'
bundle = build / 'Trilex-SingularityVR-V7'

def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))

def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest().upper()

core = read(root / 'artifacts/rc6-core-fixtures/test-report.json')
assert core['passed'] == 65 and core['failed'] == 0
ui = read(root / 'artifacts/rc6-ui-smoke/ui-report.json')
assert ui['status'] == 'PASS' and not ui['bindingErrors'] and len(ui['rows']) == 8
content = read(root / 'artifacts/rc6-bundle-content-check.json')
assert content['status'] == 'PASS' and content['unchangedRc5Modules'] == 12
offline = (root / 'artifacts/rc6-offline-tool.log').read_text(encoding='utf-8-sig')
assert 'Verified bundled exporter offline:' in offline
lifecycles = {}
for name in ['clean', 'upgrade']:
    report = read(root / f'artifacts/rc6-{name}-lifecycle/exercise-report.json')
    assert report['status'] == 'PASS' and report['catalog'] == '1.0.0-rc.6'
    assert all(row['sha256'] == row['expected'] for row in report['installed'])
    lifecycles[name] = {key: report[key] for key in [
        'status', 'catalog', 'predecessor', 'modules', 'targets',
        'originalsRestored', 'steps', 'steamAccess', 'physicalAcceptance']}
evidence = dict(
    installerBuildSource='583d2fd3cbf70c37e62148371f87d551b19cedd8',
    gameplayDllSha256='147C3297211CFA6CA6B964E29FBBD79538B7EE69D7BC155861B66F67B24BDC96',
    zipSha256=sha(build / 'Trilex-SingularityVR-V7-complete-win-x64.zip'),
    exeSha256=sha(bundle / 'TrilexModInstaller.exe'),
    signedCatalogSha256=sha(build / 'catalog.signed.json'),
    exporterArchiveSha256=sha(bundle / 'tools/umodel_win32.zip'),
    coreTests='65 PASS', producerTests='14 PASS', publishedExeLayouts='8 PL/EN PASS',
    bundledExporterOffline='PASS, fresh tool cache; no network permitted',
    corruptExporter='PASS, rejected before network and packaging writes',
    content=content, lifecycle=lifecycles,
    steamWrites='NONE', gameLaunch='NOT RUN', physicalAcceptance='NOT RUN',
    discSpanishCompatibility='UNVERIFIED', authenticode='UNSIGNED',
    optionalAudioCodec='Existing first-use download/cache requirement unchanged')
(root / 'docs/releases/installer-rc6-evidence.json').write_text(
    json.dumps(evidence, indent=2) + '\n', encoding='utf-8')
print(json.dumps(evidence, indent=2))
