# Fixed accepted Centurion operations, extracted from the MIT SingularityVR tools.
# Original assets are read locally; no geometry/animation keys are embedded.
from pathlib import Path
from collections import defaultdict
import struct
import hashlib
import numpy as np

def read_psk(path):
    data, offset, chunks = Path(path).read_bytes(), 0, {}
    while offset < len(data):
        name, _, size, count = struct.unpack_from('<20siii', data, offset)
        offset += 32
        if size < 0 or count < 0 or offset+size*count > len(data):
            raise ValueError('invalid PSK chunk')
        chunks[name.split(b'\0')[0].decode()] = (size, count, data[offset:offset+size*count])
        offset += size*count
    size, count, raw = chunks['REFSKELT']
    names, parents, local = [], [], []
    # Exact armbind::PskLocal conversion. Row vectors, UEViewer Y reflection.
    for i in range(count):
        r = raw[i*size:(i+1)*size]
        names.append(r[:64].split(b'\0')[0].decode())
        parents.append(struct.unpack_from('<i', r, 72)[0])
        x, y, z, w = struct.unpack_from('<4f', r, 76)
        x, y, z, w = np.array([x, -y, z, -w]) / np.linalg.norm([x, y, z, w])
        if i == 0:
            x, y, z = -x, -y, -z
        m = np.eye(4)
        m[:3, :3] = [[1-2*(y*y+z*z), 2*(x*y-w*z), 2*(x*z+w*y)],
                       [2*(x*y+w*z), 1-2*(x*x+z*z), 2*(y*z-w*x)],
                       [2*(x*z-w*y), 2*(y*z+w*x), 1-2*(x*x+y*y)]]
        px, py, pz = struct.unpack_from('<3f', r, 92)
        m[3, :3] = [px, -py, pz]
        local.append(m)
    absolute = []
    for i, m in enumerate(local):
        if i and not 0 <= parents[i] < i:
            raise ValueError('non-topological skeleton')
        absolute.append(m if i == 0 else m @ absolute[parents[i]])
    _, count, raw = chunks['PNTS0000']
    points = np.frombuffer(raw, '<f4', count*3).reshape(-1, 3).astype(float)
    size, count, raw = chunks['RAWWEIGHTS']
    weights = defaultdict(list)
    for i in range(count):
        weight, point, bone = struct.unpack_from('<fii', raw, i*size)
        if weight > 0:
            weights[point].append((weight, bone))
    size, count, raw = chunks['VTXW0000']
    wedges = [struct.unpack_from('<Iff', raw, i*size) for i in range(count)]
    size, count, raw = chunks['FACE0000']
    faces = [struct.unpack_from('<3HBBI', raw, i*size) for i in range(count)]
    return names, parents, local, np.array(absolute), points, weights, wedges, faces

def parse(data):
    p = 0
    chunks = {}
    while p < len(data):
        if p+32 > len(data):
            raise ValueError('short chunk header')
        raw, _, size, count = struct.unpack_from('<20siii', data, p)
        p += 32
        name = raw.split(b'\0')[0].decode('ascii')
        if size < 0 or count < 0 or count > 1000000 or p+size*count > len(data) or name in chunks:
            raise ValueError('chunk bounds/duplicate')
        chunks[name] = (size, count, data[p:p+size*count])
        p += size*count
    return chunks

def text(raw):
    if b'\0' not in raw:
        raise ValueError('unterminated name')
    return raw.split(b'\0', 1)[0].decode('ascii')

def read_psa(data):
    if not 128 <= len(data) <= 64*1024*1024:
        raise ValueError('PSA size limit')
    chunks, offset = {}, 0
    while offset < len(data):
        if offset+32 > len(data):
            raise ValueError('truncated chunk header')
        ident, version, size, count = struct.unpack_from('<20siii', data, offset)
        name = text(ident)
        end = offset+32+size*count
        if name in chunks or size < 0 or count < 0 or end > len(data):
            raise ValueError('duplicate or invalid chunk range')
        chunks[name] = (version, size, count, data[offset+32:end])
        offset = end
    if list(chunks) not in (['ANIMHEAD','BONENAMES','ANIMINFO','ANIMKEYS'],
                           ['ANIMHEAD','BONENAMES','ANIMINFO','ANIMKEYS','SCALEKEYS']):
        raise ValueError('unsupported PSA chunks/order')
    if chunks['ANIMHEAD'][1:3] != (0, 0):
        raise ValueError('invalid animation header')
    if 'SCALEKEYS' in chunks and chunks['SCALEKEYS'][1:3] != (16, 0):
        raise ValueError('scaled animation requires separate review')
    _, bone_size, bone_count, raw = chunks['BONENAMES']
    if bone_size != 120 or not 1 <= bone_count <= 4096:
        raise ValueError('invalid bone records')
    names = [text(raw[i*120:i*120+64]) for i in range(bone_count)]
    if len(set(names)) != len(names) or not all(names):
        raise ValueError('duplicate/empty bone names')
    _, size, count, raw = chunks['ANIMINFO']
    if size != 168 or not 1 <= count <= 4096:
        raise ValueError('invalid sequence records')
    sequences, total_frames = [], 0
    for i in range(count):
        row = raw[i*168:(i+1)*168]
        bones, root, compression, quota, reduction, time, rate, start, first, frames = struct.unpack_from('<4i3f3i', row, 128)
        if bones != bone_count or root or compression or start or first != total_frames:
            raise ValueError('non-contiguous/unsupported sequence')
        if not 1 <= frames <= 100000 or quota != frames*bones:
            raise ValueError('invalid sequence frame count')
        if not np.isfinite([reduction,time,rate]).all() or not 0 < rate <= 1000 or time != frames:
            raise ValueError('invalid sequence rate/time')
        sequences.append(dict(name=text(row[:64]),first=first,frames=frames,rate=rate))
        total_frames += frames
    if len({s['name'] for s in sequences}) != count:
        raise ValueError('ambiguous sequence names')
    _, size, count, raw = chunks['ANIMKEYS']
    if size != 32 or not 1 <= count <= 1000000 or count != total_frames*bone_count:
        raise ValueError('invalid animation key count')
    keys = np.frombuffer(raw, '<f4').reshape(total_frames,bone_count,8)
    if not np.isfinite(keys).all() or np.any(keys[:,:,7] != 1):
        raise ValueError('invalid key values/time')
    lengths = np.linalg.norm(keys[:,:,3:7].astype(float),axis=2)
    if np.any(np.abs(lengths-1) > .005):
        raise ValueError('non-unit animation rotation')
    # Re-encode every original record, including FLOAT32 keys; byte equality is
    # required before using a measurement. This is not a package patcher.
    rebuilt = b''
    for name, (version,size,count,raw) in chunks.items():
        if name == 'ANIMKEYS':
            raw = keys.astype('<f4').tobytes()
        rebuilt += struct.pack('<20siii',name.encode(),version,size,count)+raw
    if rebuilt != data:
        raise ValueError('PSA exact roundtrip failed')
    return names, sequences, keys

def extract(psk, units=52.5):
    names, parents, _, bind, points, weights, wedges, faces = psk
    if not np.isfinite(units) or not 10 <= units <= 1000:
        raise ValueError('invalid explicit mesh units/metre')
    if len(set(names)) != len(names) or 'clip' not in names or 'r_arm_wrist' not in names:
        raise ValueError('missing/ambiguous Centurion bones')
    clip, wrist = names.index('clip'), names.index('r_arm_wrist')
    # Include descendants, never a spatial cut or a dominant-weight shortcut.
    subtree = {clip}
    for i, parent in enumerate(parents):
        if i and parent in subtree:
            subtree.add(i)
    selected = set()
    for point, row in weights.items():
        if not 0 <= point < len(points) or any(not np.isfinite(w) or not 0 <= b < len(names) for w,b in row):
            raise ValueError('bad weights')
        total = sum(w for w,_ in row)
        if abs(total-1) > 1e-4:
            raise ValueError('unnormalized skin')
        if sum(w for w,b in row if b in subtree) >= 1-1e-6:
            selected.add(point)
    if not selected:
        raise ValueError('no rigid clip vertices')
    reflect = np.diag([1.,-1.,1.,1.])
    raw_bind = reflect @ bind @ reflect
    inv = np.linalg.inv(raw_bind[clip])
    if not np.isfinite(inv).all() or not np.isfinite(points).all():
        raise ValueError('invalid bind/positions')
    vertices, indices, remap, material_ids = [], [], {}, set()
    crossing = 0
    for face in faces:
        if any(not 0 <= k < len(wedges) for k in face[:3]):
            raise ValueError('bad wedge index')
        corners = [wedges[k] for k in face[:3]]
        if any(not 0 <= p < len(points) or not np.isfinite([u,v]).all() for p,u,v in corners):
            raise ValueError('bad point/UV')
        inside = [p in selected for p,_,_ in corners]
        if any(inside) and not all(inside):
            crossing += 1
        if not all(inside):
            continue
        material_ids.add(face[3])
        for p,u,v in corners:
            key = (p,u,v)
            if key not in remap:
                remap[key] = len(vertices)
                xyz = (np.r_[points[p],1.] @ inv)[:3]/units
                vertices.append((*xyz,0xffffffff,u,v))
            indices.append(remap[key])
    if crossing or material_ids != {0} or not indices or len(vertices)>65535:
        raise ValueError('mixed boundary/material/empty/oversized clip')
    xyz = np.array([v[:3] for v in vertices])
    if np.linalg.norm(xyz,axis=1).max() > 1:
        raise ValueError('clip exceeds bounded cache scale')
    cache = struct.pack('<4sIII',b'SVRK',2,len(vertices),len(indices))
    cache += b''.join(struct.pack('<3fI2f',*v) for v in vertices)
    cache += struct.pack('<'+'H'*len(indices),*indices)
    # Full round trip, not a count-only proof of coordinate conversion.
    encoded_xyz=np.array([struct.unpack_from('<3f',cache,16+24*i) for i in range(len(vertices))])
    local_uu = np.c_[encoded_xyz*units,np.ones(len(xyz))]
    source_ids = [key[0] for key in remap]
    if not np.allclose((local_uu @ raw_bind[clip])[:,:3],points[source_ids],rtol=0,atol=2e-5):
        raise ValueError('bind roundtrip failed')
    relative = raw_bind[clip] @ np.linalg.inv(raw_bind[wrist])
    return cache,dict(source_wedges=len(wedges),source_triangles=len(faces),
        bones=len(names),clip_bone=clip,clip_subtree=[names[i] for i in sorted(subtree)],
        rigid_points=len(selected),vertices=len(vertices),triangles=len(indices)//3,
        crossing_triangles=crossing,materials=sorted(material_ids),
        bounds_clip_metres=[xyz.min(0).tolist(),xyz.max(0).tolist()],
        dimensions_metres=np.ptp(xyz,axis=0).tolist(),units_per_metre=units,
        clip_to_wrist_bind=relative.tolist(),uv='original PSK, upper-left V; no flip',
        axes='original clip bone-local, NOT controller grip-local',
        roundtrip='every encoded FLOAT32 cache vertex reconstructed in raw PSK bind space, absolute error <= 2e-5 UU',
        texture='not inferred; native material capture remains separate',
        output_sha256=hashlib.sha256(cache).hexdigest())

def fnv(data):
    h = 2166136261
    for b in data:
        h = ((h ^ b) * 16777619) & 0xffffffff
    return h

def cooked_section(data, faces):
    pattern = struct.pack('<ihhiH', 1, 0, 0, 0, faces)
    at = data.find(pattern)
    if at < 0 or data.find(pattern, at+1) != -1:
        raise ValueError('cooked section not unique')
    pos = at+len(pattern)

    def integer():
        nonlocal pos
        value, = struct.unpack_from('<i', data, pos)
        pos += 4
        return value

    def array(fmt):
        nonlocal pos
        n = integer()
        size = struct.calcsize(fmt)
        if not 0 <= n <= 100000 or pos+n*size > len(data):
            raise ValueError('cooked array bounds')
        result = [struct.unpack_from(fmt, data, pos+i*size)[0] for i in range(n)]
        pos += n*size
        return result

    if integer() != 2:
        raise ValueError('not 16-bit indices')
    indices = array('<H')
    if len(indices) != faces*3:
        raise ValueError('index count mismatch')
    array('<H'); array('<H'); array('<B')
    if integer() != 1:
        raise ValueError('not one cooked chunk')
    if integer() != 0 or integer() != 0 or integer() != 0:
        raise ValueError('unexpected chunk vertex origin/CPU data')
    palette = array('<H')
    rigid, soft, influences = integer(), integer(), integer()
    if rigid+soft != 5799 or not 1 <= influences <= 4:
        raise ValueError('unexpected vertex chunk')
    return palette, struct.pack('<'+'H'*len(indices), *indices)

def prepare(psk, psa, cooked):
    names, parents, _, bind, _, weights, wedges, faces = psk
    palette, indices = cooked_section(cooked, len(faces))
    expected = ['clip', 'cog_alt_pistol', 'hammer', 'Trigger', 'clip_release']
    if [names[b] for b in palette] != expected:
        raise ValueError('unknown GPU palette order')
    if {b for row in weights.values() for w, b in row} != set(palette):
        raise ValueError('PSK/cooked bone contributors disagree')
    if len(wedges) != 5799 or len(faces) != 6767:
        raise ValueError('unknown topology')
    pn, sequences, keys = read_psa(psa)
    reload = next(s for s in sequences if s['name'] == 'Reload')
    if reload['frames'] != 53 or reload['rate'] != 24:
        raise ValueError('unreviewed animation')
    parent = names.index('cog_alt_pistol')
    animated = ['clip', 'clip_release']
    bone_ids = [names.index(n) for n in animated]
    if any(parents[b] != parent for b in bone_ids):
        raise ValueError('unreviewed hierarchy')
    reflect = np.diag([1., -1., 1., 1.])
    raw_bind = reflect @ bind @ reflect
    matrices = [raw_bind[parent]] + [np.linalg.inv(raw_bind[b]) for b in bone_ids]
    track = keys[reload['first']:reload['first']+53,
                 [pn.index(n) for n in animated], :7]
    if not np.isfinite(track).all() or not all(np.isfinite(m).all() for m in matrices):
        raise ValueError('nonfinite animation')
    header = struct.pack('<4s10If', b'SVCA', 1, 5799, 0, 6767, fnv(indices), 5, 1, 0, 4, 53, 24.)
    data = header + b''.join(m.astype('<f4').tobytes() for m in matrices) + track.astype('<f4').tobytes()
    if len(data) != 3208:
        raise ValueError('cache size contract')
    return data, dict(palette=palette, palette_names=expected, clip_slot=0,
        release_slot=4, parent_slot=1, vertices=5799, triangles=6767,
        index_fnv=fnv(indices), frames=53, fps=24,
        output_sha256=hashlib.sha256(data).hexdigest(),
        note='Original Reload clip/release keys; render-only, no animation notify or ammo authority')

def bake(data, units=52.5):
    if not np.isfinite(units) or not 10 <= units <= 1000:
        raise ValueError('invalid units')
    chunks = parse(data)

    def records(name, stride, fmt):
        size, count, raw = chunks[name]
        if size != stride or not 0 < count <= 65535:
            raise ValueError('unsupported static mesh chunk')
        return [struct.unpack_from(fmt, raw, i*stride) for i in range(count)]

    # Refuse skinning/animation; this is the exact rigid particle mesh.
    if chunks['REFSKELT'][1] or chunks['RAWWEIGHTS'][1]:
        raise ValueError('not a rigid static mesh')
    points = np.array(records('PNTS0000', 12, '<3f'))
    wedges = records('VTXW0000', 16, '<IffBBH')
    faces = records('FACE0000', 12, '<3HBBI')
    if not np.isfinite(points).all():
        raise ValueError('nonfinite points')
    centre = (points.min(0)+points.max(0))*.5
    vertices = []
    for p, u, v, material, _, _ in wedges:
        if p >= len(points) or not np.isfinite([u, v]).all() or material:
            raise ValueError('invalid wedge/material')
        xyz = (points[p]-centre)/units
        if np.linalg.norm(xyz) > .20:
            raise ValueError('oversized particle')
        vertices.append((*xyz, 0xffffffff, u, v))
    indices = []
    for a, b, c, material, _, _ in faces:
        if max(a, b, c) >= len(vertices) or material:
            raise ValueError('invalid face/material')
        indices.extend((a, b, c))
    cache = struct.pack('<4sIII', b'SVRK', 2, len(vertices), len(indices))
    cache += b''.join(struct.pack('<3fI2f', *v) for v in vertices)
    cache += struct.pack('<'+'H'*len(indices), *indices)
    encoded = np.array([struct.unpack_from('<3f', cache, 16+24*i) for i in range(len(vertices))])
    if not np.allclose(encoded*units+centre, points[[w[0] for w in wedges]], rtol=0, atol=2e-6):
        raise ValueError('shell position roundtrip failed')
    return cache, dict(points=len(points), vertices=len(vertices), triangles=len(faces),
                       original_centre_uu=centre.tolist(), units_per_metre=units,
                       dimensions_metres=(np.ptp(points, axis=0)/units).tolist(),
                       geometry='original points/UV/faces; centre translated for spin, no shape edits',
                       roundtrip='all FLOAT32 cache vertices -> original positions <=2e-6 UU',
                       output_sha256=hashlib.sha256(cache).hexdigest())

def generate(exports,output,mode,cooked):
    package=exports/'wp_SP_ARPistol_SF'
    if mode not in ('clip','shell','animation') or output.exists():raise ValueError('Unsupported/fresh Centurion output required')
    if mode=='shell':data,_=bake((package/'StaticMesh3/shell_rifle.pskx').read_bytes())
    else:
        mesh=read_psk(package/'SkeletalMesh3/ch_inview_alt_pistol_mesh.psk')
        if mode=='clip':data,_=extract(mesh)
        else:data,_=prepare(mesh,(package/'AnimSet/Alt_pistol.psa').read_bytes(),cooked.read_bytes())
    if len(data)!={'clip':33136,'shell':3880,'animation':3208}[mode]:raise ValueError('Centurion output contract changed')
    with output.open('xb') as stream:stream.write(data)
