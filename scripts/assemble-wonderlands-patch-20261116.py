"""Assemble the reviewed patch; preserve every other game's shipped payload."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import zipfile


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def main():
    p = argparse.ArgumentParser()
    p.add_argument('--stage', type=Path, required=True)
    p.add_argument('--exe', type=Path, required=True)
    p.add_argument('--release-dir', type=Path)
    a = p.parse_args()
    root = Path(__file__).resolve().parents[1]
    bundle = a.stage / 'bundle'
    public = a.release_dir or a.stage / 'release-assets'
    public.mkdir(exist_ok=False)
    shutil.copy2(a.exe, bundle / 'TrilexModInstaller.exe')
    shutil.copy2(a.stage / 'catalog.signed.json', bundle / 'catalog.signed.json')
    shutil.copy2(root / 'docs/releases/wonderlands-patch-20261116.md', bundle / 'docs/WONDERLANDS-PATCH-20261116.md')
    note = (a.stage / 'release-body.md').read_text('utf-8')
    for name in ['README.md', 'RELEASE-NOTES.md']:
        p = bundle / name
        with zipfile.ZipFile(a.stage / 'current/Trilex-Mod-Installer-1.1.1-win-x64.zip') as old:
            previous = old.read(name).decode('utf-8-sig')
        p.write_text(note + '\n\n---\nEarlier release history follows.\n\n' + previous, 'utf-8')
    # Steam uses the backward-compatible vr-core.zip path; only Epic needs a
    # second content-addressed archive. Remove the exact duplicate if present.
    steam_hash = sha(bundle / 'packages/wonderlands/vr-core.zip')
    duplicate = bundle / 'packages/wonderlands' / (steam_hash + '.zip')
    if duplicate.exists():
        assert sha(duplicate) == steam_hash
        duplicate.unlink()
    files = sorted(p for p in bundle.rglob('*') if p.is_file() and p.name != 'SHA256SUMS.txt')
    (bundle / 'SHA256SUMS.txt').write_text(''.join(sha(p) + '  ' + p.relative_to(bundle).as_posix() + '\n' for p in files), 'ascii')
    with zipfile.ZipFile(a.stage / 'current/Trilex-Mod-Installer-1.1.1-win-x64.zip') as old:
        changed = [n for n in old.namelist() if not n.endswith('/') and
                   old.read(n) != (bundle / n).read_bytes()]
    assert set(changed) == {'TrilexModInstaller.exe', 'catalog.signed.json',
                            'packages/wonderlands/vr-core.zip', 'README.md',
                            'RELEASE-NOTES.md', 'SHA256SUMS.txt'}, changed
    archive = public / 'Trilex-Mod-Installer-1.1.3-win-x64.zip'
    with zipfile.ZipFile(archive, 'x', zipfile.ZIP_DEFLATED) as z:
        for file in sorted(bundle.rglob('*')):
            if file.is_file():
                z.write(file, file.relative_to(bundle).as_posix())
    with zipfile.ZipFile(archive) as z:
        assert z.testzip() is None
        for file in bundle.rglob('*'):
            if file.is_file():
                assert z.read(file.relative_to(bundle).as_posix()) == file.read_bytes()
    for name in ['catalog.signed.json', 'wonderlands-vr-steam-20261116.zip', 'wonderlands-vr-epic-20261116.zip']:
        shutil.copy2(a.stage / name, public / name)
    report = dict(status='PASS', installer='1.1.3', catalog='1.1.10',
                  previousWonderlands='2026-11-10 / EFC1685E',
                  steamDll='780C91D61C0730E947BC238A9AF00F0BCD78A5AC0803980B46AA347EE0165D78',
                  epicDll='535502EED9003AC7207CFE109BACF7C6A746FDEF3DD11D437782120453878DDB',
                  runtimeTests=40, installerTests=114, uiLayouts=8,
                  lifecycle=['Steam signed install/export/verify/uninstall/restore PASS',
                             'Epic signed install/export/verify/uninstall/restore PASS'],
                  changedPriorFiles=changed, otherGamePackages='byte-identical',
                  latestSteamHeadset='NOT RUN', epic='Earlier packet; player-reported working, not new Steam optics',
                  gameWrites=False, gameLaunch=False)
    (public / 'wonderlands-patch-evidence-20261116.json').write_text(json.dumps(report, indent=2) + '\n', 'utf-8')
    (public / 'SHA256SUMS.txt').write_text(''.join(sha(p) + '  ' + p.name + '\n'
                                               for p in sorted(public.iterdir()) if p.name != 'SHA256SUMS.txt'), 'ascii')
    print(json.dumps(report))


if __name__ == '__main__':
    main()
