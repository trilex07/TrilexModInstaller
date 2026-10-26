"""Prepare local copy/literal recipes against an explicitly fingerprinted owned source snapshot.

No originals enter ZIPs. This does not establish rights for inserted media or claim
that this particular local snapshot represents every Steam retail build.
"""
import argparse, hashlib, json, shutil, struct, subprocess, zipfile, os
from pathlib import Path
ROOT=Path(__file__).resolve().parent.parent

def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest().upper()
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def write(p,v):p.write_text(json.dumps(v,indent=2)+'\n',encoding='utf-8')

def prepare(source,cooked,output):
    source=source.resolve();cooked=cooked.resolve();output=output.resolve()
    if not output.is_relative_to(ROOT/'artifacts') or output.exists():raise ValueError('Fresh private artifacts directory required')
    if sha(cooked/'RvGame.xxx')!='4153C4889C90635069ED226D918CDB76BE618F46B1BAEA152AC10CFFDF59E9EF':raise ValueError('Owned original core identity mismatch')
    pins={x['destination']:x for x in read(ROOT/'inventory/singularity-v7.json')['files']}
    v7=source/'build/next-weapon-reload/final-complete-v7-safe-20261026';manifest=read(v7/'manifest.json')
    output.mkdir(parents=True);decoded=output/'decoded';decoded.mkdir();groups={};rows=[]
    for row in manifest['files']:
        path=row['path']
        if not path.endswith('.xxx'):continue
        pin=pins[path];original=cooked/Path(path).name;target=v7/'payload'/path
        if sha(target)!=pin['sha256']:raise ValueError('Accepted target changed: '+path)
        header=original.read_bytes()[:8];second=struct.unpack_from('<I',header,4)[0]
        decoded_path=decoded/(Path(path).stem+'.u');codec='ue3-whole-lzo' if second==131072 else 'ue3-logical-lzo'
        proc=subprocess.run([str(ROOT/'.build/ue3decode/Trilex.Ue3Decode.exe'),codec,str(original),str(decoded_path)],capture_output=True,text=True)
        if proc.returncode:
            # Uncompressed source is accepted only if its exact bytes equal an existing logical snapshot.
            logical=ROOT.parent/'SingularityVR-assets-local/decomp'/(Path(path).stem+'.u')
            if logical.exists() and sha(logical)==sha(original):decoded_path=original;codec=None
            else:raise ValueError('Original decoding not established: '+path+' '+proc.stderr)
        module=pin['module'];folder=output/module;stage=folder/'stage';stage.mkdir(parents=True,exist_ok=True)
        entry=hashlib.sha256(path.lower().encode()).hexdigest()[:24]+'.svrp';patch=stage/entry
        proc=subprocess.run([str(ROOT/'.build/recipe-maker/recipe-maker.exe'),str(decoded_path),str(target),str(patch)],capture_output=True,text=True,check=True)
        file=dict(path=path,entry='',sha256=sha(target),kind='recipe',ownership='local-transform',beforeSha256=sha(original),recipe=[dict(source='instructions',offset=0,length=target.stat().st_size,entry=entry)])
        if codec:file.update(sourceCodec=codec,decodedSha256=sha(decoded_path),decodedBytes=decoded_path.stat().st_size)
        groups.setdefault(module,[]).append(file)
        rows.append(dict(path=path,originalSha256=sha(original),decodedSha256=sha(decoded_path),targetSha256=sha(target),codec=codec,instructions=proc.stdout.strip(),recipeBytes=patch.stat().st_size,finalBytes=target.stat().st_size))
        fixture=output/'owned-source-layout'/path;fixture.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(original,fixture)
    for name,files in groups.items():
        folder=output/name;stage=folder/'stage';archive=folder/(name+'-owned-source.zip')
        with zipfile.ZipFile(archive,'x',zipfile.ZIP_DEFLATED,compresslevel=9) as z:
            for p in sorted(stage.iterdir()):
                info=zipfile.ZipInfo(p.name,(2026,1,1,0,0,0));info.compress_type=zipfile.ZIP_DEFLATED
                with p.open('rb') as src,z.open(info,'w') as dst:shutil.copyfileobj(src,dst)
        m=dict(id=name,labels={'en':name,'pl':name},version='v7-20261026',state='preview',required=False,dependencies=['vr-core'],downloadBytes=archive.stat().st_size,downloadUrl=None,archiveSha256=sha(archive),expandedBytes=sum(p.stat().st_size for p in stage.iterdir())+sum(r['finalBytes'] for r in rows if pins[r['path']]['module']==name),rightsVerified=False,rightsEvidence='',files=files,reason='Local owned-source reconstruction prepared; inserted media provenance is a separate public release gate')
        write(folder/'module.json',m)
    write(output/'native-report.json',dict(status='PREPARED_REQUIRES_CSHARP_VERIFICATION',sourceIdentity='Exact hashes of local owned snapshot, no assertion of universal pristine retail',files=rows,modules={k:len(v) for k,v in groups.items()},originalFilesArchived=False,publicUpload=False,steamAccess='NONE'))
    print(json.dumps(dict(files=len(rows),modules={k:len(v) for k,v in groups.items()},steamAccess='NONE')))
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--source',required=True,type=Path);p.add_argument('--cooked',required=True,type=Path);p.add_argument('--output',required=True,type=Path);a=p.parse_args();prepare(a.source,a.cooked,a.output)
