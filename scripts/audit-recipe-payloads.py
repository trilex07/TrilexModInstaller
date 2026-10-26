"""Inspect reconstruction instructions without copying or publishing their payloads.

Literal bytes are distributed data, even inside a game-specific patch. This audit
measures them; it never determines or grants redistribution rights.
"""
import argparse
import json
import struct
from pathlib import Path


def inspect(data):
    if len(data) < 20:
        raise ValueError('Truncated SVRP header')
    magic, version, target, count = struct.unpack_from('<4sIQI', data)
    if magic != b'SVRP' or version != 1:
        raise ValueError('Unsupported SVRP')
    cursor = 20
    copied = literal = ogg = 0
    for _ in range(count):
        if cursor >= len(data):
            raise ValueError('Truncated opcode')
        op = data[cursor]
        cursor += 1
        if op == 0:
            if cursor + 12 > len(data):
                raise ValueError('Truncated copy')
            _, length = struct.unpack_from('<QI', data, cursor)
            cursor += 12
            copied += length
        elif op == 1:
            if cursor + 4 > len(data):
                raise ValueError('Truncated literal length')
            length, = struct.unpack_from('<I', data, cursor)
            cursor += 4
            if cursor + length > len(data):
                raise ValueError('Truncated literal')
            ogg += data[cursor:cursor + length].count(b'OggS')
            cursor += length
            literal += length
        else:
            raise ValueError('Unknown opcode')
        if copied + literal > target:
            raise ValueError('Target overflow')
    if cursor != len(data) or copied + literal != target:
        raise ValueError('Trailing data or target mismatch')
    return dict(targetBytes=target, copiedBytes=copied, literalBytes=literal,
                literalOggPageMarkers=ogg)


def audit(root):
    modules = []
    for name in ('polish-dubbing', 'audio-remaster', 'ui-hud'):
        folder = root / name
        manifest = json.loads((folder / 'module.json').read_text(encoding='utf-8-sig'))
        rows = []
        for entry in manifest['files']:
            for recipe in entry['recipe']:
                path = folder / 'stage' / recipe['entry']
                rows.append(dict(path=entry['path'], **inspect(path.read_bytes())))
        modules.append(dict(id=name, files=rows,
                            literalBytes=sum(r['literalBytes'] for r in rows),
                            copiedBytes=sum(r['copiedBytes'] for r in rows),
                            literalOggPageMarkers=sum(r['literalOggPageMarkers'] for r in rows),
                            rightsVerified=manifest['rightsVerified']))
    return dict(schema='trilex-recipe-payload-audit-v1', modules=modules,
                conclusion='Literal media is included in recipes; encoding does not establish redistribution rights.',
                gameAccess='NONE', payloadExported=False, rightsGranted=False)


if __name__ == '__main__':
    p = argparse.ArgumentParser()
    p.add_argument('root', type=Path)
    p.add_argument('output', type=Path)
    a = p.parse_args()
    result = audit(a.root)
    a.output.write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    print(json.dumps([{k: m[k] for k in ('id', 'literalBytes', 'literalOggPageMarkers', 'rightsVerified')}
                      for m in result['modules']]))
