"""Refresh the published 1.1.5 bundle with the tested Singularity packages."""
import argparse
import base64
import hashlib
import json
from pathlib import Path
import zipfile


def sha(data):
    return hashlib.sha256(data).hexdigest().upper()


def catalog(data):
    return json.loads(base64.b64decode(json.loads(data)['payload'], validate=True))


def core_hash(metadata):
    game = next(g for g in metadata['games'] if g['id'] == 'singularity')
    return next(m['archiveSha256'] for m in game['modules'] if m['id'] == 'vr-core')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for key in ('base', 'signed', 'core', 'output'):
        parser.add_argument('--' + key, type=Path, required=True)
    args = parser.parse_args()
    predecessors = {
        'C52DB6BA4B7A2B77E2859F3004DA8734B1FE205FDB97D0AF3AB0F98EAB9370AD': '1.1.25',
        'E8734187E13C6C665481F529A280AD98F247CAC0B247B73D7E593E4051EE2CD2': '1.1.26',
        '9E7A3CAA12B9DA17EA628FEC63B360BE2F94E6EA9A098D241CB8F8E444BA81B4': '1.1.27',
        'D6FF2164FAAC07842AD197A08A94256F88253E24E89A17149153F45984CD80AD': '1.1.28',
        '8EE5DB96F64C50DF09607B82FB8A4485A00C4CA43AEF5C1240D583BA3EACC36D': '1.1.29',
    }
    version = predecessors.get(sha(args.base.read_bytes()))
    signed = args.signed.read_bytes()
    if not version or version != catalog(signed)['version']:
        raise ValueError('Unexpected published predecessor or catalog')
    notice = (
        '## Singularity VR update for Oculus Touch (November 27, 2026)\n\n'
        'Select Singularity VR and include its portable settings. The mod now reads the Touch pointing pose '
        'separately and aligns empty hands with the controllers. Tested at the helicopter checkpoint in the '
        'OpenXR simulator. Physical Quest 2 verification and the reported floating silhouette remain open. '
        'This bundle leaves headset resolution discovery to the local runtime. Remove any old manual low-resolution '
        'launch override if you want automatic sizing.\n\n'
    ).encode()
    if version == '1.1.26':
        notice = (
            '## Singularity 720p VR menu correction (November 27, 2026)\n\n'
            'At 1280x720 the main menu and load-game list now render once per eye, '
            'including when the game starts at its default resolution. Select VR core and portable settings. '
            'The Oculus Touch hand update from catalog 1.1.25 is included.\n\n'
        ).encode()
    if version == '1.1.27':
        notice = (
            '## Singularity VR rifle effect update (November 27, 2026)\n\n'
            'The game activates both native muzzle effects together when a rifle first becomes available. '
            'This shortens the observed hitch when loading the Research Facility save. '
            'Select VR core to update. The Quest controller, menu and resolution changes are included.\n\n'
        ).encode()
    core = args.core.read_bytes()
    if version == '1.1.28':
        notice = (
            '## Singularity VR glove rendering update (November 27, 2026)\n\n'
            'Install VR core and portable settings. This update keeps the same hand geometry and pose '
            'when drawing glove depth and color, and prevents cutscene objects from being learned as hands. '
            'Tested on the Steam game with simulated Touch controllers at the burning helicopter. '
            'The extra helicopter silhouette remains under investigation.\n\n'
        ).encode()
    if version == '1.1.29':
        notice = (
            '## Singularity VR gravity decal update (November 27, 2026)\n\n'
            'Select VR core to update. The TMD gravity decal filter now checks which shader is actually active '
            'when the game draws the effect. Two-handed support also releases when the native game reports that '
            'the player is unarmed after a cutscene. Existing portable settings remain compatible.\n\n'
        ).encode()
    if sha(core) != core_hash(catalog(signed)):
        raise ValueError('Core archive differs from new catalog')
    with zipfile.ZipFile(args.base) as source:
        if sha(source.read('packages/vr-core.zip')) != core_hash(catalog(source.read('catalog.signed.json'))):
            raise ValueError('Wrong Singularity core in base bundle')
        changes = {'catalog.signed.json': signed, 'packages/vr-core.zip': core}
        for name in ('README.md', 'RELEASE-NOTES.md'):
            changes[name] = notice + source.read(name)
        checks = {}
        with zipfile.ZipFile(args.output, 'x', zipfile.ZIP_DEFLATED, compresslevel=3) as dest:
            for info in source.infolist():
                name = info.filename
                if name == 'SHA256SUMS.txt':
                    continue
                data = changes[name] if name in changes else source.read(info)
                checks[name] = sha(data)
                dest.writestr(info, data)
            sums = ''.join(f'{digest}  {name}\n' for name, digest in sorted(checks.items()))
            dest.writestr('SHA256SUMS.txt', sums)
        with zipfile.ZipFile(args.output) as dest:
            if dest.namelist() != source.namelist() or dest.read('SHA256SUMS.txt').decode() != sums:
                raise ValueError('Output manifest or archive listing mismatch')
            for name, digest in checks.items():
                if sha(dest.read(name)) != digest:
                    raise ValueError('Output mismatch for ' + name)
    print(sha(args.output.read_bytes()), args.output.name)


if __name__ == '__main__':
    main()
