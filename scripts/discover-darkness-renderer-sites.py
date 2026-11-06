"""Offline discovery of Darkness II renderer call sites; never authorizes hooks."""
import argparse
import hashlib
import json
from pathlib import Path
import struct

# The two Present calls belong to one native dispatch routine.
SITES = ((0x694052, 5, "present"), (0x69406F, 5, "alternate"), (0x564CBF, 2, "acquire"))
KNOWN = "d46bb6cb762732b72ab9d34428237d2afa89f5792bdbed957d78793ee8134ec1"
RADIUS = 20


def code_sections(data):
    def read(fmt, offset):
        return struct.unpack_from("<" + fmt, data, offset)
    if data[:2] != b"MZ":
        raise ValueError("Missing DOS signature")
    (pe,) = read("I", 0x3c)
    if data[pe:pe+4] != b"PE\0\0":
        raise ValueError("Missing PE signature")
    machine, count = read("HH", pe+4)
    (optional,) = read("H", pe+20)
    if machine != 0x14c or not 0 < count <= 96 or optional < 96 or read("H", pe+24)[0] != 0x10b:
        raise ValueError("Expected x86 PE32")
    for i in range(count):
        off = pe+24+optional+40*i
        virtual, rva, size, raw = read("IIII", off+8)
        flags = read("I", off+36)[0]
        if flags & 0x20000000:
            if not raw or raw+size > len(data):
                raise ValueError("Invalid executable section")
            yield rva, data[raw:raw+min(virtual or size, size)]


def discover(reference, candidate):
    ref, target = list(code_sections(reference)), list(code_sections(candidate))
    matches = {}
    for site, length, name in SITES:
        window, = (section[site-base-RADIUS:site-base+length+RADIUS]
                   for base, section in ref
                   if base+RADIUS <= site and site+length+RADIUS <= base+len(section))
        hits = []
        for base, section in target:
            offset = section.find(window)
            while offset != -1 and len(hits) < 2:
                hits.append(base+offset+RADIUS)
                offset = section.find(window, offset+1)
            if len(hits) >= 2:
                break
        matches[name] = {"hits": len(hits), "rva": hits[0] if len(hits) == 1 else None}
    unique = all(item["hits"] == 1 for item in matches.values())
    pair = unique and matches["alternate"]["rva"] - matches["present"]["rva"] == SITES[1][0] - SITES[0][0]
    return {"sha256": hashlib.sha256(candidate).hexdigest(), "matches": matches,
            "status": "candidate-sites-found" if pair else "unresolved", "runtimeAdmission": False}


def main():
    cli = argparse.ArgumentParser(description=__doc__)
    cli.add_argument("reference", type=Path, help="local executable from the accepted build")
    cli.add_argument("candidate", type=Path)
    args = cli.parse_args()
    reference = args.reference.read_bytes()
    if hashlib.sha256(reference).hexdigest() != KNOWN:
        cli.error("reference is not the accepted build")
    print(json.dumps(discover(reference, args.candidate.read_bytes()), indent=2))


if __name__ == "__main__":
    main()
