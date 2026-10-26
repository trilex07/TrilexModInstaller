"""Read normal and delay import identities without loading or executing a DLL."""
import struct


def inspect(data):
    def u16(at): return struct.unpack_from('<H', data, at)[0]
    def u32(at): return struct.unpack_from('<I', data, at)[0]
    if data[:2] != b'MZ': raise ValueError('Not a PE image')
    pe = u32(60)
    if data[pe:pe+4] != b'PE\0\0' or u16(pe+4) != 0x14c: raise ValueError('Not x86 PE')
    opt = pe+24
    if u16(opt) != 0x10b: raise ValueError('Not PE32')
    sections = []
    for i in range(u16(pe+6)):
        at = opt+u16(pe+20)+40*i
        vs, va, size, raw = struct.unpack_from('<IIII', data, at+8)
        sections.append((va, max(vs, size), raw, size))
    def offset(rva):
        for va, size, raw, disk in sections:
            if va <= rva < va+size:
                if rva-va >= disk or raw+rva-va >= len(data): raise ValueError('Import outside raw section')
                return raw+rva-va
        if rva < u32(opt+60): return rva
        raise ValueError('Unmapped import RVA')
    def name(rva):
        at = offset(rva); end = data.find(b'\0', at, at+256)
        if end < 0: raise ValueError('Invalid import name')
        value = data[at:end].decode('ascii')
        if '/' in value or '\\' in value or not value.lower().endswith('.dll'): raise ValueError('Unsafe DLL identity')
        return value.lower()
    names = set()
    count = u32(opt+92)
    for directory, stride, name_offset in ((1, 20, 12), (13, 32, 4)):
        if count <= directory: continue
        rva, size = struct.unpack_from('<II', data, opt+96+directory*8)
        if not rva: continue
        at = offset(rva)
        for _ in range(min(size//stride+1, 4096)):
            if at+stride > len(data): raise ValueError('Truncated import descriptor')
            if not any(data[at:at+stride]): break
            address = u32(at+name_offset)
            if directory == 13 and not u32(at) & 1: address -= u32(opt+28)
            names.add(name(address)); at += stride
        else: raise ValueError('Unterminated import table')
    return {'architecture': 'x86 PE32', 'imports': sorted(names)}
