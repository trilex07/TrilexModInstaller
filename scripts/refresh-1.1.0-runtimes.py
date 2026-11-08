"""Refresh the Singularity and Wonderlands runtimes inside catalog 1.1.0.

Replaces one reviewed DLL per game, updates the reviewed INI values and bumps the
module versions. Archives are rebuilt from the previous verified archives; every
other entry is copied byte for byte. No signing, uploads or game writes.
"""
import argparse,configparser,copy,hashlib,io,json,zipfile
from pathlib import Path

RELEASE='https://github.com/trilex07/TrilexModInstaller/releases/download/installer-1.1.0/'

def sha(data):return hashlib.sha256(data).hexdigest().upper()

def module(cat,game,mid):return next(m for g in cat['games'] if g['id']==game for m in g['modules'] if m['id']==mid)

def repack(mod,archive,replacements):
    if sha(archive)!=mod['archiveSha256'].upper():raise ValueError(mod['id']+': previous archive hash mismatch')
    out=io.BytesIO();seen=set()
    with zipfile.ZipFile(io.BytesIO(archive)) as src,zipfile.ZipFile(out,'w',compression=zipfile.ZIP_DEFLATED) as dst:
        for item in src.infolist():
            data=src.read(item)
            entry=next((f for f in mod['files'] if f.get('entry')==item.filename),None)
            if entry is not None and entry['path'] in replacements:
                data=replacements[entry['path']];entry['sha256']=sha(data);seen.add(entry['path'])
            elif entry is not None and entry['kind']=='copy' and sha(data)!=entry['sha256'].upper():
                raise ValueError(item.filename+': previous payload hash mismatch')
            dst.writestr(item,data)
        expanded=sum(i.file_size for i in dst.infolist())
    if seen!=set(replacements):raise ValueError('Replacement target missing from archive: '+str(set(replacements)-seen))
    packed=out.getvalue()
    mod.update(downloadBytes=len(packed),archiveSha256=sha(packed),expandedBytes=expanded)
    return packed

def profile_values(path):
    ini=configparser.ConfigParser(interpolation=None,strict=False);ini.optionxform=str
    if not ini.read(path,encoding='utf-8-sig'):raise FileNotFoundError(path)
    values={f'{s}/{k}':v for s in ini.sections() for k,v in ini[s].items()}
    for key in ('Simulator/RuntimeJson','Simulator/DataDir'):
        if key in values:values[key]=''
    if len(values)<50:raise ValueError('Profile unexpectedly small: '+str(len(values)))
    if values.get('Simulator/Enabled','0')!='0':raise ValueError('Profile enables the OpenXR simulator')
    return values

def main():
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('catalog','singularity-archive','singularity-dll','wonderlands-archive','wonderlands-dll','wonderlands-profile','output'):p.add_argument('--'+name,type=Path,required=True)
    p.add_argument('--singularity-sha256',required=True);p.add_argument('--wonderlands-sha256',required=True)
    a=p.parse_args()
    cat=json.loads(a.catalog.read_text(encoding='utf-8-sig'));new=copy.deepcopy(cat)
    sdll=a.singularity_dll.read_bytes();wdll=a.wonderlands_dll.read_bytes()
    if sha(sdll)!=a.singularity_sha256.upper() or sha(wdll)!=a.wonderlands_sha256.upper():raise ValueError('Reviewed DLL hash mismatch')

    core=module(new,'singularity','vr-core')
    spack=repack(core,a.singularity_archive.read_bytes(),{'Binaries/d3d9.dll':sdll})
    core.update(version='v7-20261108-gravity-fix',downloadUrl=RELEASE+'singularity-vr-core-20261108.zip')
    settings=module(new,'singularity','portable-settings')
    ini=next(f for f in settings['files'] if f['kind']=='ini' and f['path']=='Binaries/SingularityVR.ini')
    if ini['iniValues'].get('Input/DualHandActionPose')!='1':raise ValueError('TrackedGripWrist requires DualHandActionPose=1')
    ini['iniValues'].update({'Input/TrackedGripWrist':'1','Diagnostic/DropDecalLocalTangent':'1'})
    settings['version']='v7-20261108-gravity-fix'

    wcore=module(new,'wonderlands','vr-core')
    wpack=repack(wcore,a.wonderlands_archive.read_bytes(),{'OakGame/Binaries/Win64/xinput1_3.dll':wdll})
    wini=next(f for f in wcore['files'] if f['kind']=='ini')
    wini['iniValues']=profile_values(a.wonderlands_profile)
    wcore.update(version='1.1.0-20261108',downloadUrl=RELEASE+'wonderlands-vr-core-20261108.zip')

    new['version']='1.1.1'
    a.output.mkdir(parents=True,exist_ok=True)
    (a.output/'singularity-vr-core-20261108.zip').write_bytes(spack)
    (a.output/'wonderlands-vr-core-20261108.zip').write_bytes(wpack)
    (a.output/'catalog.unsigned.json').write_text(json.dumps(new,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('PASS Singularity d3d9',sha(sdll)[:8],'Wonderlands xinput',sha(wdll)[:8],'; other modules unchanged')

if __name__=='__main__':main()
