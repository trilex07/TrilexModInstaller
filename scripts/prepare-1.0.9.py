from pathlib import Path
import configparser,json,hashlib,zipfile,shutil,subprocess
root=Path.cwd();out=root/'artifacts/installer-1.0.9';tiny=Path('C:/Users/Trilex/Tiny Tina VR')
assert not out.exists();out.mkdir()
shutil.copytree(root/'artifacts/installer-1.0.8/bundle',out/'bundle')
profile=configparser.ConfigParser(interpolation=None);profile.optionxform=str;profile.read(root/'config/WonderlandsVR.ini',encoding='utf-8-sig')
assert profile['Simulator']['Enabled']=='0' and not profile['Simulator']['RuntimeJson'] and not profile['Simulator']['DataDir']
values={s+'/'+k:v for s in profile.sections() for k,v in profile[s].items()}
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest().upper()
expected={'xinput1_3.dll':'00919C692D06B031DBFB36DB368ADBE45762EBEC0E016124B34716F7A1E2D809','openxr_loader.dll':'66946EA6E9CC2649E299EC412521C5EAFE56C635D05055097FE4DB81E11932DF'}
files=[];archive=out/'bundle/packages/wonderlands/vr-core.zip';archive.parent.mkdir(parents=True)
assets={name:(tiny/'build/Release'/name).read_bytes() for name in expected}
for name in expected:assert hashlib.sha256(assets[name]).hexdigest().upper()==expected[name]
assets['LICENSE-WonderlandsVR.txt']=(root/'LICENSE').read_bytes()
assets['LICENSE-MinHook.txt']=(tiny/'third_party/minhook/LICENSE.txt').read_bytes()
assets['LICENSE-OpenXR.txt']=(root/'artifacts/installer-1.0.8/bundle/licenses/vr-runtime/OpenXR-LICENSE.txt').read_bytes()
assets['THIRD-PARTY-PSVR2.txt']=b'PSVR2 Toolkit is optional and NOT redistributed. Gaze status ABI declarations originate from Bnuuy Solutions PSVR2Toolkit (MIT). Install its driver separately for Sense triggers and gaze. https://github.com/BnuuySolutions/PSVR2Toolkit\n'
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=9) as z:
 for name,data in assets.items():
  z.writestr('runtime/'+name,data)
  files.append(dict(path='OakGame/Binaries/Win64/'+name,entry='runtime/'+name,sha256=hashlib.sha256(data).hexdigest().upper(),kind='copy',ownership='mod-authored' if name in ['xinput1_3.dll','LICENSE-WonderlandsVR.txt','THIRD-PARTY-PSVR2.txt'] else 'licensed'))
files.append(dict(path='OakGame/Binaries/Win64/WonderlandsVR.ini',entry='',sha256='',kind='ini',ownership='mod-authored',iniValues=values))
cat=json.loads((root/'artifacts/installer-1.0.8/catalog.unsigned.json').read_text(encoding='utf-8-sig'))
cat['version']='1.0.9';cat['expiresUtc']='2027-11-07T00:00:00Z'
cat['games']=[g for g in cat['games'] if g['id']!='wonderlands']
cat['games'].append(dict(id='wonderlands',name="Tiny Tina's Wonderlands VR",state='stable',adapter='WonderlandsGameAdapter',steamAppId='1286680',executable='OakGame/Binaries/Win64/Wonderlands.exe',exeSha256=['EE33D5FB008AEF854FFA5A71E46940FA4ADD5C45251D3E61C4749B91DF2CD46D'],modules=[dict(id='vr-core',labels={'en':'Native VR, tracked hands, wrist HUD and haptics','pl':'Natywne VR, śledzone dłonie, HUD i wibracje'},version='1.0.9',state='stable',required=True,dependencies=[],downloadBytes=archive.stat().st_size,downloadUrl='https://github.com/trilex07/TrilexModInstaller/releases/download/installer-1.0.9/wonderlands-vr-core.zip',archiveSha256=sha(archive),expandedBytes=sum(map(len,assets.values())),rightsVerified=True,rightsEvidence='Original Trilex runtime; MinHook BSD-2 and OpenXR Apache-2.0 notices included; no game binaries/assets or UEVR code.',files=files,reason='')]))
(out/'catalog.unsigned.json').write_text(json.dumps(cat,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
identity=subprocess.check_output(['git','-C',str(tiny),'rev-parse','HEAD'],text=True).strip()
(out/'wonderlands-provenance.json').write_text(json.dumps({'sourceCommit':identity,'dlls':expected,'profile':'config/WonderlandsVR.ini','profileSha256':sha(root/'config/WonderlandsVR.ini'),'installedDllByteEqual':True,'privatePathsRemoved':['Simulator/RuntimeJson','Simulator/DataDir'],'gameExecutableIncluded':False,'newHeadsetValidation':'NOT RUN'},indent=2)+'\n')
print('Prepared 1.0.9; existing game catalogs/packages unchanged; Tiny package',sha(archive),archive.stat().st_size)
