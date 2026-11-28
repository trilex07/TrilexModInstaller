"""Assemble and byte-audit the scoped Wonderlands refresh before upload."""
import argparse,hashlib,json,shutil,zipfile
from pathlib import Path

def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()

def main():
 p=argparse.ArgumentParser();p.add_argument('--stage',type=Path,required=True);a=p.parse_args()
 r=a.stage;b=r/'bundle';public=r/'release-assets';public.mkdir(exist_ok=False)
 shutil.copy2(r/'catalog.signed.json',b/'catalog.signed.json')
 (b/'SHA256SUMS.txt').write_text(''.join(sha(x)+'  '+x.relative_to(b).as_posix()+'\n' for x in sorted(b.rglob('*')) if x.is_file() and x.name!='SHA256SUMS.txt'),'ascii')
 with zipfile.ZipFile(r/'current/Trilex-Mod-Installer-1.1.3-win-x64.zip') as old:
  names={n for n in old.namelist() if not n.endswith('/')}
  changed=[n for n in sorted(names) if old.read(n)!=(b/n).read_bytes()]
  assert set(changed)=={'catalog.signed.json','packages/wonderlands/vr-core.zip','README.md','RELEASE-NOTES.md','SHA256SUMS.txt'},changed
  added={x.relative_to(b).as_posix() for x in b.rglob('*') if x.is_file()}-names
  assert added=={'docs/WONDERLANDS-PATCH-20261120.md','docs/WonderlandsVR-Controls-20261120.png'},added
  assert old.read('TrilexModInstaller.exe')==(b/'TrilexModInstaller.exe').read_bytes()
 for profile in ['steam','epic']:
  result=json.loads((r/f'lifecycle-{profile}/verification.json').read_text())
  assert result['status']=='PASS' and result['restore']=='byte-exact'
 output=public/'Trilex-Mod-Installer-1.1.3-win-x64.zip'
 with zipfile.ZipFile(output,'x',zipfile.ZIP_DEFLATED) as z:
  for x in sorted(b.rglob('*')):
   if x.is_file():z.write(x,x.relative_to(b).as_posix())
 with zipfile.ZipFile(output) as z:
  assert z.testzip() is None
  for x in b.rglob('*'):
   if x.is_file():assert z.read(x.relative_to(b).as_posix())==x.read_bytes()
 for n in ['catalog.signed.json','wonderlands-vr-steam-20261120.zip']:shutil.copy2(r/n,public/n)
 for n in ['WONDERLANDS-PATCH-20261120.md','WonderlandsVR-Controls-20261120.png']:shutil.copy2(b/'docs'/n,public/n)
 evidence=json.loads((r/'prepared.json').read_text())
 evidence.update(status='OFFLINE_PACKAGE_PASS',changedPriorFiles=changed,addedFiles=sorted(added),lifecycle=['steam PASS','epic PASS'],physicalNewBindings='NOT RUN',runtimeLaunch=False)
 (public/'wonderlands-patch-evidence-20261120.json').write_text(json.dumps(evidence,indent=2)+'\n','utf-8')
 checks={line.split('  ',1)[1]:line.split('  ',1)[0] for line in (r/'current/SHA256SUMS.txt').read_text('utf-8-sig').splitlines() if line}
 checks.update({x.name:sha(x) for x in public.iterdir()})
 (public/'SHA256SUMS.txt').write_text(''.join(h+'  '+n+'\n' for n,h in sorted(checks.items())),'ascii')
 print(json.dumps(evidence))

if __name__=='__main__':main()
