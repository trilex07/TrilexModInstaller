"""Repack only the headset-accepted Wonderlands DLL and reviewed feature keys.

Preserves the downloaded installer EXE and every other game's package. Never
writes game files. Run prepare, sign/verify with existing tools, then assemble.
"""
import argparse, base64, copy, hashlib, io, json, zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / 'artifacts/installer-wonderlands-accepted-20261110'
NAME = 'Trilex-Mod-Installer-1.1.0-win-x64.zip'
PACKAGE = 'wonderlands-vr-core-20261110-headset.zip'
PIN = 'EFC1685E762ED4C1F55AB674FD084CF28701A5468FD8EDFFFBF8F495B6D4D491'
CATPIN = '3287C83259920A282D03469ECF3E528D34E3A57011B7CFC9A1FF701FE0A3892C'
CHANGES = {'Hands/ArmedRightArmIK':'1', 'Modes/CenterActionSkillCamera':'1',
 'HUD/MenuFigureLowerCm':'12', 'HUD/KeepLootCardOnPanel':'1',
 'HUD/HideCrosshair':'1', 'HUD/GunTargetHealthbar':'1',
 'HUD/GazeTargetHealthbar':'1', 'HUD/WorldTargetHealthbar':'1',
 'EyeTracking/LootCardFocusHold':'1'}
NOTE = '''## November 10: headset-accepted Wonderlands Steam follow-ups

- Publishes exact headset-tested DLL EFC1685E (source c967f44), with tracked weapon selection, sprint elbow correction, Graveborn combat camera centering, enemy health, loot focus and inventory/HUD adjustments enabled in the portable profile.
- Known OPEN issues: green weapon-optic reticle appears in the left eye only; DLSS Quality renders gameplay in the left eye only; enemy name/health intermittently returns to the center panel.
- Epic is not supported by this package yet. No claim that every weapon, skill or UI layout has been verified.
- Keeps the installer 1.1.0 download link, executable, other games and PSVR2 clients. Catalog 1.1.7. Redownload/extract the full ZIP into a fresh folder, then Update/Reinstall Wonderlands with the game closed; existing installers can fetch the updated signed catalog.
- Optional NVIDIA DLSS runtime is not redistributed. Newer offline candidates are excluded.

'''

def sha(data): return hashlib.sha256(data).hexdigest().upper()
def catalog(data): return json.loads(base64.b64decode(json.loads(data)['payload']))
def module(cat): return next(m for g in cat['games'] if g['id']=='wonderlands'
                             for m in g['modules'] if m['id']=='vr-core')

def prepare(dll):
    current = STAGE/'current'
    checks = dict((line.split('  ',1)[1], line.split('  ',1)[0].upper())
                  for line in (current/'SHA256SUMS.txt').read_text('utf-8-sig').splitlines() if line)
    raw = (current/'catalog.signed.json').read_bytes()
    assert sha(raw) == CATPIN == checks['catalog.signed.json']
    data = (current/NAME).read_bytes(); assert sha(data)==checks[NAME]
    bundle=STAGE/'bundle'; assert not bundle.exists(), 'Fresh bundle required'
    with zipfile.ZipFile(io.BytesIO(data)) as z:
        assert z.testzip() is None
        for n in z.namelist():
            target=(bundle/n).resolve()
            assert target.is_relative_to(bundle.resolve()), 'Unsafe ZIP path'
        z.extractall(bundle)
    assert (bundle/'catalog.signed.json').read_bytes()==raw
    old=catalog(raw); cat=copy.deepcopy(old); mod=module(cat)
    assert old['version']=='1.1.6'
    payload=dll.read_bytes(); assert sha(payload)==PIN
    archive=(bundle/'packages/wonderlands/vr-core.zip').read_bytes()
    assert sha(archive)==mod['archiveSha256'].upper()
    out=io.BytesIO(); replaced=0
    with zipfile.ZipFile(io.BytesIO(archive)) as src,zipfile.ZipFile(out,'w',zipfile.ZIP_DEFLATED) as dst:
        for entry in src.infolist():
            value=src.read(entry)
            spec=next((f for f in mod['files'] if f.get('entry')==entry.filename and f['kind']=='copy'),None)
            if spec:
                assert sha(value)==spec['sha256'].upper()
                if spec['path'].endswith('/xinput1_3.dll'):
                    value=payload;spec['sha256']=PIN;replaced+=1
            dst.writestr(entry,value)
    assert replaced==1
    data=out.getvalue();(STAGE/PACKAGE).write_bytes(data)
    (bundle/'packages/wonderlands/vr-core.zip').write_bytes(data)
    values=next(f for f in mod['files'] if f['kind']=='ini')['iniValues']
    before=values.copy();values.update(CHANGES)
    assert {k:v for k,v in values.items() if k not in CHANGES}=={k:v for k,v in before.items() if k not in CHANGES}
    assert values['Simulator/Enabled']=='0'
    mod.update(version='1.1.0-20261110-headset',archiveSha256=sha(data),downloadBytes=len(data),
      downloadUrl='https://github.com/trilex07/TrilexModInstaller/releases/download/installer-1.1.0/'+PACKAGE)
    with zipfile.ZipFile(io.BytesIO(data)) as z: mod['expandedBytes']=sum(i.file_size for i in z.infolist())
    assert [g for g in cat['games'] if g['id']!='wonderlands']==[g for g in old['games'] if g['id']!='wonderlands']
    cat['version']='1.1.7'
    (STAGE/'catalog.unsigned.json').write_text(json.dumps(cat,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('PASS baseline digests, accepted DLL, scoped package and nine INI keys; other games unchanged')

def assemble():
    bundle=STAGE/'bundle'; public=STAGE/'release-assets'; public.mkdir(exist_ok=False)
    raw=(STAGE/'catalog.signed.json').read_bytes()
    assert catalog(raw)==json.loads((STAGE/'catalog.unsigned.json').read_text('utf-8'))
    (bundle/'catalog.signed.json').write_bytes(raw)
    for name in ('README.md','RELEASE-NOTES.md'):
        p=bundle/name;p.write_text(NOTE+p.read_text('utf-8-sig'),encoding='utf-8')
    files=sorted(p for p in bundle.rglob('*') if p.is_file() and p.name!='SHA256SUMS.txt')
    (bundle/'SHA256SUMS.txt').write_text(''.join(sha(p.read_bytes())+'  '+p.relative_to(bundle).as_posix()+'\n' for p in files),encoding='ascii')
    with zipfile.ZipFile(STAGE/'current'/NAME) as original:
        changed=[n for n in original.namelist() if not n.endswith('/') and original.read(n)!=(bundle/n).read_bytes()]
    assert set(changed)=={'catalog.signed.json','packages/wonderlands/vr-core.zip','README.md','RELEASE-NOTES.md','SHA256SUMS.txt'},changed
    with zipfile.ZipFile(public/NAME,'x',zipfile.ZIP_DEFLATED) as z:
        for p in sorted(bundle.rglob('*')):
            if p.is_file():z.write(p,p.relative_to(bundle).as_posix())
    with zipfile.ZipFile(public/NAME) as z:
        assert z.testzip() is None
        for p in bundle.rglob('*'):
            if p.is_file():assert z.read(p.relative_to(bundle).as_posix())==p.read_bytes()
    for n in ('catalog.signed.json',PACKAGE):(public/n).write_bytes((STAGE/n).read_bytes())
    evidence={'status':'OFFLINE_PACKAGE_PASS','dll':PIN,'catalogVersion':'1.1.7','changedBundleFiles':changed,
      'installerExeUnchanged':True,'otherGamesUnchanged':True,'gameWrites':False,'gameLaunched':False,
      'headset':'User accepted exact DLL; three new issues OPEN. Package lifecycle evidence recorded separately.'}
    (public/'wonderlands-headset-evidence-20261110.json').write_text(json.dumps(evidence,indent=2)+'\n')
    checks=dict((line.split('  ',1)[1],line.split('  ',1)[0]) for line in (STAGE/'current/SHA256SUMS.txt').read_text('utf-8-sig').splitlines() if line)
    checks.update({p.name:sha(p.read_bytes()) for p in public.iterdir()})
    (public/'SHA256SUMS.txt').write_text(''.join(v+'  '+k+'\n' for k,v in sorted(checks.items())),encoding='ascii')
    print(json.dumps(evidence))

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('action',choices=['prepare','assemble']);p.add_argument('--dll',type=Path)
    args=p.parse_args()
    if args.action=='prepare':prepare(args.dll)
    else:assemble()
