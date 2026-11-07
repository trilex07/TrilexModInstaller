"""Refresh only the reviewed Darkness DLL and its signed-catalog input.

No game writes, signing, uploads, version bumps or changes to other modules.
"""
import argparse,copy,hashlib,json,struct,zipfile
from pathlib import Path

def sha(data):return hashlib.sha256(data).hexdigest().upper()

def refresh(catalog,archive,runtime,expected):
    result=copy.deepcopy(catalog)
    module=next(m for g in result['games'] if g['id']=='darkness2' for m in g['modules'] if m['id']=='vr-core')
    if sha(archive)!=module['archiveSha256'].upper():raise ValueError('Original archive hash mismatch')
    if sha(runtime)!=expected.upper():raise ValueError('Reviewed DLL hash mismatch')
    if runtime[:2]!=b'MZ' or len(runtime)<64:raise ValueError('Expected native PE32 DLL')
    pe=struct.unpack_from('<I',runtime,60)[0]
    if (runtime[pe:pe+4]!=b'PE\0\0' or struct.unpack_from('<H',runtime,pe+4)[0]!=0x14c or
        struct.unpack_from('<H',runtime,pe+24)[0]!=0x10b or not struct.unpack_from('<H',runtime,pe+22)[0]&0x2000):
        raise ValueError('Expected native x86 PE32 DLL')
    import io
    output=io.BytesIO()
    with zipfile.ZipFile(io.BytesIO(archive)) as source,zipfile.ZipFile(output,'w',compression=zipfile.ZIP_DEFLATED) as target:
        names=[i.filename for i in source.infolist()]
        if len(names)!=len(set(names)) or set(names)!={f['entry'] for f in module['files']}:raise ValueError('Unexpected archive entries')
        for item in source.infolist():
            data=source.read(item)
            manifest=next(f for f in module['files'] if f['entry']==item.filename)
            if sha(data)!=manifest['sha256'].upper():raise ValueError('Original payload hash mismatch')
            if manifest['path']=='d3d9.dll':
                data=runtime
                manifest['sha256']=sha(data)
            target.writestr(item,data)
        module['expandedBytes']=sum(i.file_size for i in target.infolist())
    packed=output.getvalue()
    module['downloadBytes']=len(packed)
    module['archiveSha256']=sha(packed)
    return result,packed

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ('catalog','archive','runtime','output'):parser.add_argument('--'+name,type=Path,required=True)
    parser.add_argument('--expected-runtime-sha256',required=True)
    args=parser.parse_args()
    updated,packed=refresh(json.loads(args.catalog.read_text(encoding='utf-8-sig')),args.archive.read_bytes(),args.runtime.read_bytes(),args.expected_runtime_sha256)
    args.output.mkdir(parents=True,exist_ok=False)
    (args.output/'darkness2-vr-core.zip').write_bytes(packed)
    (args.output/'catalog.unsigned.json').write_text(json.dumps(updated,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('PASS scoped Darkness DLL refresh; catalog version and other modules preserved')

if __name__=='__main__':main()
