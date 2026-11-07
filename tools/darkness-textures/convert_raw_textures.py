#!/usr/bin/env python3
"""
Convert raw Evolution Engine texture streams (extracted by QuickBMS from toc/cache)
into clean PNG images, and optionally re-encode PNGs back into Evolution Engine's
16 KB planar-split DXT1/DXT5 mipmap stream.

Evolution Engine (The Darkness II) texture stream layout:
  1. Each 16,384-byte (0x4000) compression chunk (and the final tail chunk < 16 KB)
     stores DXT blocks in planar (de-interleaved) order for higher LZ compression:
       - DXT1 (8 B/block):
           [0 .. N*4)     : N x 4-byte RGB565 endpoints (c0, c1)
           [N*4 .. N*8)   : N x 4-byte 2-bit selector indices
       - DXT5 (16 B/block):
           [0 .. N*2)     : N x 2-byte alpha endpoints (a0, a1)
           [N*2 .. N*8)   : N x 6-byte 3-bit alpha selector indices
           [N*8 .. N*12)  : N x 4-byte RGB565 color endpoints (c0, c1)
           [N*12 .. N*16) : N x 4-byte 2-bit color selector indices
  2. Mipmaps are stored SMALLEST-TO-LARGEST (1x1 -> 2x2 -> ... -> WxH), so the
     highest-resolution mip level always occupies the LAST `top_mip_bytes` of the
     de-interleaved stream (`data[len(data) - top_mip_bytes :]`).

Usage:
    python tools/convert_raw_textures.py --source extracted_textures/source --dest extracted_textures/converted
"""

import os
import struct
import argparse
from pathlib import Path
from PIL import Image


def _unpack565(data):
    r = (data >> 11) & 0x1F
    g = (data >> 5) & 0x3F
    b = data & 0x1F
    return (r * 255 // 31, g * 255 // 63, b * 255 // 31)


def _interp565(c0, c1, factor):
    r = c0[0] + (c1[0] - c0[0]) * factor // 3
    g = c0[1] + (c1[1] - c0[1]) * factor // 3
    b = c0[2] + (c1[2] - c0[2]) * factor // 3
    return (r, g, b)


def _dxt1_colors(c0_raw, c1_raw):
    c0 = _unpack565(c0_raw)
    c1 = _unpack565(c1_raw)
    if c0_raw > c1_raw:
        return [
            (c0[0], c0[1], c0[2], 255),
            (c1[0], c1[1], c1[2], 255),
            _interp565(c0, c1, 1) + (255,),
            _interp565(c0, c1, 2) + (255,),
        ]
    else:
        return [
            (c0[0], c0[1], c0[2], 255),
            (c1[0], c1[1], c1[2], 255),
            ((c0[0] + c1[0]) // 2, (c0[1] + c1[1]) // 2, (c0[2] + c1[2]) // 2, 255),
            (0, 0, 0, 0),
        ]


def _dxt5_alphas(a0, a1, index_bits):
    if a0 > a1:
        alphas = [a0, a1]
        for i in range(1, 7):
            alphas.append((a0 * (7 - i) + a1 * i) // 6)
    else:
        alphas = [a0, a1]
        for i in range(1, 5):
            alphas.append((a0 * (5 - i) + a1 * i) // 4)
        alphas += [0, 255]
    indices = []
    bits = index_bits
    for _ in range(16):
        indices.append(bits & 0x7)
        bits >>= 3
    return [alphas[i] for i in indices]


def decompress_dxt1_block(data, offset):
    c0_raw = struct.unpack_from('<H', data, offset)[0]
    c1_raw = struct.unpack_from('<H', data, offset + 2)[0]
    indices_raw = struct.unpack_from('<I', data, offset + 4)[0]
    colors = _dxt1_colors(c0_raw, c1_raw)
    result = []
    bits = indices_raw
    for _ in range(16):
        result.append(colors[bits & 0x3])
        bits >>= 2
    return result


def decompress_dxt5_block(data, offset):
    a0 = data[offset]
    a1 = data[offset + 1]
    alpha_bits = (
        data[offset + 2]
        | (data[offset + 3] << 8)
        | (data[offset + 4] << 16)
        | (data[offset + 5] << 24)
        | (data[offset + 6] << 32)
        | (data[offset + 7] << 40)
    )
    alphas = _dxt5_alphas(a0, a1, alpha_bits)
    c0_raw = struct.unpack_from('<H', data, offset + 8)[0]
    c1_raw = struct.unpack_from('<H', data, offset + 10)[0]
    indices_raw = struct.unpack_from('<I', data, offset + 12)[0]
    colors = _dxt1_colors(c0_raw, c1_raw)
    result = []
    bits = indices_raw
    for i in range(16):
        r, g, b, _ = colors[bits & 0x3]
        bits >>= 2
        result.append((r, g, b, alphas[i]))
    return result


def decompress_dxt1_level(data, offset, width, height):
    blocks_w = (width + 3) // 4
    blocks_h = (height + 3) // 4
    img = Image.new('RGBA', (width, height))
    px = img.load()
    for by in range(blocks_h):
        for bx in range(blocks_w):
            block = decompress_dxt1_block(data, offset)
            offset += 8
            for py in range(4):
                for px_ in range(4):
                    x = bx * 4 + px_
                    y = by * 4 + py
                    if x < width and y < height:
                        px[x, y] = block[py * 4 + px_]
    return img, offset


def decompress_dxt5_level(data, offset, width, height):
    blocks_w = (width + 3) // 4
    blocks_h = (height + 3) // 4
    img = Image.new('RGBA', (width, height))
    px = img.load()
    for by in range(blocks_h):
        for bx in range(blocks_w):
            block = decompress_dxt5_block(data, offset)
            offset += 16
            for py in range(4):
                for px_ in range(4):
                    x = bx * 4 + px_
                    y = by * 4 + py
                    if x < width and y < height:
                        px[x, y] = block[py * 4 + px_]
    return img, offset


def deinterleave_dxt1_stream(data, chunk_bytes=16384):
    """Reassemble planar 16KB Evolution Engine DXT1 chunks into standard 8B DXT1 blocks."""
    out = bytearray(len(data))
    pos = 0
    while pos < len(data):
        rem = min(chunk_bytes, len(data) - pos)
        n = rem // 8
        if n == 0:
            out[pos:pos + rem] = data[pos:pos + rem]
            break
        o_c = pos
        o_i = pos + n * 4
        for i in range(n):
            dst = pos + i * 8
            out[dst:dst + 4] = data[o_c + i * 4:o_c + i * 4 + 4]
            out[dst + 4:dst + 8] = data[o_i + i * 4:o_i + i * 4 + 4]
        pos += rem
    return bytes(out)


def deinterleave_dxt5_stream(data, chunk_bytes=16384):
    """Reassemble planar 16KB Evolution Engine DXT5 chunks into standard 16B DXT5 blocks."""
    out = bytearray(len(data))
    pos = 0
    while pos < len(data):
        rem = min(chunk_bytes, len(data) - pos)
        n = rem // 16
        if n == 0:
            out[pos:pos + rem] = data[pos:pos + rem]
            break
        o_a0 = pos
        o_ai = pos + n * 2
        o_c0 = pos + n * 8
        o_ci = pos + n * 12
        for i in range(n):
            dst = pos + i * 16
            out[dst:dst + 2] = data[o_a0 + i * 2:o_a0 + i * 2 + 2]
            out[dst + 2:dst + 8] = data[o_ai + i * 6:o_ai + i * 6 + 6]
            out[dst + 8:dst + 12] = data[o_c0 + i * 4:o_c0 + i * 4 + 4]
            out[dst + 12:dst + 16] = data[o_ci + i * 4:o_ci + i * 4 + 4]
        pos += rem
    return bytes(out)


def dxt1_level_size(w, h):
    return ((w + 3) // 4) * ((h + 3) // 4) * 8


def dxt5_level_size(w, h):
    return ((w + 3) // 4) * ((h + 3) // 4) * 16


def dxt_chain_size(w, h, block_bytes):
    total = 0
    while True:
        total += ((w + 3) // 4) * ((h + 3) // 4) * block_bytes
        if w == 1 and h == 1:
            break
        w = max(1, w // 2)
        h = max(1, h // 2)
    return total


# Exact Evolution Engine mip-chain byte sizes -> (format, width, height)
KNOWN_LAYOUTS = []
for _w, _h in [
    (64, 64), (128, 64), (64, 128), (128, 128),
    (256, 128), (128, 256), (256, 256),
    (512, 256), (256, 512), (512, 512),
    (1024, 512), (512, 1024), (1024, 1024),
    (2048, 1024), (1024, 2048), (2048, 2048),
]:
    KNOWN_LAYOUTS.append(('DXT1', _w, _h, dxt_chain_size(_w, _h, 8), dxt1_level_size(_w, _h)))
    KNOWN_LAYOUTS.append(('DXT1_SINGLE', _w, _h, dxt1_level_size(_w, _h), dxt1_level_size(_w, _h)))
    KNOWN_LAYOUTS.append(('DXT5', _w, _h, dxt_chain_size(_w, _h, 16), dxt5_level_size(_w, _h)))
    KNOWN_LAYOUTS.append(('DXT5_SINGLE', _w, _h, dxt5_level_size(_w, _h), dxt5_level_size(_w, _h)))


def convert_file(src_path):
    with open(src_path, 'rb') as f:
        data = f.read()

    size = len(data)
    if size == 0:
        return None, 'empty file'

    # 1. Match exact Evolution Engine DXT1/DXT5 mip-chain or single-level stream
    # Prefer DXT1 square/2:1 chains before DXT5 unless size uniquely requires DXT5
    for fmt, w, h, chain_bytes, top_bytes in KNOWN_LAYOUTS:
        # Allow 0, 8, 16, or 24 bytes of trailing/leading alignment metadata
        diff = size - chain_bytes
        if diff in (0, 8, 16, 24, 32) and size >= top_bytes:
            if fmt.startswith('DXT1'):
                fixed = deinterleave_dxt1_stream(data[:chain_bytes], 16384)
                img, _ = decompress_dxt1_level(fixed, len(fixed) - top_bytes, w, h)
                return img.convert('RGB'), f'{fmt} {w}x{h} (planar16k, tail_mip={top_bytes})'
            else:
                fixed = deinterleave_dxt5_stream(data[:chain_bytes], 16384)
                img, _ = decompress_dxt5_level(fixed, len(fixed) - top_bytes, w, h)
                return img, f'{fmt} {w}x{h} (planar16k, tail_mip={top_bytes})'

    # 2. Uncompressed fallback
    for w, h in [(64, 64), (128, 128), (256, 256), (512, 512), (1024, 1024)]:
        if size == w * h * 4:
            img = Image.frombytes('RGBA', (w, h), data, 'raw')
            return img, f'RGBA8 {w}x{h}'

    return None, f'unrecognized size={size}'


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--source', required=True, help='source folder with category subdirs')
    ap.add_argument('--dest', required=True, help='destination folder for converted PNGs')
    ap.add_argument('--diffuse-only', action='store_true', default=True,
                    help='convert only diffuse (_d) textures (default: True)')
    args = ap.parse_args()

    source = Path(args.source)
    dest = Path(args.dest)

    cats = [d.name for d in sorted(source.iterdir()) if d.is_dir()]
    total_ok = 0
    total_fail = 0

    for cat in cats:
        cat_src = source / cat
        cat_dest = dest / cat
        cat_dest.mkdir(parents=True, exist_ok=True)

        files = sorted(cat_src.iterdir())
        ok = fail = 0
        for f in files:
            if not f.is_file():
                continue
            if args.diffuse_only and not f.stem.lower().endswith('_d'):
                continue
            img, info = convert_file(str(f))
            if img is not None:
                out_path = cat_dest / (f.stem + '.png')
                img.save(str(out_path), 'PNG', compress_level=6)
                ok += 1
            else:
                fail += 1
                print(f'  FAIL {cat}/{f.name}: {info}')
        total_ok += ok
        total_fail += fail
        print(f'{cat}: {ok} converted, {fail} failed -> {cat_dest}')

    print(f'\nTotal: {total_ok} converted, {total_fail} failed')


if __name__ == '__main__':
    main()
