"""Prepare hash-pinned MIT client resources for a desktop source build."""
import argparse,hashlib,urllib.request,zipfile,io
from pathlib import Path

URL='https://github.com/trilex07/TrilexModInstaller/releases/download/installer-1.1.0/psvr2-client-runtime-20261109.zip'
SHA='E4645FAEFB27B530F0862B5223CADA2461135942A26843D762891D81BE096CAC'
FILES={
 'psvr2_toolkit_capi_loader.dll':'30A456CC81C2B0D687FE987C2988EE90DCCB54AC24F52C4AAAF8F79AF7BC82D8',
 'darkness/Darkness2Psvr2Bridge.exe':'9C2C3F69AD107DB5F2BE886D8F749DAC9AB1756A320EF8CF9056F87A84841697',
}
def sha(b):return hashlib.sha256(b).hexdigest().upper()
def main():
 p=argparse.ArgumentParser();p.add_argument('--archive',type=Path);p.add_argument('--out',type=Path,default=Path(__file__).resolve().parents[1]/'artifacts/psvr2-client-runtime');a=p.parse_args()
 data=a.archive.read_bytes() if a.archive else urllib.request.urlopen(URL,timeout=60).read(2_000_000)
 assert sha(data)==SHA,'PSVR2 resource archive SHA256 mismatch'
 with zipfile.ZipFile(io.BytesIO(data)) as z:
  assert set(z.namelist())==set(FILES)|{'SOURCE.md','THIRD-PARTY-PSVR2.txt'}
  for name,expected in FILES.items():assert sha(z.read(name))==expected
  for name in z.namelist():
   target=a.out/name;target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(z.read(name))
 print('PASS pinned PSVR2 client resources prepared; no driver or game installed')
if __name__=='__main__':main()
