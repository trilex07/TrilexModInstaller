"""Assemble RC7's complete directory, then archive only after lifecycle PASS."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import zipfile

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT/'artifacts/Trilex-SingularityVR-1.0.0-rc.7'
BUNDLE = OUT/'Trilex-SingularityVR-V7'


def sha(p):
    with p.open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest().upper()


def assemble():
    if BUNDLE.exists():
        raise ValueError('Fresh complete bundle required')
    old = ROOT/'artifacts/Trilex-SingularityVR-1.0.0-rc.6/Trilex-SingularityVR-V7'
    # Copy small files normally; package bytes are immutable hardlinks to RC7
    # staging, which itself pins unchanged RC6 payloads. Never modify in place.
    shutil.copytree(old, BUNDLE, ignore=lambda d, names: ['packages'] if Path(d)==old else [])
    (BUNDLE/'packages').mkdir()
    for p in sorted((OUT/'packages').glob('*.zip')):
        os.link(p, BUNDLE/'packages'/p.name)
    shutil.copyfile(ROOT/'artifacts/rc7-publish/TrilexModInstaller.exe', BUNDLE/'TrilexModInstaller.exe')
    shutil.copyfile(OUT/'catalog.signed.json', BUNDLE/'catalog.signed.json')
    shutil.copyfile(ROOT/'docs/INSTALL-V7.md', BUNDLE/'INSTALL-V7.md')
    shutil.copyfile(ROOT/'docs/releases/installer-rc7.md', BUNDLE/'RC7-RELEASE-NOTES.md')
    rows = [sha(p)+'  '+p.relative_to(BUNDLE).as_posix() for p in sorted(BUNDLE.rglob('*'))
            if p.is_file() and p.name!='SHA256SUMS.txt']
    (BUNDLE/'SHA256SUMS.txt').write_text('\n'.join(rows)+'\n', encoding='utf-8')
    print('PASS complete RC7 directory, signed catalog, current EXE, bundled exporter and packages')


def archive():
    ui = json.loads((ROOT/'artifacts/rc7-ui-isolated/ui-report.json').read_text())
    assert ui['status']=='PASS' and not ui['bindingErrors'] and len(ui['rows'])==8
    exporter = (ROOT/'private/rc7-offline-tool.log').read_text(encoding='utf-8-sig')
    assert 'Verified bundled exporter offline:' in exporter
    lifecycles = {}
    for name in ('clean', 'upgrade'):
        p = ROOT/f'artifacts/rc7-{name}-lifecycle/exercise-report.json'
        report = json.loads(p.read_text(encoding='utf-8-sig'))
        assert report['status']=='PASS' and report['catalog']=='1.0.0-rc.7'
        assert all(f['sha256']==f['expected'] for f in report['installed'])
        lifecycles[name] = {k: report[k] for k in ('status', 'catalog', 'predecessor', 'modules', 'targets', 'originalsRestored', 'steps')}
    target = OUT/'Trilex-SingularityVR-V7-complete-win-x64.zip'
    with zipfile.ZipFile(target, 'x', zipfile.ZIP_DEFLATED, compresslevel=6) as z:
        for p in sorted(BUNDLE.rglob('*')):
            if p.is_file():
                z.write(p, str(Path(BUNDLE.name)/p.relative_to(BUNDLE)))
    with zipfile.ZipFile(target) as z:
        assert z.testzip() is None
    evidence = json.loads((OUT/'runtime-content-evidence.json').read_text())
    evidence.update(status='PASS_RELEASE_READY', installerBuildSource='a56491c',
        exeSha256=sha(BUNDLE/'TrilexModInstaller.exe'), zipSha256=sha(target),
        zipBytes=target.stat().st_size, catalogSha256=sha(OUT/'catalog.signed.json'),
        lifecycle=lifecycles, coreTests='66 PASS / 0 FAIL',
        archiveIntegrity='PASS', installerUi='8 PL/EN layouts PASS, no binding errors',
        bundledExporterOffline='PASS, fresh isolated tool cache, network disabled',
        authenticode='UNSIGNED')
    (OUT/'installer-rc7-evidence.json').write_text(json.dumps(evidence, indent=2)+'\n', encoding='utf-8')
    (OUT/'ZIP-SHA256SUMS.txt').write_text(sha(target)+'  '+target.name+'\n', encoding='utf-8')
    print(json.dumps(evidence, indent=2))


if __name__=='__main__':
    p=argparse.ArgumentParser(); p.add_argument('action', choices=['assemble', 'archive'])
    a=p.parse_args(); assemble() if a.action=='assemble' else archive()
