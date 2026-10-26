"""Make exact-preimage upgrade recipes from reviewed offline V7 artifacts.

Never claims a pre-V7 mod backup is pristine retail. Never publishes these artifacts.
XOR masks require the exact local original; entire cooked packages are not archived.
Rights/first-install baselines are separate explicit gates, not inferred from a delta.
"""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import zipfile

ROOT=Path(__file__).resolve().parent.parent
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest().upper()
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def write(p,v):p.write_text(json.dumps(v,indent=2)+'\n',encoding='utf-8')


def delta(before,after,stage,key):
    common=min(before.stat().st_size,after.stat().st_size);parts=[]
    mask=stage/(key+'.xor');mask.parent.mkdir(parents=True,exist_ok=True)
    with before.open('rb') as old,after.open('rb') as new,mask.open('xb') as out:
        remaining=common
        while remaining:
            size=min(remaining,1024*1024);a=old.read(size);b=new.read(size)
            if len(a)!=size or len(b)!=size:raise ValueError('Input changed while preparing mask')
            out.write((int.from_bytes(a,'little')^int.from_bytes(b,'little')).to_bytes(size,'little'));remaining-=size
        for offset in range(0,common,256*1024*1024):parts.append(dict(source='xor-original',offset=offset,length=min(common-offset,256*1024*1024),entry=key+'.xor'))
        if after.stat().st_size>common:
            tail=stage/(key+'.tail')
            with tail.open('xb') as t:shutil.copyfileobj(new,t)
            for offset in range(0,tail.stat().st_size,256*1024*1024):parts.append(dict(source='payload',offset=offset,length=min(tail.stat().st_size-offset,256*1024*1024),entry=key+'.tail'))
    # Independently reconstruct with streamed byte reads before emitting metadata.
    check=hashlib.sha256()
    with before.open('rb') as old,mask.open('rb') as masks:
        while m:=masks.read(1024*1024):check.update((int.from_bytes(old.read(len(m)),'little')^int.from_bytes(m,'little')).to_bytes(len(m),'little'))
    tail=stage/(key+'.tail')
    if tail.exists():
        with tail.open('rb') as f:
            while b:=f.read(1024*1024):check.update(b)
    if check.hexdigest().upper()!=sha(after):raise ValueError('Delta roundtrip mismatch')
    return parts


def prepare(source,output):
    source=source.resolve();output=output.resolve()
    if not output.is_relative_to(ROOT/'artifacts') or output.exists():raise ValueError('Fresh installer artifacts directory required')
    stage=source/'build/next-weapon-reload/final-complete-v7-safe-20261026'
    backup=source/'build/next-weapon-reload/accepted-v7-promotion-20261026-145847/asset-backup'
    manifest=read(stage/'manifest.json');inventory=read(ROOT/'inventory/singularity-v7.json');pins={x['destination'].lower():x for x in inventory['files']}
    output.mkdir(parents=True);groups={};rows=[]
    for row in manifest['files']:
        path=row['path']
        if not path.lower().endswith('.xxx'):continue
        if path.lower().endswith('/rvgame.xxx'):raise ValueError('Protected core cannot be patched')
        pin=pins[path.lower()];before=backup/path;after=stage/'payload'/path
        if sha(before)!=row['before'].upper() or sha(after)!=row['after'].upper() or sha(after)!=pin['sha256']:raise ValueError('Native package provenance mismatch: '+path)
        module=pin['module'];folder=output/module;payload=folder/'stage';payload.mkdir(parents=True,exist_ok=True)
        parts=delta(before,after,payload,hashlib.sha256(path.lower().encode()).hexdigest()[:24])
        file=dict(path=path,entry='',sha256=sha(after),kind='recipe',ownership='local-transform',beforeSha256=sha(before),recipe=parts)
        groups.setdefault(module,[]).append(file);rows.append(dict(path=path,beforeSha256=sha(before),afterSha256=sha(after),beforeBytes=before.stat().st_size,afterBytes=after.stat().st_size,module=module))
    for name,files in groups.items():
        folder=output/name;archive=folder/(name+'-pre-v7-upgrade.zip');payload=folder/'stage'
        with zipfile.ZipFile(archive,'x',zipfile.ZIP_DEFLATED,compresslevel=9) as z:
            for p in sorted(payload.iterdir()):
                info=zipfile.ZipInfo(p.name,(2026,1,1,0,0,0));info.compress_type=zipfile.ZIP_DEFLATED
                with p.open('rb') as src,z.open(info,'w') as dst:shutil.copyfileobj(src,dst)
        module=dict(id=name,labels={'en':name+' exact baseline upgrade','pl':name+' aktualizacja dokładnej bazy'},version='v7-20261026',state='preview',required=False,dependencies=[],downloadBytes=archive.stat().st_size,downloadUrl=None,archiveSha256=sha(archive),expandedBytes=sum(p.stat().st_size for p in payload.iterdir())+sum(r['afterBytes'] for r in rows if r['module']==name),rightsVerified=False,rightsEvidence='',files=files,reason='Offline exact pre-V7 upgrade reconstruction verified; pristine first-install baseline and inserted-content provenance remain separate gates')
        write(folder/'module.json',module)
    report=dict(schema=1,status='OFFLINE_UPGRADE_RECIPES_PREPARED_NOT_PUBLIC_RELEASE',baseline='EXACT_PRE_V7_BACKUP_NOT_PROVEN_PRISTINE_RETAIL',nativeFiles=len(rows),bytesAfter=sum(x['afterBytes'] for x in rows),modules={k:len(v) for k,v in groups.items()},files=rows,originalPackagesArchived=False,publicUpload=False,steamAccess='NONE')
    write(output/'delta-report.json',report);print(json.dumps({k:v for k,v in report.items() if k!='files'}))


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--source',required=True,type=Path);p.add_argument('--output',required=True,type=Path);a=p.parse_args();prepare(a.source,a.output)
