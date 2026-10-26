#!/usr/bin/env python3
"""
DDS I/O helpers for the Singularity VR material remaster pipeline.

Real DDS files are written and read back here - no "saved as PNG and called it
DDS". The writer emits a standard 128-byte DDS header plus real DXT1/DXT5
compressed mip levels (the same compression the game ships), and the reader
decodes them again for validation.

Layout notes (verified against the shipped UModel DDS files and the injector):
    file offset 0   : magic 'DDS '
    offset 12        : dwHeight
    offset 16        : dwWidth
    offset 28        : dwMipMapCount
    offset 76        : DDPIXELFORMAT.dwSize (32)
    offset 80        : DDPIXELFORMAT.dwFlags (0x4 = FOURCC)
    offset 84        : DDPIXELFORMAT.dwFourCC ('DXT1' / 'DXT5')
    offset 128       : first mip level data, contiguous, smallest-last
"""

from __future__ import annotations

import io
import struct
from pathlib import Path

import numpy as np
from PIL import Image

MAGIC = b"DDS "
HEADER_SIZE = 128
VALID_FOURCCS = ("DXT1", "DXT5")
# bytes per block for [DXT1, DXT5]
BLOCK_BYTES = {"DXT1": 8, "DXT5": 16}


# --------------------------------------------------------------------------- #
# reading
# --------------------------------------------------------------------------- #
def read_dds_header(path: Path) -> dict:
    """Parse the DDS header. Returns dict with height/width/mip_count/fourcc."""
    data = Path(path).read_bytes()
    if len(data) < HEADER_SIZE or data[:4] != MAGIC:
        raise ValueError(f"not a DDS file: {path}")
    height = struct.unpack_from("<I", data, 12)[0]
    width = struct.unpack_from("<I", data, 16)[0]
    mip_count = struct.unpack_from("<I", data, 28)[0]
    fourcc = data[84:88].decode("latin1")
    if fourcc not in VALID_FOURCCS:
        raise ValueError(f"unsupported fourcc {fourcc!r} in {path}")
    if width == 0 or height == 0:
        raise ValueError(f"degenerate dimensions {width}x{height} in {path}")
    if width % 4 or height % 4:
        raise ValueError(f"non-block-aligned dimensions {width}x{height} in {path}")
    return {
        "width": int(width),
        "height": int(height),
        "mip_count": int(mip_count) if mip_count else 1,
        "fourcc": fourcc,
        "file_size": len(data),
    }


def load_dds(path: Path) -> tuple[Image.Image, dict]:
    """Decode a DDS file back to RGBA pixels plus its parsed header."""
    hdr = read_dds_header(path)
    with Image.open(path) as im:
        img = im.convert("RGBA")
    return img, hdr


# --------------------------------------------------------------------------- #
# writing
# --------------------------------------------------------------------------- #
def _encode_level(img: Image.Image, fourcc: str) -> bytes:
    """Compress one RGBA/RGB level to DXT blocks (real DDS pixel data)."""
    buf = io.BytesIO()
    img.save(buf, format="DDS", pixel_format=fourcc)
    out = buf.getvalue()
    if out[:4] != MAGIC:
        raise RuntimeError("DDS encoder produced unexpected data")
    return out[HEADER_SIZE:]


def save_dds(path: Path, img: Image.Image, fourcc: str, full_chain: bool = True) -> dict:
    """
    Write a real DDS file with a DXT1/DXT5 mip chain.

    full_chain=True  -> chain halves down to 1x1 (matches a complete streamable
                        texture, which is what the game texture owns).
    full_chain=False -> mip0 only (what UModel exports).

    Returns the header dict of the written file.
    """
    if fourcc not in VALID_FOURCCS:
        raise ValueError(f"fourcc must be one of {VALID_FOURCCS}, got {fourcc}")
    img = img.convert("RGBA")
    w, h = img.size

    levels = [img]
    if full_chain:
        while max(w, h) > 1:
            w = max(1, w // 2)
            h = max(1, h // 2)
            # independent channels: game alpha packing (e.g. additive glow as
            # bright RGB with ~zero alpha) must survive downsampling intact
            levels.append(resize_rgba_independent(levels[-1], (w, h), Image.BOX))
    else:
        levels = [img]

    hdr = bytearray(HEADER_SIZE)
    hdr[0:4] = MAGIC
    struct.pack_into("<I", hdr, 4, 124)                      # dwSize
    flags = 0x1 | 0x2 | 0x4 | 0x1000 | 0x80000               # caps|h|w|pf|mipcount
    struct.pack_into("<I", hdr, 8, flags)
    struct.pack_into("<I", hdr, 12, img.size[1])             # height
    struct.pack_into("<I", hdr, 16, img.size[0])             # width
    struct.pack_into("<I", hdr, 20, 0)                        # pitch/linear size
    struct.pack_into("<I", hdr, 24, 0)                        # depth
    struct.pack_into("<I", hdr, 28, len(levels))             # mip map count
    struct.pack_into("<I", hdr, 76, 32)                      # pixelformat size
    struct.pack_into("<I", hdr, 80, 0x4)                     # pixelformat flags FOURCC
    hdr[84:88] = fourcc.encode("latin1")
    struct.pack_into("<I", hdr, 104, 0x1000)                 # caps: TEXTURE
    if len(levels) > 1:
        struct.pack_into("<I", hdr, 108, 0x400000)           # caps2: MIPMAP

    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("wb") as f:
        f.write(hdr)
        for level in levels:
            f.write(_encode_level(level, fourcc))

    return read_dds_header(path)


# --------------------------------------------------------------------------- #
# normal maps: signed downsampling
# --------------------------------------------------------------------------- #
def normal_to_signed(arr: np.ndarray) -> np.ndarray:
    """[0,255] RGBA normal map -> float array with X,Y in [-1,1], Z in [0,1].

    Alpha (channel 3) is returned untouched so callers can put it back exactly.
    """
    a = arr.astype(np.float32)
    xy = (a[:, :, 0:2] / 255.0) * 2.0 - 1.0
    z = a[:, :, 2] / 255.0
    return np.concatenate([xy, z[:, :, None], a[:, :, 3:4]], axis=2)


def signed_to_normal(signed: np.ndarray) -> np.ndarray:
    """Signed normal array -> [0,255] RGBA with unit-length renormalisation."""
    nx, ny = signed[:, :, 0], signed[:, :, 1]
    nz = signed[:, :, 2]
    length = np.sqrt(nx * nx + ny * ny + nz * nz)
    length[length < 1e-6] = 1.0
    nx, ny, nz = nx / length, ny / length, nz / length
    out = np.empty(signed.shape, dtype=np.uint8)
    # (v/255)*2-1 inverted with rounding, so 0.0 maps to 128 like every engine
    out[:, :, 0] = np.clip(np.round((nx * 0.5 + 0.5) * 255.0), 0, 255).astype(np.uint8)
    out[:, :, 1] = np.clip(np.round((ny * 0.5 + 0.5) * 255.0), 0, 255).astype(np.uint8)
    out[:, :, 2] = np.clip(np.round(nz * 255.0), 0, 255).astype(np.uint8)
    out[:, :, 3] = np.clip(np.round(signed[:, :, 3]), 0, 255).astype(np.uint8)
    return out


def downsample_normal(img: Image.Image, size: tuple[int, int]) -> Image.Image:
    """
    Resize a normal map the correct way: X/Y are averaged in signed space, the
    vector is renormalised, and the alpha channel is averaged in place (it is
    often a mask, not a constant). A naive RGB box blur would bend the normals
    and darken the Z channel.
    """
    signed = normal_to_signed(np.asarray(img.convert("RGBA")))
    channels = []
    for c in range(4):                       # X, Y, Z and alpha, all as float
        plane = signed[:, :, c].astype(np.float32)
        small = Image.fromarray(plane, mode="F").resize(size, Image.BOX)
        channels.append(np.asarray(small, dtype=np.float32)[:, :, None])
    blended = np.concatenate(channels, axis=2)
    return Image.fromarray(signed_to_normal(blended), "RGBA")


def resize_rgba_independent(img: Image.Image, size: tuple[int, int],
                            filt=Image.BOX) -> Image.Image:
    """Resize RGBA treating colour and alpha as independent channels.

    PIL's resampling filters (everything except NEAREST) combine the channels:
    pixels with low alpha drag their RGB toward the transparent-black
    background, permanently darkening bright-but-transparent texels (UE3 packs
    additive glow as bright RGB with ~zero alpha, e.g. the E99 bullet). Game
    materials sample RGB and alpha through separate expressions, so filtering
    them independently is the faithful operation. Required for every mip-chain
    and upscale step on colour textures with meaningful alpha.
    """
    img = img.convert("RGBA")
    rgb = img.convert("RGB").resize(size, filt)
    alpha = img.getchannel("A").resize(size, filt)
    out = rgb.convert("RGBA")
    out.putalpha(alpha)
    return out


def make_mip_chain(img: Image.Image, fourcc: str) -> list[Image.Image]:
    """Mip chain that uses signed downsampling for normal maps."""
    levels = [img.convert("RGBA")]
    w, h = img.size
    while max(w, h) > 1:
        w = max(1, w // 2)
        h = max(1, h // 2)
        levels.append(downsample_normal(levels[-1], (w, h)))
    return levels


def save_normal_dds(path: Path, img: Image.Image) -> dict:
    """DXT1 normal map with a signed-downsampled mip chain."""
    levels = make_mip_chain(img.convert("RGBA"), "DXT1")
    hdr = bytearray(HEADER_SIZE)
    hdr[0:4] = MAGIC
    struct.pack_into("<I", hdr, 4, 124)
    struct.pack_into("<I", hdr, 8, 0x1 | 0x2 | 0x4 | 0x1000 | 0x80000)
    struct.pack_into("<I", hdr, 12, img.size[1])
    struct.pack_into("<I", hdr, 16, img.size[0])
    struct.pack_into("<I", hdr, 28, len(levels))
    struct.pack_into("<I", hdr, 76, 32)
    struct.pack_into("<I", hdr, 80, 0x4)
    hdr[84:88] = b"DXT1"
    struct.pack_into("<I", hdr, 104, 0x1000)
    if len(levels) > 1:
        struct.pack_into("<I", hdr, 108, 0x400000)
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("wb") as f:
        f.write(hdr)
        for lvl in levels:
            f.write(_encode_level(_rgb_only_if_opaque(lvl), "DXT1"))
    return read_dds_header(path)


def _rgb_only_if_opaque(img: Image.Image) -> Image.Image:
    """DXT1 keeps constant-255 alpha as plain RGB (matches the shipped maps)."""
    a = np.asarray(img)
    if a[:, :, 3].min() == 255:
        return img.convert("RGB")
    return img


# --------------------------------------------------------------------------- #
# provenance / hashing
# --------------------------------------------------------------------------- #
def fnv1a64_mip0(path: Path) -> int:
    """
    FNV-1a 64 over the compact mip-0 DXT bytes of a DDS file.

    This is the same value the injector computes from the game's locked
    texture, so a matching number means the two byte streams are identical.
    """
    b = Path(path).read_bytes()
    fourcc = b[84:88].decode("latin1")
    w = struct.unpack_from("<I", b, 16)[0]
    h = struct.unpack_from("<I", b, 12)[0]
    if fourcc not in BLOCK_BYTES:
        raise ValueError(f"fnv1a64_mip0: unsupported fourcc {fourcc}")
    blk = BLOCK_BYTES[fourcc]
    need = (w // 4) * blk * (h // 4)
    if 128 + need > len(b):
        raise ValueError("fnv1a64_mip0: file shorter than its mip0")
    hv = 0xCBF29CE484222325
    for x in b[128:128 + need]:
        hv ^= x
        hv = (hv * 0x100000001B3) & 0xFFFFFFFFFFFFFFFF
    return hv


def video_memory_bytes(path: Path) -> int:
    """Video memory the DDS contents occupy, from the actual file contents."""
    hdr = read_dds_header(path)
    blk = BLOCK_BYTES[hdr["fourcc"]]
    w, h = hdr["width"], hdr["height"]
    total = 0
    for _ in range(hdr["mip_count"]):
        total += ((w + 3) // 4) * ((h + 3) // 4) * blk
        w, h = (max(1, w // 2), max(1, h // 2))
    return total


# --------------------------------------------------------------------------- #
# validation
# --------------------------------------------------------------------------- #
def validate_dds(path: Path) -> dict:
    """
    Re-open a written DDS and check it really decodes: header vs pixel data,
    dimensions, format, mip count, channel count and edge continuity.
    Returns a dict of measurements (no pass/fail claims beyond them).
    """
    hdr = read_dds_header(path)
    img, _ = load_dds(path)
    arr = np.asarray(img)
    expected_bytes = video_memory_bytes(path)
    levels = hdr["mip_count"]
    w, h = hdr["width"], hdr["height"]
    blk = BLOCK_BYTES[hdr["fourcc"]]

    # pixel data present for every declared level?
    data_bytes = hdr["file_size"] - HEADER_SIZE
    per_level = 0
    tw, th = w, h
    for _ in range(levels):
        per_level += ((tw + 3) // 4) * ((th + 3) // 4) * blk
        tw, th = max(1, tw // 2), max(1, th // 2)

    # edge continuity: mean absolute difference across the wrap seam. A tiling
    # texture should be roughly as continuous at the seam as it is internally.
    def seam(axis: int) -> float:
        if arr.shape[0 if axis else 1] < 2:
            return 0.0
        if axis == 0:
            return float(np.abs(arr[-1].astype(np.int16) - arr[0].astype(np.int16)).mean())
        return float(np.abs(arr[:, -1].astype(np.int16) - arr[:, 0].astype(np.int16)).mean())

    return {
        "file": str(path),
        "width": w,
        "height": h,
        "fourcc": hdr["fourcc"],
        "mip_count": levels,
        "file_bytes": hdr["file_size"],
        "pixel_data_bytes": data_bytes,
        "declared_mip_bytes": per_level,
        "video_memory_bytes": expected_bytes,
        "decoded_size": [int(img.size[0]), int(img.size[1])],
        "decoded_mode": img.mode,
        "decoded_channels": int(arr.shape[2]),
        "seam_row_mean_abs": round(seam(0), 3),
        "seam_col_mean_abs": round(seam(1), 3),
    }
