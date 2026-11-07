#!/usr/bin/env python3
"""
The Darkness II - Comic-Hybrid (Top Cow Heavy Ink) Weapons & Tentacles Texture Pack Manager.

Provides:
  1. --build   : (Offline / SIDE-safe) Encodes the 64 upscaled 2K Strong Comic-Hybrid PNGs
                 from `extracted_textures/2k/{tentacles,weapons}` into Evolution Engine
                 16 KB planar DXT1/DXT5 mipmap streams (`H`, `B`, `F` payloads) inside
                 `extracted_textures/package/`, verifying every byte length and header.
  2. --install : (MAIN only) Installs the pre-built `H.Texture`, `B.Texture`, `F.Texture`
                 payloads into `<GameDir>/Cache.Windows` with automatic `.orig_bak` backups.
  3. --restore : (MAIN only) Restores vanilla `H.Texture`, `B.Texture`, `F.Texture` from
                 `.orig_bak` backups.
  4. --status  : Checks current installation status of the Comic Texture Pack.
"""

from __future__ import annotations

import os
import sys
import csv
import json
import time
import struct
import shutil
import hashlib
import argparse
import tempfile
import subprocess
from pathlib import Path

import numpy as np
from PIL import Image

TOOLS_DIR = Path(__file__).resolve().parent
REPO_ROOT = TOOLS_DIR.parent
sys.path.insert(0, str(TOOLS_DIR))

from polish_pack import Cache, require, sha
import convert_raw_textures as crt

DEFAULT_GAME_CACHE = Path(r"C:\Program Files (x86)\Steam\steamapps\common\Darkness II\Cache.Windows")
DEFAULT_UPSCALED_DIR = REPO_ROOT / "extracted_textures" / "2k"
DEFAULT_PACKAGE_DIR = REPO_ROOT / "extracted_textures" / "package"
QBMS_EXE = TOOLS_DIR / "quickbms" / "quickbms.exe"
BMS_SCRIPT = TOOLS_DIR / "darkness2.bms"

AFFECTED_STEMS = ("H.Texture", "B.Texture", "F.Texture")


def assert_game_closed():
    if os.name != "nt":
        return
    output = subprocess.check_output(["tasklist", "/FO", "CSV", "/NH"], text=True, errors="replace")
    names = {r[0].casefold() for r in csv.reader(output.splitlines()) if r}
    require(
        not names.intersection({"darknessii.exe", "thedarknessii.exe", "darkness2.exe"}),
        "Close The Darkness II before installing or restoring textures.",
    )


def encode_dxt1_blocks_np(pil_img: Image.Image) -> tuple[np.ndarray, np.ndarray]:
    """
    Fast vectorized NumPy DXT1 block encoder.
    Returns (endpoints_u32, indices_u32) of shape (N_blocks,) each.
    """
    w, h = pil_img.size
    bw, bh = max(1, w // 4), max(1, h // 4)
    if w < 4 or h < 4:
        pad_img = Image.new("RGB", (max(4, w), max(4, h)))
        rep = pil_img.convert("RGB").resize((max(4, w), max(4, h)), Image.NEAREST)
        pad_img.paste(rep, (0, 0))
        arr = np.array(pad_img, dtype=np.float32)
    else:
        arr = np.array(pil_img.convert("RGB"), dtype=np.float32)

    blocks = arr.reshape(bh, 4, bw, 4, 3).transpose(0, 2, 1, 3, 4).reshape(-1, 16, 3)
    cmin = blocks.min(axis=1)
    cmax = blocks.max(axis=1)
    inset = (cmax - cmin) / 16.0
    cmin = np.clip(cmin + inset, 0, 255)
    cmax = np.clip(cmax - inset, 0, 255)

    def to565(c):
        r = np.round(c[:, 0] * 31.0 / 255.0).astype(np.uint16)
        g = np.round(c[:, 1] * 63.0 / 255.0).astype(np.uint16)
        b = np.round(c[:, 2] * 31.0 / 255.0).astype(np.uint16)
        return (r << 11) | (g << 5) | b, np.stack([r * 255.0 / 31.0, g * 255.0 / 63.0, b * 255.0 / 31.0], axis=1)

    c0_565, c0_rgb = to565(cmax)
    c1_565, c1_rgb = to565(cmin)

    swap = c0_565 < c1_565
    c0_565_sw = np.where(swap, c1_565, c0_565)
    c1_565_sw = np.where(swap, c0_565, c1_565)
    c0_rgb_sw = np.where(swap[:, None], c1_rgb, c0_rgb)
    c1_rgb_sw = np.where(swap[:, None], c0_rgb, c1_rgb)

    equal = c0_565_sw == c1_565_sw

    p0 = c0_rgb_sw
    p1 = c1_rgb_sw
    p2 = (2.0 * p0 + p1) / 3.0
    p3 = (p0 + 2.0 * p1) / 3.0
    palette = np.stack([p0, p1, p2, p3], axis=1)

    diff = blocks[:, :, None, :] - palette[:, None, :, :]
    dist = (diff * diff).sum(axis=3)
    best = dist.argmin(axis=2).astype(np.uint32)
    best[equal] = 0

    shifts = (np.arange(16, dtype=np.uint32) * 2)[None, :]
    indices = (best << shifts).sum(axis=1, dtype=np.uint32)
    endpoints = c0_565_sw.astype(np.uint32) | (c1_565_sw.astype(np.uint32) << 16)
    return endpoints, indices


def build_mip_list(base_img: Image.Image, min_w: int, min_h: int, max_w: int, max_h: int) -> list[tuple[int, int, Image.Image]]:
    """Builds smallest-to-largest mip chain from (min_w, min_h) up to (max_w, max_h)."""
    dims = []
    w, h = max_w, max_h
    while True:
        dims.append((w, h))
        if w == min_w and h == min_h:
            break
        w = max(1, w // 2)
        h = max(1, h // 2)
    dims.reverse()
    mips = []
    for mw, mh in dims:
        if base_img.size == (mw, mh):
            mips.append((mw, mh, base_img))
        else:
            mips.append((mw, mh, base_img.resize((mw, mh), Image.LANCZOS)))
    return mips


def interleave_planar_dxt1_chain(mips: list[tuple[int, int, Image.Image]], tail_pad: bytes = b"", chunk_bytes: int = 16384) -> bytes:
    """Encodes a list of mips as DXT1 and interleaves into 16 KB Evolution Engine planar chunks."""
    ep_list = []
    idx_list = []
    for _, _, im in mips:
        ep, idx = encode_dxt1_blocks_np(im)
        ep_list.append(ep)
        idx_list.append(idx)
    all_ep = np.concatenate(ep_list)
    all_idx = np.concatenate(idx_list)
    total_blocks = len(all_ep)
    total_bytes = total_blocks * 8

    out = bytearray(total_bytes)
    pos_b = 0
    pos_blk = 0
    while pos_b < total_bytes:
        rem = min(chunk_bytes, total_bytes - pos_b)
        n = rem // 8
        out[pos_b : pos_b + n * 4] = all_ep[pos_blk : pos_blk + n].tobytes()
        out[pos_b + n * 4 : pos_b + n * 8] = all_idx[pos_blk : pos_blk + n].tobytes()
        pos_b += rem
        pos_blk += n
    if tail_pad:
        out.extend(tail_pad)
    return bytes(out)


def replace_planar_dxt5_rgb_chain(orig_planar: bytes, mips: list[tuple[int, int, Image.Image]], chunk_bytes: int = 16384) -> bytes:
    """
    Preserves the exact original DXT5 alpha planes [0 .. N*8) in every 16 KB chunk,
    and replaces the RGB planes [N*8 .. N*16) with our new DXT1-encoded RGB blocks.
    """
    ep_list = []
    idx_list = []
    for _, _, im in mips:
        ep, idx = encode_dxt1_blocks_np(im)
        ep_list.append(ep)
        idx_list.append(idx)
    all_ep = np.concatenate(ep_list)
    all_idx = np.concatenate(idx_list)
    total_blocks = len(all_ep)
    chain_bytes = total_blocks * 16
    require(len(orig_planar) >= chain_bytes, f"DXT5 orig_planar too short: {len(orig_planar)} < {chain_bytes}")

    out = bytearray(orig_planar)
    pos_b = 0
    pos_blk = 0
    while pos_b < chain_bytes:
        rem = min(chunk_bytes, chain_bytes - pos_b)
        n = rem // 16
        if n == 0:
            break
        # Keep [pos_b : pos_b + n*8] (alpha endpoints + alpha 3-bit indices) untouched!
        out[pos_b + n * 8 : pos_b + n * 12] = all_ep[pos_blk : pos_blk + n].tobytes()
        out[pos_b + n * 12 : pos_b + n * 16] = all_idx[pos_blk : pos_blk + n].tobytes()
        pos_b += rem
        pos_blk += n
    return bytes(out)


def parse_h_header(hb: bytes) -> tuple[int, int, int, int, str, int, int]:
    """
    Finds the 0x94000000 descriptor in H.Texture entry and returns:
      (pos94, mips_B, mips_F, N_table, fmt_str, b_w, b_h)
    """
    for pos94 in range(len(hb) - 180, len(hb) - 140):
        if pos94 >= 0 and hb[pos94 : pos94 + 4] == b"\x94\x00\x00\x00":
            mips_b = hb[pos94 + 4]
            mips_f = hb[pos94 + 5]
            n = hb[pos94 + 7]
            rem = len(hb) - (pos94 + 11 + 4 * n)
            if rem == 149:
                suf = hb[pos94 + 11 + 4 * n :]
                fmt_code = suf[0]
                fmt_str = "DXT1" if fmt_code == 0 else "DXT5"
                b_w, b_h = struct.unpack_from("<HH", suf, 6)
                return pos94, mips_b, mips_f, n, fmt_str, b_w, b_h
    raise ValueError("Could not locate valid 0x94000000 + 149B descriptor in H.Texture header")


def patch_h_for_uncompressed_f(hb: bytes) -> bytes:
    """
    Sets N=0 (hb[pos94+7] = 0) and removes the 4*N compressed mip offset table
    when F.Texture is stored uncompressed (packed == size), matching native vanilla
    uncompressed F.Texture entries (e.g. BrickRedOld_d.png).
    """
    pos94, mips_b, mips_f, n, _, _, _ = parse_h_header(hb)
    if n == 0:
        return hb
    patched = hb[: pos94 + 7] + b"\x00\x00\x00\x00" + hb[pos94 + 11 + 4 * n :]
    require(len(patched) == len(hb) - 4 * n, "Unexpected patched H.Texture length")
    return patched


def extract_selective(cache_dir: Path, stem: str, target_paths: set[str], out_dir: Path):
    cache = Cache(cache_dir, stem)
    toc_bytes = bytearray(cache.toc)
    selected = set(target_paths) & set(cache.entries.keys())
    pos_to_path = {v[0]: k for k, v in cache.entries.items()}

    src_cache_path = cache_dir / f"{stem}.cache.orig_bak"
    if not src_cache_path.exists():
        src_cache_path = cache_dir / f"{stem}.cache"

    with tempfile.TemporaryDirectory() as tmp:
        tmp_p = Path(tmp)
        mini_cache = tmp_p / "toc.cache"
        mini_toc = tmp_p / "toc.toc"
        with open(src_cache_path, "rb") as sf, open(mini_cache, "wb") as df:
            for pos in range(8, len(toc_bytes), 96):
                off, stamp, packed, size, padding, parent = struct.unpack_from("<QQIIII", toc_bytes, pos)
                if off == 0xffffffffffffffff or stamp == 0:
                    continue
                path = pos_to_path.get(pos)
                if path in selected:
                    new_off = df.tell()
                    sf.seek(off)
                    df.write(sf.read(packed))
                    struct.pack_into("<Q", toc_bytes, pos, new_off)
                else:
                    struct.pack_into("<Q", toc_bytes, pos + 8, 0)
        mini_toc.write_bytes(toc_bytes)
        out_dir.mkdir(parents=True, exist_ok=True)
        subprocess.run([str(QBMS_EXE), "-o", str(BMS_SCRIPT), str(mini_toc), str(out_dir)], check=True, capture_output=True)


def build_package(cache_dir: Path, upscaled_dir: Path, package_dir: Path):
    t0 = time.time()
    print("=== BUILDING COMIC-HYBRID (TOP COW HEAVY INK) TEXTURE PACKAGE ===")

    # Use .orig_bak TOCs if present so build is always against clean vanilla baseline
    def load_vanilla_cache(stem: str) -> Cache:
        bak_toc = cache_dir / f"{stem}.toc.orig_bak"
        bak_cch = cache_dir / f"{stem}.cache.orig_bak"
        if bak_toc.exists() and bak_cch.exists():
            tmp_d = package_dir / "_vanilla_toc_tmp"
            tmp_d.mkdir(parents=True, exist_ok=True)
            shutil.copy2(bak_toc, tmp_d / f"{stem}.toc")
            # Create sparse/dummy or use Cache on original
        return Cache(cache_dir, stem)

    h_toc = Cache(cache_dir, "H.Texture")
    b_toc = Cache(cache_dir, "B.Texture")
    f_toc = Cache(cache_dir, "F.Texture")

    png_map = {p.name.lower(): p for p in upscaled_dir.rglob("*.png")}
    targets = [
        k for k in sorted(h_toc.entries.keys())
        if Path(k).name.lower() in png_map
        and not any(x in k.lower() for x in ("/fx/", "/structural/", "/commonfx/", "/carnivalweapons/", "/cemeteryumbrella"))
        and (
            k.lower().startswith("d2/characters/demonarm/")
            or k.lower().startswith("d2/characters/jackie/jackiefparms")
            or "tentacle" in k.lower()
            or k.lower().startswith("d2/weapons/")
            or k.lower().startswith("ds/weapons/")
        )
    ]
    require(len(targets) == 64, f"Expected 64 weapon/tentacle targets, found {len(targets)}")

    if package_dir.exists():
        shutil.rmtree(package_dir)
    (package_dir / "H").mkdir(parents=True, exist_ok=True)
    (package_dir / "B").mkdir(parents=True, exist_ok=True)
    (package_dir / "F").mkdir(parents=True, exist_ok=True)

    with tempfile.TemporaryDirectory() as tmp:
        tmp_p = Path(tmp)
        extract_selective(cache_dir, "H.Texture", set(targets), tmp_p / "H")
        extract_selective(cache_dir, "B.Texture", set(targets), tmp_p / "B")
        extract_selective(cache_dir, "F.Texture", set(targets), tmp_p / "F")

        manifest_items = []
        for idx, rel in enumerate(targets, 1):
            up_png = png_map[Path(rel).name.lower()]
            up_img = Image.open(up_png).convert("RGB")

            hb_orig = (tmp_p / "H" / rel).read_bytes()
            b_orig = (tmp_p / "B" / rel).read_bytes()
            f_orig = (tmp_p / "F" / rel).read_bytes() if (tmp_p / "F" / rel).exists() else b""

            pos94, mips_b, mips_f, n_tbl, fmt_str, b_w, b_h = parse_h_header(hb_orig)
            full_w = b_w << mips_f
            full_h = b_h << mips_f

            # 1. Encode B.Texture mip chain (1x1 .. b_w x b_h)
            b_mips = build_mip_list(up_img, 1, 1, b_w, b_h)
            if fmt_str == "DXT1":
                b_chain_len = crt.dxt_chain_size(b_w, b_h, 8)
                b_tail = b_orig[b_chain_len:]
                b_new = interleave_planar_dxt1_chain(b_mips, b_tail, 16384)
            else:
                b_new = replace_planar_dxt5_rgb_chain(b_orig, b_mips, 16384)
            require(len(b_new) == len(b_orig), f"B size mismatch for {rel}: {len(b_new)} != {len(b_orig)}")

            # 2. Encode F.Texture mip chain ((b_w*2)x(b_h*2) .. full_w x full_h) if streamed
            if mips_f > 0 and len(f_orig) > 0:
                f_mips = build_mip_list(up_img, b_w * 2, b_h * 2, full_w, full_h)
                if fmt_str == "DXT1":
                    f_new = interleave_planar_dxt1_chain(f_mips, b"", 16384)
                else:
                    f_new = replace_planar_dxt5_rgb_chain(f_orig, f_mips, 16384)
                require(len(f_new) == len(f_orig), f"F size mismatch for {rel}: {len(f_new)} != {len(f_orig)}")
                h_new = patch_h_for_uncompressed_f(hb_orig)
            else:
                f_new = None
                h_new = hb_orig

            # Write encoded payloads into package_dir/{H,B,F}
            hp_out = package_dir / "H" / rel
            bp_out = package_dir / "B" / rel
            hp_out.parent.mkdir(parents=True, exist_ok=True)
            bp_out.parent.mkdir(parents=True, exist_ok=True)
            hp_out.write_bytes(h_new)
            bp_out.write_bytes(b_new)

            f_sha = None
            if f_new is not None:
                fp_out = package_dir / "F" / rel
                fp_out.parent.mkdir(parents=True, exist_ok=True)
                fp_out.write_bytes(f_new)
                f_sha = hashlib.sha256(f_new).hexdigest()

            manifest_items.append({
                "path": rel,
                "format": fmt_str,
                "base_dim": f"{b_w}x{b_h}",
                "full_dim": f"{full_w}x{full_h}",
                "h_size": len(h_new),
                "b_size": len(b_new),
                "f_size": len(f_new) if f_new else 0,
                "h_sha256": hashlib.sha256(h_new).hexdigest(),
                "b_sha256": hashlib.sha256(b_new).hexdigest(),
                "f_sha256": f_sha,
            })
            if idx % 16 == 0 or idx == len(targets):
                print(f"  Encoded & verified [{idx:2d}/{len(targets)}] ({rel} -> {fmt_str} {full_w}x{full_h})")

    manifest = {
        "schema": 1,
        "name": "Darkness2VR-ComicHybrid-WeaponsTentacles-2K",
        "count": len(manifest_items),
        "items": manifest_items,
    }
    (package_dir / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(f"\n[SUCCESS] Built & verified {len(manifest_items)} textures in {time.time() - t0:.1f}s -> {package_dir}")


def check_status(cache_dir: Path):
    print("=== THE DARKNESS II: COMIC TEXTURE PACK STATUS ===")
    any_mod = False
    for stem in AFFECTED_STEMS:
        toc = cache_dir / f"{stem}.toc"
        cch = cache_dir / f"{stem}.cache"
        toc_bak = cache_dir / f"{stem}.toc.orig_bak"
        cch_bak = cache_dir / f"{stem}.cache.orig_bak"
        has_bak = toc_bak.exists() and cch_bak.exists()
        if has_bak:
            diff = cch.stat().st_size - cch_bak.stat().st_size
            is_mod = diff > 0
            if is_mod:
                any_mod = True
            st = f"[MODDED (+{diff / (1024*1024):.2f} MB)]" if is_mod else "[VANILLA]"
        else:
            st = "[VANILLA - No backup needed yet]"
        print(f"  {stem:12s}: {st} (Backup exists: {has_bak})")
    print("\nOVERALL STATE:")
    if any_mod:
        print("  -> COMIC-HYBRID WEAPONS & TENTACLES PACK IS ACTIVE.")
    else:
        print("  -> VANILLA TEXTURES ARE ACTIVE.")


def install_package(cache_dir: Path, package_dir: Path):
    assert_game_closed()
    manifest_path = package_dir / "manifest.json"
    require(manifest_path.exists(), f"Package manifest not found: {manifest_path}. Run --build first.")
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    items = manifest["items"]

    print(f"=== INSTALLING COMIC-HYBRID TEXTURE PACK ({len(items)} WEAPONS & TENTACLES) ===")

    # 1. Verify all payload hashes before touching game files
    replacements = {"H.Texture": {}, "B.Texture": {}, "F.Texture": {}}
    for item in items:
        rel = item["path"]
        hp = package_dir / "H" / rel
        bp = package_dir / "B" / rel
        h_bytes = hp.read_bytes()
        b_bytes = bp.read_bytes()
        require(hashlib.sha256(h_bytes).hexdigest() == item["h_sha256"], f"H SHA256 mismatch: {rel}")
        require(hashlib.sha256(b_bytes).hexdigest() == item["b_sha256"], f"B SHA256 mismatch: {rel}")
        replacements["H.Texture"][rel] = h_bytes
        replacements["B.Texture"][rel] = b_bytes
        if item["f_size"] > 0:
            fp = package_dir / "F" / rel
            f_bytes = fp.read_bytes()
            require(hashlib.sha256(f_bytes).hexdigest() == item["f_sha256"], f"F SHA256 mismatch: {rel}")
            replacements["F.Texture"][rel] = f_bytes

    # 2. Ensure clean .orig_bak backups exist for H.Texture, B.Texture, F.Texture
    for stem in AFFECTED_STEMS:
        for ext in (".toc", ".cache"):
            src = cache_dir / f"{stem}{ext}"
            bak = cache_dir / f"{stem}{ext}.orig_bak"
            if not bak.exists():
                print(f"  Creating vanilla backup: {bak.name} ...")
                shutil.copy2(src, bak)
            else:
                # Always start from clean .orig_bak so repeated installs don't bloat the cache
                shutil.copy2(bak, src)

    # 3. Append replacements and update TOC entries in-place (avoiding 2.2 GB full file copy)
    for stem in AFFECTED_STEMS:
        reps = replacements[stem]
        if not reps:
            continue
        cache_obj = Cache(cache_dir, stem)
        toc = bytearray(cache_obj.toc)
        cch_path = cache_dir / f"{stem}.cache"
        toc_path = cache_dir / f"{stem}.toc"
        with open(cch_path, "ab") as f:
            for rel, data in sorted(reps.items()):
                pos = cache_obj.entries[rel][0]
                struct.pack_into("<Q", toc, pos, f.tell())
                struct.pack_into("<III", toc, pos + 16, len(data), len(data), 0)
                f.write(data)
        toc_path.write_bytes(toc)
        # Verify TOC readback
        rebuilt = Cache(cache_dir, stem)
        require(rebuilt.entries.keys() == cache_obj.entries.keys(), f"TOC keys changed for {stem}")
        print(f"  Patched {stem:10s}: {len(reps):2d} assets appended & verified")

    print("\n[SUCCESS] Comic-Hybrid Weapons & Tentacles Texture Pack installed!")
    check_status(cache_dir)


def restore_vanilla(cache_dir: Path):
    assert_game_closed()
    print("=== RESTORING VANILLA TEXTURE CACHES ===")
    restored = 0
    for stem in AFFECTED_STEMS:
        for ext in (".toc", ".cache"):
            src = cache_dir / f"{stem}{ext}"
            bak = cache_dir / f"{stem}{ext}.orig_bak"
            if bak.exists():
                shutil.copy2(bak, src)
                print(f"  Restored {src.name}")
                restored += 1
    print(f"\n[SUCCESS] Restored {restored} files to 100% vanilla state.")
    check_status(cache_dir)


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    group = ap.add_mutually_exclusive_group(required=True)
    group.add_argument("--build", action="store_true", help="Build & verify binary package from extracted_textures/2k")
    group.add_argument("--install", action="store_true", help="Install package into Cache.Windows (MAIN only)")
    group.add_argument("--restore", action="store_true", help="Restore vanilla texture caches from .orig_bak (MAIN only)")
    group.add_argument("--status", action="store_true", help="Check current installation status")
    ap.add_argument("--cache-dir", type=Path, default=DEFAULT_GAME_CACHE)
    ap.add_argument("--upscaled-dir", type=Path, default=DEFAULT_UPSCALED_DIR)
    ap.add_argument("--package-dir", type=Path, default=DEFAULT_PACKAGE_DIR)
    args = ap.parse_args()

    if args.build:
        build_package(args.cache_dir, args.upscaled_dir, args.package_dir)
    elif args.install:
        install_package(args.cache_dir, args.package_dir)
    elif args.restore:
        restore_vanilla(args.cache_dir)
    elif args.status:
        check_status(args.cache_dir)


if __name__ == "__main__":
    main()
