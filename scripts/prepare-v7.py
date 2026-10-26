"""Prepare exact accepted mod code plus notices; never read or write Steam.

Derived game assets are inventoried individually, not falsely relicensed MIT.
The prepared code archive is a dependency, not a complete playable V7 package.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import zipfile
from pe_dependencies import inspect

ROOT = Path(__file__).resolve().parent.parent


def sha(path):
    with path.open('rb') as stream: return hashlib.file_digest(stream, 'sha256').hexdigest().upper()


def read(path): return json.loads(path.read_text(encoding='utf-8-sig'))
def write(path, value): path.write_text(json.dumps(value, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')


def prepare(source, sdk, output, system_dinput):
    source, sdk, output, system_dinput = [p.resolve() for p in (source, sdk, output, system_dinput)]
    # Existing reviewed worktree is read-only input; all output stays in installer artifacts.
    if not output.is_relative_to(ROOT/'artifacts') or output.exists(): raise ValueError('Fresh artifacts subdirectory required')
    expected_source = '38ca72f88112bfcbd26addb0d6aa4f76095aa15e'
    inventory = read(ROOT/'inventory/singularity-v7.json')
    if inventory['runtimeSource'] != expected_source: raise ValueError('Unreviewed runtime source')
    if subprocess.check_output(['git','-C',str(source),'diff',expected_source,'--','spikes'],text=True).strip(): raise ValueError('Runtime source changed from accepted V7')
    pins = {x['destination']: x for x in inventory['files']}
    runtime = source/'build/next-weapon-reload/final-complete-v7-safe-20261026/runtime'
    inputs = [(runtime/'d3d9.dll','Binaries/d3d9.dll','MIT'), (runtime/'dinput8.dll','Binaries/dinput8.dll','MIT'), (sdk/'bin/openxr_loader.dll','Binaries/openxr_loader.dll','Apache-2.0')]
    for path, dest, _ in inputs:
        if dest in pins and sha(path) != pins[dest]['sha256']: raise ValueError('Accepted DLL mismatch: '+dest)
        inspect(path.read_bytes())
    system_info = inspect(system_dinput.read_bytes())
    if system_dinput.name.lower() != 'dinput8.dll' or system_dinput.parent.name.lower() != 'syswow64': raise ValueError('Use the owned Windows SysWOW64 DirectInput file')
    output.mkdir(parents=True);stage=output/'stage';stage.mkdir()
    files=[];audit=[]
    def copy(path,dest,license_id):
        target=stage/dest;target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(path,target)
        h=sha(path);files.append(dict(path=dest,entry=dest,sha256=h,kind='copy',ownership='licensed'))
        audit.append(dict(destination=dest,sha256=h,license=license_id,sourceCommit=expected_source if path.is_relative_to(source) else 'local SDK artifact',sourceRelative=str(path.relative_to(source)).replace('\\','/') if path.is_relative_to(source) else path.name))
    for path,dest,license_id in inputs: copy(path,dest,license_id)
    # Preserve full original license text, including exceptions and upstream attribution.
    for path,name,license_id in ((source/'LICENSE','SingularityVR-LICENSE.txt','MIT'),(source/'THIRD-PARTY-NOTICES.txt','SingularityVR-THIRD-PARTY-NOTICES.txt','BSD-2-Clause/HDE-BSD/Apache-2.0'),(sdk/'share/doc/openxr/LICENSE','OpenXR-LICENSE.txt','Apache-2.0')):
        copy(path,'Binaries/vr-assets/licenses/'+name,license_id)
    nis=(source/'spikes/view_matrix/nis/NIS_Config.h').read_text()
    header=nis[:nis.index('//---------------------------------------------------------------------------------')]
    if 'NVIDIA' not in header or 'Permission is hereby granted' not in header: raise ValueError('Missing NIS notice')
    nisfile=output/'NVIDIA-NIS-LICENSE.txt';nisfile.write_text(header,encoding='utf-8');copy(nisfile,'Binaries/vr-assets/licenses/NVIDIA-NIS-LICENSE.txt','MIT')
    system_hash=sha(system_dinput)
    files.append(dict(path='Binaries/dinput8_orig.dll',entry='',sha256=system_hash,kind='recipe',ownership='local-transform',sourceSha256=system_hash,systemSource='windows-directinput8-x86',recipe=[dict(source='original',offset=0,length=system_dinput.stat().st_size)]))
    # Exact accepted settings are retained as review input, never silently enabled as a public preset.
    write(output/'accepted-settings-review.json',dict(iniValues=inventory['iniValues'],ignored=inventory['ignoredIniKeys'],status='REQUIRES_ASSET_COMPLETENESS_AND_PORTABLE_PRESET_REVIEW'))
    module=dict(id='runtime-code',labels={'en':'Accepted V7 runtime code','pl':'Kod wykonawczy V7'},version='v7-20261026',state='preview',required=False,dependencies=[],downloadBytes=0,downloadUrl='https://github.com/trilex07/TrilexModInstaller/releases/download/packages-v7-20261026/runtime-code.zip',archiveSha256=None,expandedBytes=0,rightsVerified=True,rightsEvidence='docs/distribution/v7-code-license-review.json; exact MIT/BSD/Apache notices included; Windows DirectInput stays local',files=files,reason='Dependency package only: native generated assets, complete presets and integration are required before playable installation')
    archive=output/'runtime-code.zip'
    with zipfile.ZipFile(archive,'x',zipfile.ZIP_DEFLATED,compresslevel=9) as z:
        for f in sorted((x for x in files if x['kind']=='copy'),key=lambda x:x['entry']):
            info=zipfile.ZipInfo(f['entry'],(2026,1,1,0,0,0));info.compress_type=zipfile.ZIP_DEFLATED;z.writestr(info,(stage/f['entry']).read_bytes())
    module.update(archiveSha256=sha(archive),downloadBytes=archive.stat().st_size,expandedBytes=sum((stage/x['entry']).stat().st_size for x in files if x['kind']=='copy'))
    write(output/'runtime-code.module.json',module)
    dependencies={dest:inspect(path.read_bytes()) for path,dest,_ in inputs}
    report=dict(schema=1,source=expected_source,archiveSha256=module['archiveSha256'],archiveBytes=module['downloadBytes'],files=audit,systemSource=dict(identity='windows-directinput8-x86',sha256=system_hash,bytes=system_dinput.stat().st_size,distribution='NOT INCLUDED; exact-hash local copy',pe=system_info),imports=dependencies,prerequisites=['Windows x64 with matching SysWOW64 DirectInput identity','Microsoft Visual C++ v14 x86 runtime (not x64-only)','Active compatible OpenXR runtime; SteamVR is not bundled'],status='CODE_DEPENDENCY_PREPARED_NOT_COMPLETE_V7',steamAccess='NONE')
    write(output/'code-review.json',report)
    gates=[]
    inactive_glb={'glove_left.svrg','glove_right.svrg','glove.svrt'}
    for item in inventory['files']:
        status='OMIT_INACTIVE_GLB_NATIVE_MODE' if Path(item['destination']).name in inactive_glb else 'PREPARED_LICENSED_CODE' if item['destination'] in {x[1] for x in inputs} else 'LOCAL_SYSTEM_SOURCE' if item['destination'].endswith('dinput8_orig.dll') else 'VERIFY_ONLY' if item['destination'].endswith(('.exe','/RvGame.xxx')) else 'LOCAL_GENERATION_REQUIRED' if item['ownership']=='original-derived' else 'SOURCE_PROVENANCE_REQUIRED'
        gates.append(dict(destination=item['destination'],sha256=item['sha256'],module=item['module'],status=status))
    write(output/'all-files-readiness.json',dict(files=gates,pins=len(gates),notCoveredByOldInventory=['Binaries/openxr_loader.dll','Visual C++ x86 runtime prerequisite'],publicAssetsPublished=False))
    print(json.dumps({'output':str(output),'archiveBytes':module['downloadBytes'],'archiveSha256':module['archiveSha256'],'status':report['status']}))


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--source',type=Path,required=True);p.add_argument('--sdk',type=Path,required=True);p.add_argument('--output',type=Path,required=True);p.add_argument('--system-dinput',type=Path,default=Path(os.environ.get('WINDIR','C:/Windows'))/'SysWOW64/dinput8.dll');a=p.parse_args();prepare(a.source,a.sdk,a.output,a.system_dinput)
