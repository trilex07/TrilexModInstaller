"""Assemble and byte-audit the installer 1.1.4 bundle (adds Everybody's Gone to the Rapture VR) before upload.

Input layout under --stage (artifacts/installer-1.1.4):
  current/Trilex-Mod-Installer-1.1.3-win-x64.zip + current/SHA256SUMS.txt  (published 1.1.3, hash-checked here)
  bundle/                       extracted 1.1.3 ZIP (rebuilt from current/ when missing)
  publish/TrilexModInstaller.exe self-contained 1.1.4 build
  catalog-1.1.13.signed.json    signed catalog with Rapture (goes inside the bundle)
  RELEASE-BODY.md               PL/EN notes (prepended to README.md / RELEASE-NOTES.md, copied to docs/)
--rapture: artifacts/rapture-20261123 (module ZIP + tester bundle)
--online-catalog: the unchanged signed 1.1.12 catalog published as the release's catalog.signed.json, so that
  1.1.3 apps (which reject any catalog containing Rapture) still verify it and see the 1.1.4 update prompt.
"""
import argparse, hashlib, json, shutil, zipfile
from pathlib import Path

def sha(p): return hashlib.sha256(Path(p).read_bytes()).hexdigest().upper()

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--stage', type=Path, required=True); ap.add_argument('--rapture', type=Path, required=True)
    ap.add_argument('--online-catalog', type=Path, required=True)
    a = ap.parse_args(); r = a.stage; b = r / 'bundle'; public = r / 'release-assets'
    old_zip = r / 'current/Trilex-Mod-Installer-1.1.3-win-x64.zip'
    published = {l.split('  ', 1)[1]: l.split('  ', 1)[0] for l in (r / 'current/SHA256SUMS.txt').read_text('utf-8-sig').splitlines() if l}
    assert sha(old_zip) == published['Trilex-Mod-Installer-1.1.3-win-x64.zip'], 'base is not the published 1.1.3 ZIP'
    assert sha(a.online_catalog) == published['catalog.signed.json'], 'online catalog must be the published 1.1.12 bytes'
    if b.exists(): shutil.rmtree(b)
    with zipfile.ZipFile(old_zip) as z: z.extractall(b)
    public.mkdir(exist_ok=False)

    package = a.rapture / 'rapture-vr-core-20261123.zip'; tester = a.rapture / 'tester/RaptureVR-test-20261123.zip'
    body = (r / 'RELEASE-BODY.md').read_text('utf-8')
    shutil.copy2(r / 'publish/TrilexModInstaller.exe', b / 'TrilexModInstaller.exe')
    shutil.copy2(r / 'catalog-1.1.13.signed.json', b / 'catalog.signed.json')
    (b / 'packages/rapture').mkdir(); shutil.copy2(package, b / 'packages/rapture/vr-core.zip')
    (b / 'docs/RAPTURE-VR-20261123.md').write_text(body, 'utf-8')
    for n in ['README.md', 'RELEASE-NOTES.md']:
        (b / n).write_text(body + '\n\n---\nPrevious release history follows.\n\n' + (b / n).read_text('utf-8'), 'utf-8')
    (b / 'SHA256SUMS.txt').write_text(''.join(sha(x) + '  ' + x.relative_to(b).as_posix() + '\n' for x in sorted(b.rglob('*'))
                                              if x.is_file() and x.name != 'SHA256SUMS.txt'), 'ascii')

    with zipfile.ZipFile(old_zip) as old:
        names = {n for n in old.namelist() if not n.endswith('/')}
        changed = [n for n in sorted(names) if old.read(n) != (b / n).read_bytes()]
        assert set(changed) == {'TrilexModInstaller.exe', 'catalog.signed.json', 'README.md', 'RELEASE-NOTES.md', 'SHA256SUMS.txt'}, changed
        added = {x.relative_to(b).as_posix() for x in b.rglob('*') if x.is_file()} - names
        assert added == {'packages/rapture/vr-core.zip', 'docs/RAPTURE-VR-20261123.md'}, added
    output = public / 'Trilex-Mod-Installer-1.1.4-win-x64.zip'
    with zipfile.ZipFile(output, 'x', zipfile.ZIP_DEFLATED) as z:
        for x in sorted(b.rglob('*')):
            if x.is_file(): z.write(x, x.relative_to(b).as_posix())
    with zipfile.ZipFile(output) as z:
        assert z.testzip() is None
        for x in b.rglob('*'):
            if x.is_file(): assert z.read(x.relative_to(b).as_posix()) == x.read_bytes()
    shutil.copy2(a.online_catalog, public / 'catalog.signed.json')
    shutil.copy2(r / 'catalog-1.1.13.signed.json', public / 'catalog-1.1.13.signed.json')
    shutil.copy2(package, public / package.name); shutil.copy2(tester, public / tester.name)
    (public / 'SHA256SUMS.txt').write_text(''.join(sha(x) + '  ' + x.name + '\n' for x in sorted(public.iterdir()) if x.name != 'SHA256SUMS.txt'), 'ascii')
    evidence = {'status': 'OFFLINE_PACKAGE_PASS', 'base': '1.1.3 ' + published['Trilex-Mod-Installer-1.1.3-win-x64.zip'],
                'changedPriorFiles': changed, 'addedFiles': sorted(added), 'exe': sha(b / 'TrilexModInstaller.exe'),
                'bundleCatalog': sha(b / 'catalog.signed.json'), 'onlineCatalog': sha(public / 'catalog.signed.json'),
                'assets': {x.name: [sha(x), x.stat().st_size] for x in sorted(public.iterdir())}, 'raptureHeadsetAcceptance': 'PENDING'}
    (r / 'assembly-evidence.json').write_text(json.dumps(evidence, indent=1) + '\n', 'utf-8'); print(json.dumps(evidence, indent=1))

if __name__ == '__main__': main()
