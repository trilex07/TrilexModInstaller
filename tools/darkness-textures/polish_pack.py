"""Fixed Evolution cache parser/writer, shared with Darkness2VR tools."""
import hashlib,shutil,struct
from pathlib import Path

def require(condition, message):
    if not condition:
        raise ValueError(message)


def sha(path):
    with open(path, "rb") as f:
        return hashlib.file_digest(f, "sha256").hexdigest()


class Cache:
    """96-byte TOC entries: stored size at +16, unpacked +20, padding +24."""
    def __init__(self, root, stem):
        self.root, self.stem = Path(root), stem
        self.toc = bytearray((self.root / (stem + ".toc")).read_bytes())
        require(self.toc[:4] == bytes.fromhex("4ec66718"), "TOC magic")
        require((len(self.toc) - 8) % 96 == 0, "TOC length")
        self.entries = {}
        dirs = [""]
        limit = (self.root / (stem + ".cache")).stat().st_size
        for pos in range(8, len(self.toc), 96):
            off, stamp, packed, size, padding, parent = struct.unpack_from("<QQIIII", self.toc, pos)
            name = self.toc[pos+32:pos+96].split(b"\0", 1)[0].decode("utf-8")
            require(name and not any(c in name for c in ("/", "\\", ":")) and name not in (".", ".."), "Unsafe TOC component")
            require(parent < len(dirs), "TOC directory reference")
            path = (dirs[parent] + "/" + name).lstrip("/")
            if off == 0xffffffffffffffff:
                dirs.append(path)
            elif stamp:
                require(off + packed <= limit, "TOC payload bounds")
                require(path not in self.entries, "Duplicate live TOC path: " + path)
                self.entries[path] = (pos, off, packed, size)

    def write(self, out, replacements):
        require(set(replacements) <= self.entries.keys(), "Unknown replacement")
        out = Path(out)
        out.mkdir(parents=True, exist_ok=True)
        dst = out / (self.stem + ".cache")
        shutil.copyfile(self.root / dst.name, dst)
        toc = bytearray(self.toc)
        with dst.open("ab") as f:
            for path, data in sorted(replacements.items()):
                pos = self.entries[path][0]
                # Preserve timestamp, directory identity, name and unrelated entries.
                struct.pack_into("<Q", toc, pos, f.tell())
                struct.pack_into("<III", toc, pos+16, len(data), len(data), 0)
                f.write(data)
        (out / (self.stem + ".toc")).write_bytes(toc)
        rebuilt = Cache(out, self.stem)
        require(rebuilt.entries.keys() == self.entries.keys(), "Changed TOC identities")
        with dst.open("rb") as f:
            for path, data in replacements.items():
                _, off, packed, size = rebuilt.entries[path]
                f.seek(off)
                require(packed == size == len(data) and f.read(size) == data, "Payload readback")
        allowed = set()
        for path in replacements:
            pos = self.entries[path][0]
            allowed.update(range(pos, pos+8))
            allowed.update(range(pos+16, pos+28))
        require(all(a == b or i in allowed for i, (a, b) in enumerate(zip(self.toc, toc))), "Unrelated TOC mutation")

