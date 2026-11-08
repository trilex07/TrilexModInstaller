"""Reconstruct closed wrist hands from the player's stock compressed resource."""
import hashlib
import json
from pathlib import Path
import struct
import subprocess
import sys
import closed_wrist_mesh
import hands_only_asset as stock
from polish_pack import require, sha

RESOURCE = stock.RESOURCE.lstrip('/')
ACCEPTED_BODY = 'ce0b8c981cc45af2d918603d482c69e3721ff68365c1594c28d3438a07acfe46'


def extract(inputs, stem, output, bundle):
    # Only the one owned resource is copied into the decoder's tiny private cache.
    toc = bytearray(Path(inputs[stem + '.toc']).read_bytes())
    cache_path = Path(inputs[stem + '.cache'])
    require(toc[:4] == bytes.fromhex('4ec66718') and (len(toc) - 8) % 96 == 0, 'FP arms TOC layout')
    directories, found = [''], []
    for pos in range(8, len(toc), 96):
        off, stamp, stored, unpacked, padding, parent, raw = struct.unpack_from('<QQIIII64s', toc, pos)
        require(parent < len(directories), 'FP arms TOC parent')
        name = raw.split(b'\0')[0].decode('utf-8')
        require(name and name not in ('.', '..') and not any(c in name for c in '/\\:'), 'FP arms TOC name')
        path = directories[parent] + '/' + name
        if off == 0xffffffffffffffff:
            directories.append(path)
        elif stamp and path.lstrip('/') == RESOURCE:
            found.append((pos, off, stored, unpacked))
    require(len(found) == 1, 'FP arms resource missing or ambiguous')
    at, offset, packed, size = found[0]
    current_entry = (offset, packed, size)
    require(0 < packed <= 1024 * 1024 and 0 < size <= 1024 * 1024 and offset + packed <= cache_path.stat().st_size,
        'FP arms resource exceeds bounded source range')
    if stem == 'B.Misc':
        offset, packed, size = stock.ENTRY_OFFSET, stock.ENTRY_SIZE, 206112
    with cache_path.open('rb') as stream:
        stream.seek(offset)
        payload = stream.read(packed)
    require(len(payload) == packed, 'FP arms source range')
    if stem == 'B.Misc':
        require(stock.sha(payload) == stock.ENTRY_SHA, 'Stock compressed FP arms mismatch')
    for pos in range(8, len(toc), 96):
        if struct.unpack_from('<Q', toc, pos)[0] != 0xffffffffffffffff and pos != at:
            struct.pack_into('<Q', toc, pos + 8, 0)
    struct.pack_into('<Q', toc, at, 0)
    struct.pack_into('<III', toc, at + 16, packed, size, 0)
    mini = output / stem
    mini.mkdir()
    (mini / 'toc.toc').write_bytes(toc)
    (mini / 'toc.cache').write_bytes(payload)
    decoded = mini / 'decoded'
    decoded.mkdir()
    result = subprocess.run([str(bundle / 'quickbms.exe'), '-o', str(bundle / 'darkness2.bms'),
        str(mini / 'toc.toc'), str(decoded)], capture_output=True, timeout=30)
    require(result.returncode == 0, 'FP arms extraction failed: ' + result.stderr.decode(errors='replace')[-600:])
    return (decoded / RESOURCE).read_bytes(), (at, current_entry)


def prepare(input_json, output, bundle):
    inputs = json.loads(Path(input_json).read_text(encoding='utf-8-sig'))
    require(set(inputs) == {s + e for s in ('H.Misc', 'B.Misc') for e in ('.cache', '.toc')}, 'FP arms inputs')
    output = Path(output)
    output.mkdir()
    header, _ = extract(inputs, 'H.Misc', output, bundle)
    original, (at, (offset, packed, size)) = extract(inputs, 'B.Misc', output, bundle)
    body, geometry = closed_wrist_mesh.patch(header, original)
    require(stock.sha(body) == ACCEPTED_BODY, 'Reconstructed hands differ from accepted local model')
    source = Path(inputs['B.Misc.cache'])
    toc_source = Path(inputs['B.Misc.toc'])
    toc = bytearray(toc_source.read_bytes())
    with source.open('rb') as stream:
        digest = hashlib.file_digest(stream, 'sha256')
        stream.seek(offset)
        current = stream.read(packed)
    before = digest.hexdigest()
    append = b'' if packed == size == len(body) and current == body else body
    if append:
        require((offset, packed, size) == (stock.ENTRY_OFFSET, stock.ENTRY_SIZE, len(body)) or
            (packed == size == len(body) and stock.sha(current) in {
                stock.sha(stock.patch(header, original)[0]), stock.PREVIOUS_CLOSED_WRIST_SHA}),
            'Existing FP arms resource is modified by another mod')
        struct.pack_into('<Q', toc, at, source.stat().st_size)
        struct.pack_into('<III', toc, at + 16, len(body), len(body), 0)
    digest.update(append)
    (output / 'B.Misc.append').write_bytes(append)
    (output / 'B.Misc.toc').write_bytes(toc)
    rows = [dict(path='Cache.Windows/B.Misc.cache', sourceBytes=source.stat().st_size,
        append='B.Misc.append', sha256=digest.hexdigest(), beforeSha256=before),
        dict(path='Cache.Windows/B.Misc.toc', payload='B.Misc.toc', sha256=stock.sha(toc), beforeSha256=sha(toc_source))]
    (output / 'result.json').write_text(json.dumps(rows), encoding='utf-8')
    (output / 'geometry.json').write_text(json.dumps(geometry), encoding='utf-8')
    print('Closed wrist hands reconstructed from owned game; SHA256=' + stock.sha(body), flush=True)


if __name__ == '__main__':
    prepare(sys.argv[1], sys.argv[2], Path(getattr(sys, '_MEIPASS', Path(__file__).parent)))
