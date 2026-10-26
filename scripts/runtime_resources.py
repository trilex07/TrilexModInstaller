"""Pinned runtime dependency closure, independent of the original V7 deploy delta.

The deploy delta was not a clean-install bill of materials. This list records
the files actually consumed by the accepted runtime, including older assets.
No recursive backup copying and no claim of headset or third-party rights PASS.
"""
import hashlib
import json
import struct
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest().upper()


def inventory():
    return json.loads((ROOT / 'inventory/v7-runtime-resources.json').read_text())


def validate_catalog(data):
    catalog = json.loads(data)
    if catalog.get('schema') != 'singularityvr-weapon-wheel-catalog-v1':
        raise ValueError('Unsupported wheel catalog')
    expected = {5799: 'gun-centurion', 11387: 'gun-valkyrie',
                8297: 'gun-spikeshot', 11389: 'gun-kasimov'}
    sectors = catalog.get('sectors', [])
    indices = [sector['index'] for sector in sectors]
    if len(indices) != len(set(indices)):
        raise ValueError('Duplicate wheel sector')
    sprites = {sector['gun_verts']: sector['atlas_icon'] for sector in sectors
               if sector['gun_verts']}
    if sprites != expected or set(catalog.get('cycle_order', [])) != set(expected):
        raise ValueError('Wheel catalog gun/sprite mismatch')
    # cycle_order_verified=False is preserved: direct selection resolves native
    # inventory; it must not turn a proposed cycle order into a verified one.
    return catalog


def validate_dds(path):
    with path.open('rb') as stream:
        data = stream.read(128)
    if len(data) != 128 or data[:4] != b'DDS ':
        raise ValueError('Invalid runtime DDS: ' + str(path))
    height, width = struct.unpack_from('<II', data, 12)
    levels = max(1, struct.unpack_from('<I', data, 28)[0])
    fmt = data[84:88]
    if not (0 < width <= 8192 and 0 < height <= 8192) or fmt not in (b'DXT1', b'DXT5'):
        raise ValueError('Unsupported runtime DDS: ' + str(path))
    if levels > max(width, height).bit_length():
        raise ValueError('Invalid mip count')
    block = 8 if fmt == b'DXT1' else 16
    expected = 128 + sum(max(1, ((width >> i) + 3) // 4) *
                         max(1, ((height >> i) + 3) // 4) * block for i in range(levels))
    if path.stat().st_size != expected:
        raise ValueError('DDS byte budget mismatch: ' + str(path))
    return dict(width=width, height=height, levels=levels, fourcc=fmt.decode())


def require_closure(modules):
    owners = {}
    for module in modules:
        for file in module['files']:
            if file['kind'] != 'ini':
                owners[file['path'].lower()] = (module['id'], file)
    for row in inventory()['files']:
        actual = owners.get(row['path'].lower())
        if actual is None:
            raise ValueError('Missing accepted runtime resource: ' + row['path'])
        owner, file = actual
        if owner != row['module'] or file.get('sha256', '').upper() != row['sha256']:
            raise ValueError('Runtime resource identity/owner mismatch: ' + row['path'])
        if row.get('beforeSha256') and file.get('beforeSha256') != row['beforeSha256']:
            raise ValueError('Runtime original pin missing: ' + row['path'])
    return len(inventory()['files'])
