"""Verify the RC7 refresh closure, exact allowed changes and local audio output."""
import base64,hashlib,json,zipfile
from pathlib import Path
from importlib.util import spec_from_file_location,module_from_spec
ROOT=Path(__file__).resolve().parents[1]
spec=spec_from_file_location('refresh',ROOT/'scripts/refresh-rc7-singularity-bugfix.py');r=module_from_spec(spec);spec.loader.exec_module(r)
def verify():
 old={m['id']:m for m in r.read(r.OUT/'previous-catalog.json')['games'][0]['modules']}
 catalog=r.read(r.OUT/'catalog.json');new={m['id']:m for m in catalog['games'][0]['modules']}
 assert catalog['version']=='1.0.0-rc.7' and old.keys()==new.keys()
 changes=[]
 for mid,m in new.items():
  archive=r.OUT/'packages'/f'{mid}.zip';assert r.sha(archive)==m['archiveSha256']
  assert archive.stat().st_size==m['downloadBytes']
  a=old[mid]
  for key in a:
   if key not in ('files','version','archiveSha256','downloadBytes','expandedBytes'):assert m[key]==a[key],(mid,key)
  if mid not in ('vr-core','audio-remaster','portable-settings'):assert m==a
  before={f['path']:f for f in a['files']};assert before.keys()=={f['path'] for f in m['files']}
  with zipfile.ZipFile(archive) as z:
   assert z.testzip() is None
   for f in m['files']:
    if f['kind']=='copy':assert hashlib.sha256(z.read(f['entry'])).hexdigest().upper()==f['sha256']
    b=before[f['path']]
    if b==f:continue
    changes.append(f['path'])
    if f['path']=='Binaries/d3d9.dll':assert f==dict(b,sha256=r.DLL_SHA)
    elif f['path']=='RvGame/CookedPC/wp_SP_ARMachineGun_SF.xxx':assert f==dict(b,sha256=r.BANK_SHA)
    elif f['path']=='Binaries/SingularityVR.ini':assert f==dict(b,iniValues=dict(b['iniValues'],**r.FLAGS))
    else:raise AssertionError(('Unexpected changed file',mid,f['path']))
  if mid=='audio-remaster':
   with zipfile.ZipFile(r.BASE/'packages/audio-remaster.zip') as prior,zipfile.ZipFile(archive) as after:
    assert set(prior.namelist())==set(after.namelist())
    for name in prior.namelist():
     if name!='04_weapon_walkiria_ar9.json':assert prior.read(name)==after.read(name),name
     else:
      b=json.loads(prior.read(name));f=json.loads(after.read(name))
      expected=dict(b,encodedSha256=r.sha(r.RUNTIME/'build/gravity-audio/repaired/fixed-machinegunFire57.ogg'),logicalSha256=r.sha(r.RUNTIME/'build/gravity-audio/repaired/fixed-logical.u'),targetSha256=r.BANK_SHA)
      assert f==expected
 assert sorted(changes)==sorted(['Binaries/d3d9.dll','Binaries/SingularityVR.ini','RvGame/CookedPC/wp_SP_ARMachineGun_SF.xxx'])
 assert r.sha(r.OUT/'audio-check/repaired.xxx')==r.BANK_SHA
 evidence=dict(status='PASS',catalogVersion=catalog['version'],changedFiles=changes,changedModules=['vr-core','audio-remaster','portable-settings'],unchangedModules=len(new)-3,rightsDependenciesAndSources='UNCHANGED',allArchiveHashes='PASS',localAudioWorkerExactReproduction='PASS',settings=r.FLAGS,steamWrites='NONE',physicalAcceptance='NOT RUN by this fixture; cinema2percent remains PENDING')
 r.write(r.OUT/'content-verification.json',evidence);print(json.dumps(evidence,indent=2))
if __name__=='__main__':verify()
