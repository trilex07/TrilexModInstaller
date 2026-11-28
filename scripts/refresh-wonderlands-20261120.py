"""Refresh only Wonderlands Steam in the verified current 1.1.3 public bundle."""
import argparse,base64,copy,hashlib,io,json,zipfile
from pathlib import Path

def sha(data):return hashlib.sha256(data).hexdigest().upper()

def main():
 p=argparse.ArgumentParser()
 for name in ['stage','dll','notes','screenshot']:p.add_argument('--'+name,type=Path,required=True)
 a=p.parse_args();current=a.stage/'current';bundle=a.stage/'bundle'
 assert not bundle.exists(),'Use a fresh stage'
 raw=(current/'catalog.signed.json').read_bytes()
 assert sha(raw)=='1A8044F20B92776B2347A28617ED6E08B71A1CA6CFEF4286FA72C604EBB6B85D'
 archive=(current/'Trilex-Mod-Installer-1.1.3-win-x64.zip').read_bytes()
 assert sha(archive)=='39A766694CA71D3581EEDB81DC69F2A4EA93025B07CD3E9CD2E6492D0AAED0A3'
 old=json.loads(base64.b64decode(json.loads(raw)['payload']));cat=copy.deepcopy(old)
 assert old['version']=='1.1.11';cat['version']='1.1.12'
 with zipfile.ZipFile(io.BytesIO(archive)) as z:
  assert z.testzip() is None and z.read('catalog.signed.json')==raw
  for n in z.namelist():assert (bundle/n).resolve().is_relative_to(bundle.resolve())
  z.extractall(bundle)
 game=next(g for g in cat['games'] if g['id']=='wonderlands')
 previous=copy.deepcopy(game)
 mod=game['modules'][0];package=(bundle/'packages/wonderlands/vr-core.zip').read_bytes()
 assert sha(package)==mod['archiveSha256'].upper()
 payload=a.dll.read_bytes();digest=sha(payload)
 out=io.BytesIO();replaced=0
 with zipfile.ZipFile(io.BytesIO(package)) as src,zipfile.ZipFile(out,'w',zipfile.ZIP_DEFLATED) as dst:
  for item in src.infolist():
   value=src.read(item)
   spec=next((f for f in mod['files'] if f.get('entry')==item.filename and f['kind']=='copy'),None)
   if spec:
    assert sha(value)==spec['sha256'].upper()
    if spec['path'].endswith('/xinput1_3.dll'):
     value=payload;spec['sha256']=digest;replaced+=1
   dst.writestr(item,value)
 assert replaced==1
 package=out.getvalue();name='wonderlands-vr-steam-20261120.zip'
 mod.update(version='1.1.3-20261120',archiveSha256=sha(package),downloadBytes=len(package),
  downloadUrl='https://github.com/trilex07/TrilexModInstaller/releases/download/installer-1.1.3/'+name)
 with zipfile.ZipFile(io.BytesIO(package)) as z:mod['expandedBytes']=sum(i.file_size for i in z.infolist())
 values=next(f['iniValues'] for f in mod['files'] if f['kind']=='ini' and 'Input/Layout' in f['iniValues'])
 values.update({'Input/DPadWheel':'0','Input/DPadWheelGaze':'1','Input/SnapTurnDegrees':'0',
  'Input/MenuDoubleL3':'0','Input/MoveStickOuterDeadzonePercent':'0','Input/LeftHandInteract':'0',
  'Hands/BodyDisplay':'0'})
 assert values['Simulator/Enabled']=='0' and not values['Simulator/RuntimeJson'] and not values['Simulator/DataDir']
 for profile in game['buildProfiles']:
  if profile['id']=='steam':profile['modules']=copy.deepcopy(game['modules'])
  else:assert profile==next(x for x in previous['buildProfiles'] if x['id']==profile['id'])
 assert [g for g in cat['games'] if g['id']!='wonderlands']==[g for g in old['games'] if g['id']!='wonderlands']
 (bundle/'packages/wonderlands/vr-core.zip').write_bytes(package)
 (a.stage/name).write_bytes(package)
 (a.stage/'catalog.unsigned.json').write_text(json.dumps(cat,ensure_ascii=False,indent=2)+'\n','utf-8')
 note=a.notes.read_text('utf-8');(bundle/'docs/WONDERLANDS-PATCH-20261120.md').write_text(note,'utf-8')
 (bundle/'docs/WonderlandsVR-Controls-20261120.png').write_bytes(a.screenshot.read_bytes())
 for name in ['README.md','RELEASE-NOTES.md']:
  target=bundle/name;target.write_text(note+'\n\n---\nPrevious release history follows.\n\n'+target.read_text('utf-8-sig'),'utf-8')
 (a.stage/'prepared.json').write_text(json.dumps(dict(dll=digest,catalog='1.1.12',baselineCatalog=sha(raw),otherGamesUnchanged=True,epicUnchanged=True,installerExeUnchanged=True),indent=2))
 print('Prepared only Wonderlands Steam; DLL '+digest)

if __name__=='__main__':main()
