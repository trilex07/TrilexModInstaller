"""Refresh RC7 mod content using reviewed exact sources. Never touches Steam.
Keep release/catalog version RC7 and all unrelated recipes immutable.
"""
import base64,copy,hashlib,json,os,shutil,subprocess,zipfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
OLD=ROOT/'artifacts/rc7-unverified-game-warning'
BASE=ROOT/'artifacts/rc7-explicit-output-exceptions/Trilex-SingularityVR-V7'
OUT=ROOT/'artifacts/rc7-singularity-bugfix-20261103'
RUNTIME=ROOT.parent/'SingularityVR-gravity-audio'
DLL_SHA='7AD356F832ED84E2F599733BE58553BD661381C6C2FD51B4ACDEE25116F38E86'
SOURCE='9488a26a'
BANK_SHA='B8126A759169A89D92FA596D6FBE52E8D94F3BCBA0ACC24FD61967CAD87AFD7A'
FLAGS={'Render/WorldScalePct':'76','Render/CinemaRightLeftExtensionPermille':'20','Input/TmdGazeAim':'1'}
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest().upper()
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def write(p,o):p.write_text(json.dumps(o,indent=2)+'\n',encoding='utf-8')
def prepare():
 assert not OUT.exists(),'Fresh output required; preserve previous release'
 OUT.mkdir(); packages=OUT/'packages';packages.mkdir()
 catalog=json.loads(base64.b64decode(read(BASE/'catalog.signed.json')['payload']))
 write(OUT/'previous-catalog.json',catalog)
 dll=RUNTIME/'build/cinema-right-edge-trial-20/artifacts/d3d9.dll'
 assert sha(dll)==DLL_SHA
 provenance=read(dll.with_name('d3d9.build.json'))
 assert provenance['commit'].startswith(SOURCE) and provenance['architecture']=='x86 PE32' and provenance['optimization']=='/O2'
 tools=ROOT/'src/Trilex.Installer.Tools/bin/Release/net10.0/Trilex.Installer.Tools.dll'
 for m in catalog['games'][0]['modules']:
  mid=m['id']
  if mid not in ('vr-core','audio-remaster','portable-settings'):
   os.link(BASE/'packages'/f'{mid}.zip',packages/f'{mid}.zip');assert sha(packages/f'{mid}.zip')==m['archiveSha256'];continue
  stage=OUT/(mid+'-stage');stage.mkdir()
  with zipfile.ZipFile(BASE/'packages'/f'{mid}.zip') as z:z.extractall(stage)
  m['version']='v7-20261103-rc7-fixes'
  if mid=='vr-core':
   target=stage/'Binaries/d3d9.dll';shutil.copyfile(dll,target)
   next(f for f in m['files'] if f['path']=='Binaries/d3d9.dll')['sha256']=DLL_SHA
  elif mid=='portable-settings':
   settings=next(f['iniValues'] for f in m['files'] if f['path']=='Binaries/SingularityVR.ini')
   settings.update(FLAGS)
   assert not settings.get('Diagnostic/CinemaEyeAudit') # do not ship diagnostic captures
  else:
   f=next(f for f in m['files'] if f['path']=='RvGame/CookedPC/wp_SP_ARMachineGun_SF.xxx')
   assert f['sha256']=='59E33F9950081E91916E07F3A86473B35826572E2DBDFA26D7749A4387D2618A'
   meta=stage/f['recipe'][0]['entry'];op=read(meta)
   fixed=RUNTIME/'build/gravity-audio/repaired'
   assert sha(fixed/'wp_SP_ARMachineGun_SF.xxx')==BANK_SHA
   op.update(encodedSha256=sha(fixed/'fixed-machinegunFire57.ogg'),logicalSha256=sha(fixed/'fixed-logical.u'),targetSha256=BANK_SHA)
   write(meta,op);f['sha256']=BANK_SHA
  manifest=OUT/(mid+'.input.json');write(manifest,m)
  subprocess.run(['dotnet',str(tools),'pack',str(manifest),str(stage),str(packages/f'{mid}.zip')],check=True)
  m.update(read(packages/f'{mid}.zip.module.json'))
 write(OUT/'catalog.json',catalog)
 write(OUT/'runtime-content-evidence.json',dict(status='PREPARED_NOT_PUBLISHED',runtimeSource=provenance['commit'],runtimeSha256=DLL_SHA,rifleBankSha256=BANK_SHA,settings=FLAGS,diagnosticCapture='OFF',changedModules=['vr-core','audio-remaster','portable-settings'],installerVersionUnchanged='1.0.0-rc.7',physical='Weapon attachment and world-scale accepted; captions accepted; 2percent right-eye extension remains PENDING',steamWrites='NONE'))
 print('Prepared RC7 runtime, repaired rifle recipe and three preset deltas; all other payloads preserved')
if __name__=='__main__':prepare()
