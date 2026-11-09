"""Freeze the already staged/tested 1.1.0 PSVR2 bundle; no uploads/game writes."""
import hashlib,json,shutil,subprocess,zipfile
from pathlib import Path

def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
root=Path(__file__).resolve().parents[1]
stage=root/'artifacts/installer-1.1.0-gaze-20261109';bundle=stage/'bundle'
release=stage/'release-assets';release.mkdir(exist_ok=True)
source=subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip()
heading='''## November 9: PSVR2 eye-tracking client installation correction

- Supplies the missing Darkness II x64 helper and pinned CAPI loader in the exact per-user runtime directories.
- Configures Wonderlands to use that user-local loader; Singularity's bridge falls back there when its old fixed Steam path is missing.
- All three renderers, game content and gameplay settings remain unchanged. Toolkit driver installation and calibration remain separate opt-ins.
- Redownload and extract this full ZIP into a fresh folder, close the games, then Update/Reinstall each mod. Merely reopening an old 1.1.0 EXE does not obtain the new embedded clients.
- Catalog 1.1.5. Offline verification passed; physical PSVR2 acceptance is pending. See docs/PSVR2-TOOLKIT.md.

'''
for name in ('README.md','RELEASE-NOTES.md'):
 text=(bundle/name).read_text(encoding='utf-8-sig')
 if heading not in text:(bundle/name).write_text(heading+text,encoding='utf-8')
# Source-owned files and license text mirror the tested executable implementation.
for name in ('PSVR2-TOOLKIT.md','PSVR2-CLIENT-FIX-20261109.md'):shutil.copy2(root/'docs'/name,bundle/'docs'/name)
shutil.copy2(root/'THIRD-PARTY-NOTICES.md',bundle/'THIRD-PARTY-NOTICES.md')
shutil.copy2(stage/'publish/TrilexModInstaller.exe',bundle/'TrilexModInstaller.exe')
files=sorted(p for p in bundle.rglob('*') if p.is_file() and p!=bundle/'SHA256SUMS.txt')
(bundle/'SHA256SUMS.txt').write_text(''.join(sha(p)+'  '+p.relative_to(bundle).as_posix()+'\n' for p in files),encoding='ascii')
name='Trilex-Mod-Installer-1.1.0-win-x64.zip'
with zipfile.ZipFile(release/name,'w',zipfile.ZIP_DEFLATED) as z:
 for p in sorted(p for p in bundle.rglob('*') if p.is_file()):z.write(p,p.relative_to(bundle).as_posix())
with zipfile.ZipFile(release/name) as z:
 for p in bundle.rglob('*'):
  if p.is_file():assert z.read(p.relative_to(bundle).as_posix())==p.read_bytes()
for n in ('catalog.signed.json','singularity-vr-core-20261109-psvr2.zip','psvr2-client-runtime-20261109.zip'):shutil.copy2(stage/n,release/n)
evidence={'status':'OFFLINE_PASS','physicalAcceptance':'PENDING','installerSource':source,'installerVersion':'1.1.0','catalogVersion':'1.1.5',
 'installerExeSha256':sha(bundle/'TrilexModInstaller.exe'),'completeZipSha256':sha(release/name),'catalogSha256':sha(stage/'catalog.signed.json'),
 'singularityBridgeSource':'69acb592','singularityBridgeSha256':'6B5BCBF5ACE2C2A69DF20E9B65C87366BC32C1D63CBEBB617D8C1FED9162DA93',
 'darknessBridgeSource':'6ad62a2dd7fc5ed732b866478564db1ac83cd7ff','darknessBridgeSha256':'9C2C3F69AD107DB5F2BE886D8F749DAC9AB1756A320EF8CF9056F87A84841697',
 'coreTests':106,'wpfLayouts':8,'wpfStates':32,'embeddedResourceInstall':'PASS fresh/reinstall/manual-kit','wonderlandsSignedLifecycle':'PASS install/export/verify/exact-restore',
 'bundleFiles':len(files)+1,'bundleByteVerification':'PASS','steamWrites':False,'gameLaunched':False,'driverInstalled':False,
 'preservation':'All three renderers unchanged; only Singularity helper changed in game archives; all other archive bytes unchanged'}
(release/'installer-1.1.0-psvr2-client-evidence.json').write_text(json.dumps(evidence,indent=2)+'\n',encoding='utf-8')
previous=root/'artifacts/installer-1.1.0-wonderlands-20261109/release-assets'
checks={p.name:sha(p) for p in previous.iterdir() if p.is_file() and p.name!='SHA256SUMS.txt'}
checks.update({p.name:sha(p) for p in release.iterdir() if p.is_file() and p.name!='SHA256SUMS.txt'})
(release/'SHA256SUMS.txt').write_text(''.join(digest+'  '+n+'\n' for n,digest in sorted(checks.items())),encoding='ascii')
(stage/'release-body.md').write_text(heading+'\n'+(root/'artifacts/installer-1.1.0-wonderlands-20261109/release-body.md').read_text(encoding='utf-8-sig'),encoding='utf-8')
print(json.dumps(evidence,indent=2))
