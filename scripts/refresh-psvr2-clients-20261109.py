"""Scoped repack from the verified 1.1.4 bundle. Never writes game files."""
import argparse,base64,copy,hashlib,io,json,shutil,zipfile
from pathlib import Path

def sha(b):return hashlib.sha256(b).hexdigest().upper()
def main():
 p=argparse.ArgumentParser()
 for n in ('base','bridge','out'):p.add_argument('--'+n,type=Path,required=True)
 a=p.parse_args();a.out.mkdir(parents=True,exist_ok=True)
 assert not (a.out/'catalog.unsigned.json').exists(), 'Use a fresh output catalog'
 raw=(a.base/'catalog.signed.json').read_bytes()
 assert sha(raw)=='EDEE069C047E5723A34327C42CE33F4A9A5284716D15529C607A03DB13CC5943'
 old=json.loads(base64.b64decode(json.loads(raw)['payload']));cat=copy.deepcopy(old)
 bridge=a.bridge.read_bytes();assert sha(bridge)=='6B5BCBF5ACE2C2A69DF20E9B65C87366BC32C1D63CBEBB617D8C1FED9162DA93'
 for game in cat['games']:
  mod=next(m for m in game['modules'] if m['id']=='vr-core')
  if game['id']=='singularity':
   data=(a.base/'packages/vr-core.zip').read_bytes();assert sha(data)==mod['archiveSha256'].upper()
   out=io.BytesIO();seen=0
   with zipfile.ZipFile(io.BytesIO(data)) as src,zipfile.ZipFile(out,'w',zipfile.ZIP_DEFLATED) as dst:
    for item in src.infolist():
     value=src.read(item)
     entry=next((f for f in mod['files'] if f.get('entry')==item.filename and f['kind']=='copy'),None)
     if entry:
      assert sha(value)==entry['sha256'].upper()
      if entry['path']=='Binaries/SingularityPsvr2Bridge.exe':
       value=bridge;entry['sha256']=sha(value);seen+=1
     dst.writestr(item,value)
   assert seen==1
   new=out.getvalue();name='singularity-vr-core-20261109-psvr2.zip';(a.out/name).write_bytes(new)
   mod.update(version='v7-20261109-psvr2-clients',downloadUrl='https://github.com/trilex07/TrilexModInstaller/releases/download/installer-1.1.0/'+name,archiveSha256=sha(new),downloadBytes=len(new))
   with zipfile.ZipFile(io.BytesIO(new)) as z:mod['expandedBytes']=sum(i.file_size for i in z.infolist())
   # Renderer and every other payload must remain byte-exact.
   with zipfile.ZipFile(io.BytesIO(data)) as before,zipfile.ZipFile(io.BytesIO(new)) as after:
    assert before.namelist()==after.namelist()
    assert [n for n in before.namelist() if before.read(n)!=after.read(n)]==[next(f['entry'] for f in mod['files'] if f['path']=='Binaries/SingularityPsvr2Bridge.exe')]
  elif game['id'] in ('darkness2','wonderlands'):
   mod['version']='1.1.0-20261109-psvr2-clients'
 # Only the scoped Seeker-independent helper and version stamps differ.
 restored=copy.deepcopy(cat)
 for game,previous in zip(restored['games'],old['games']):
  for mod,prior in zip(game['modules'],previous['modules']):
   if mod['id']=='vr-core':
    if game['id']=='singularity':mod.update(copy.deepcopy(prior))
    else:mod['version']=prior['version']
 assert restored==old
 cat['version']='1.1.5'
 (a.out/'catalog.unsigned.json').write_text(json.dumps(cat,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
 print('PASS pinned baseline, bridge-only repack, unchanged renderer/game assets and other archives; catalog1.1.5')
if __name__=='__main__':main()
