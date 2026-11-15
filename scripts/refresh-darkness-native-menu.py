"""Refresh the staged English menu ZIP while preserving signed input requirements.

Use local game files as a verification fixture. Their bytes are accepted for
verification only if every existing recipe reconstructs its pinned output.
The refreshed recipes preserve the original source SHA-256 and copy operations
outside the language record. No signing, publishing, or game installation.
"""
import argparse
import hashlib
import importlib.util
from io import BytesIO
import json
from pathlib import Path
import struct
import zipfile

from darkness_menu_language import validate_menu_language


def sha(data):
    return hashlib.sha256(data).hexdigest().upper()


def read_tape(data, length):
    magic, version, size, count = struct.unpack_from("<4sIQI", data)
    if (magic, version, size) != (b"SVRP", 1, length) or count > 1000000:
        raise ValueError("Unexpected source reconstruction recipe")
    p, ops = 20, []
    for _ in range(count):
        op = data[p]
        p += 1
        if op == 0:
            off, n = struct.unpack_from("<QI", data, p)
            ops.append((off, n))
            p += 12
        elif op == 1:
            n, = struct.unpack_from("<I", data, p)
            p += 4
            ops.append((None, data[p:p+n]))
            p += n
        else:
            raise ValueError("Unexpected recipe opcode")
    if p != len(data) or sum(map(op_length, ops)) != length:
        raise ValueError("Invalid recipe lengths")
    return ops


def op_length(op):
    return len(op[1]) if op[0] is None else op[1]


def materialize(ops, source):
    parts = []
    for off, part in ops:
        if off is not None:
            if off + part > len(source):
                raise ValueError("Recipe exceeds the verification fixture")
            part = source[off:off+part]
        parts.append(part)
    return b"".join(parts)


def replace_range(ops, start, stop, data):
    def slice_ops(left, right):
        pos = 0
        for off, part in ops:
            length = len(part) if off is None else part
            a, b = max(0, left-pos), min(length, right-pos)
            if a < b:
                yield (None, part[a:b]) if off is None else (off+a, b-a)
            pos += length

    total = sum(map(op_length, ops))
    if not 0 <= start <= stop <= total:
        raise ValueError("Replacement outside reconstructed asset")
    return [*slice_ops(0, start), (None, data), *slice_ops(stop, total)]


def write_tape(ops, size):
    tape = bytearray(struct.pack("<4sIQI", b"SVRP", 1, size, len(ops)))
    for off, part in ops:
        if off is None:
            tape.extend(struct.pack("<BI", 1, len(part)))
            tape.extend(part)
        else:
            tape.extend(struct.pack("<BQI", 0, off, part))
    return tape


def refresh(tool, module, archive, fixture, english, output):
    validate_menu_language(tool, english, "en")
    if sha(archive.read_bytes()) != module["archiveSha256"]:
        raise ValueError("Original menu archive hash mismatch")
    names = ("H.Misc.cache", "H.Misc.toc")
    old = output / "private-before"
    old.mkdir()
    verified = {}
    with zipfile.ZipFile(archive) as src:
        if sorted(src.namelist()) != sorted(name + ".svrp" for name in names):
            raise ValueError("Unexpected menu archive entries")
        if sorted(f["path"] for f in module["files"]) != sorted("Cache.Windows/" + n for n in names):
            raise ValueError("Unexpected menu cache targets")
        for name in names:
            file = next(f for f in module["files"] if f["path"] == "Cache.Windows/" + name)
            recipe, = file["recipe"]
            if file["kind"] != "recipe" or recipe["entry"] != name + ".svrp" or recipe["source"] != "instructions":
                raise ValueError("Unexpected menu recipe format")
            source = (fixture / name).read_bytes()
            ops = read_tape(src.read(recipe["entry"]), recipe["length"])
            before = materialize(ops, source)
            if sha(before) != file["sha256"]:
                raise ValueError(f"{name}: fixture did not reconstruct the pinned output")
            (old / name).write_bytes(before)
            verified[name] = file, recipe, source, ops, before

    cache = tool.Cache(old, "H.Misc")
    key, = [k for k in cache.entries if Path(k).name == "Languages.cl"]
    record, offset, packed, size = cache.entries[key]
    before = verified[names[0]][-1]
    if packed != size or offset+size != len(before):
        raise ValueError("Expected appended, stored Languages.cl")
    if before[offset:] == english:
        raise ValueError("Existing recipe already contains the supplied English table")
    replacements = {
        names[0]: (offset, offset+size, english),
        names[1]: (record+16, record+28, struct.pack("<III", len(english), len(english), 0)),
    }
    rebuilt = output / "private-after"
    rebuilt.mkdir()
    packed_zip = BytesIO()
    expanded = 0
    with zipfile.ZipFile(packed_zip, "w", zipfile.ZIP_DEFLATED) as target:
        for name in names:
            file, recipe, source, ops, before = verified[name]
            start, stop, data = replacements[name]
            output_ops = replace_range(ops, start, stop, data)
            expected = before[:start] + data + before[stop:]
            tape = write_tape(output_ops, len(expected))
            if materialize(read_tape(tape, len(expected)), source) != expected:
                raise ValueError("Updated menu recipe failed round trip")
            (rebuilt / name).write_bytes(expected)
            recipe["length"] = len(expected)
            file["sha256"] = sha(expected)
            target.writestr(recipe["entry"], tape)
            expanded += len(tape)
    result = tool.Cache(rebuilt, "H.Misc")
    if result.entries != {**cache.entries, key: (record, offset, len(english), len(english))}:
        raise ValueError("The recipe changed unrelated cache records")
    packed_zip = packed_zip.getvalue()
    (output / "darkness-menu.zip").write_bytes(packed_zip)
    module.update(archiveSha256=sha(packed_zip), downloadBytes=len(packed_zip),
                  expandedBytes=expanded)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("catalog", "archive", "fixture", "english", "cache-tool", "output"):
        parser.add_argument("--" + name, required=True, type=Path)
    args = parser.parse_args()
    spec = importlib.util.spec_from_file_location("darkness_cache_tool", args.cache_tool)
    tool = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(tool)
    catalog = json.loads(args.catalog.read_text(encoding="utf-8-sig"))
    modules = next(g for g in catalog["games"] if g["id"] == "darkness2")["modules"]
    menu = next(m for m in modules if m["id"] == "darkness-menu")
    args.output.mkdir(parents=True, exist_ok=False)
    refresh(tool, menu, args.archive, args.fixture, args.english.read_bytes(), args.output)
    (args.output / "catalog.unsigned.json").write_text(json.dumps(catalog, ensure_ascii=False, indent=2) + "\n",
                                                       encoding="utf-8")
    print("PASS pinned native menu recipe refresh: Continue / Options / Back")


if __name__ == "__main__":
    main()
