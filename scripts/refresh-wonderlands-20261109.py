"""Refresh only Wonderlands in the verified existing 1.1.0 bundle (no game writes)."""
import argparse,base64,configparser,copy,hashlib,io,json,shutil,zipfile
from pathlib import Path

def sha(data): return hashlib.sha256(data).hexdigest().upper()
def main():
 p=argparse.ArgumentParser()
 for n in ('base','dll','profile','out'): p.add_argument('--'+n,type=Path,required=True)
 a=p.parse_args(); a.out.mkdir(parents=True,exist_ok=False)
 raw=(a.base/'catalog.signed.json').read_bytes()
 assert sha(raw)=='6EF15229D4571B3F4304DCEA26F29CB1B22C28E3A5CA73B1E109FC66C8B62579'
 old=json.loads(base64.b64decode(json.loads(raw)['payload']));cat=copy.deepcopy(old)
 mod=next(m for g in cat['games'] if g['id']=='wonderlands' for m in g['modules'] if m['id']=='vr-core')
 archive=(a.base/'packages/wonderlands/vr-core.zip').read_bytes()
 assert sha(archive)==mod['archiveSha256'].upper()
 dll=a.dll.read_bytes();assert sha(dll)=='504885E44AA1EE13D75C7958670D80FFD41E9389091FAA0C365FC083D0B260A2'
 profile=configparser.ConfigParser(interpolation=None);profile.optionxform=str
 assert profile.read(a.profile,encoding='utf-8-sig')
 assert profile['Simulator']['Enabled']=='0'
 profile['Simulator']['RuntimeJson']='';profile['Simulator']['DataDir']=''
 # Optional NVIDIA runtime is not redistributed in this MIT mod package.
 profile['Performance']['DLSSMode']='0'
 values={f'{s}/{k}':v for s in profile.sections() for k,v in profile[s].items()}
 assert len(values)>100 and values['VR/D3D12']=='1'
 assert not any('C:\\Users' in v or 'C:/Users' in v for v in values.values())
 out=io.BytesIO();seen=set()
 with zipfile.ZipFile(io.BytesIO(archive)) as src,zipfile.ZipFile(out,'w',zipfile.ZIP_DEFLATED) as dst:
  for item in src.infolist():
   data=src.read(item);entry=next((f for f in mod['files'] if f.get('entry')==item.filename),None)
   if entry and entry['kind']=='copy':
    assert sha(data)==entry['sha256'].upper()
    if entry['path'].endswith('/xinput1_3.dll'):data=dll;entry['sha256']=sha(dll);seen.add('dll')
   dst.writestr(item,data)
 assert seen=={'dll'}
 data=out.getvalue();name='wonderlands-vr-core-20261109.zip'
 mod.update(version='1.1.0-20261109',downloadUrl='https://github.com/trilex07/TrilexModInstaller/releases/download/installer-1.1.0/'+name,archiveSha256=sha(data),downloadBytes=len(data))
 with zipfile.ZipFile(io.BytesIO(data)) as z:mod['expandedBytes']=sum(i.file_size for i in z.infolist())
 next(f for f in mod['files'] if f['kind']=='ini')['iniValues']=values
 cat['version']='1.1.4'
 assert [g for g in cat['games'] if g['id']!='wonderlands']==[g for g in old['games'] if g['id']!='wonderlands']
 (a.out/name).write_bytes(data)
 (a.out/'catalog.unsigned.json').write_text(json.dumps(cat,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
 stream=io.StringIO();profile.write(stream);(a.out/'WonderlandsVR.ini').write_text(stream.getvalue(),encoding='utf-8')
 print('PASS unchanged other games, pinned baseline and DLL, portable profile; new package',sha(data))
if __name__=='__main__':main()
