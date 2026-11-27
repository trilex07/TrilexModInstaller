"""Prepare the Singularity modules from a verified launcher 1.1.5 catalog."""
import argparse
import base64
import hashlib
import io
import json
from pathlib import Path
import subprocess
import zipfile


def digest(data):
    return hashlib.sha256(data).hexdigest().upper()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('tools', 'catalog', 'core', 'dll', 'out'):
        parser.add_argument('--' + name, type=Path, required=True)
    parser.add_argument('--version', required=True)
    args = parser.parse_args()
    key = Path(__file__).resolve().parents[1] / 'catalog/trusted-publisher.pem'
    subprocess.run([str(args.tools.resolve()), 'verify', str(args.catalog), str(key)], check=True)
    envelope = json.loads(args.catalog.read_text(encoding='utf-8-sig'))
    catalog = json.loads(base64.b64decode(envelope['payload'], validate=True))
    if tuple(map(int, args.version.split('.'))) <= tuple(map(int, catalog['version'].split('.'))):
        raise ValueError('New catalog version must exceed the source version')
    game = next(g for g in catalog['games'] if g['id'] == 'singularity')
    modules = {m['id']: m for m in game['modules']}
    core = modules['vr-core']
    settings = modules['portable-settings']
    dll = args.dll.read_bytes()
    builds = {
        'D265B8843C10D98BFB748D809C871FD03C0472B972B7D0F6F151727F7ADA5D9D': ('1.1.24', 'v7-20261127-quest2'),
        '2B6B0A8972992087E69E564E898211BFFB6C9F6358B53824ACE500EC987EE817': ('1.1.25', 'v8-20261127-quest2-menu'),
    }
    build = builds.get(digest(dll))
    if not build or build[0] != catalog['version']:
        raise ValueError('DLL differs from the tested release candidate')
    if digest(args.core.read_bytes()) != core['archiveSha256']:
        raise ValueError('Original package differs from the signed catalog')
    args.out.mkdir(parents=True, exist_ok=True)
    output = args.out / ('singularity-vr-core-' + build[1].split('-', 1)[1] + '.zip')
    if output.exists() or (args.out / 'catalog.unsigned.json').exists():
        raise FileExistsError('Choose a fresh output directory')
    archive = io.BytesIO()
    with zipfile.ZipFile(args.core) as source, zipfile.ZipFile(archive, 'w') as dest:
        entries = {f['entry']: f for f in core['files'] if f['entry']}
        if set(source.namelist()) != set(entries) or len(source.infolist()) != len(entries):
            raise ValueError('Unexpected package contents')
        expanded = 0
        for info in source.infolist():
            data = source.read(info)
            file = entries[info.filename]
            if digest(data) != file['sha256']:
                raise ValueError('Hash mismatch for ' + info.filename)
            if info.filename == 'Binaries/d3d9.dll':
                data = dll
                file['sha256'] = digest(data)
            dest.writestr(info, data)
            expanded += len(data)
    core.update(version=build[1], downloadBytes=len(archive.getvalue()),
                expandedBytes=expanded, archiveSha256=digest(archive.getvalue()),
                downloadUrl='https://github.com/trilex07/TrilexModInstaller/releases/download/installer-1.1.5/' + output.name)
    ini = next(f['iniValues'] for f in settings['files'] if f['path'] == 'Binaries/SingularityVR.ini')
    ini.update({'Input/PreferAimPose': '1', 'Render/AlignFreeHands': '1'})
    if build[0] == '1.1.25':
        ini['Render/NativeMenuTargetSizeFix'] = '1'
    # These are runtime caches for the local headset. Keep the user's measured values on upgrade.
    for field in ('Render/AutoResX', 'Render/AutoResY'):
        ini.pop(field, None)
    settings['version'] = core['version']
    catalog['version'] = args.version
    output.write_bytes(archive.getvalue())
    (args.out / 'catalog.unsigned.json').write_text(json.dumps(catalog, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


if __name__ == '__main__':
    main()
