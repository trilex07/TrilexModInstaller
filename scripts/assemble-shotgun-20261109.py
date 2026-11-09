"""Freeze the tested shotgun refresh; no upload or game writes."""
import hashlib,json,shutil,zipfile
from pathlib import Path

def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
root=Path(__file__).resolve().parents[1];out=root/'artifacts/installer-1.1.0-shotgun-20261109'
bundle=out/'bundle';public=out/'release-assets';public.mkdir(exist_ok=False)
heading='''## November 9: Singularity shotgun audio compatibility repair

- Repairs the shotgunFire35 Vorbis header rejected by the original game decoder. Encoded sound, timing, channels and all other sound-bank content stay unchanged.
- Catalog 1.1.6; original and repaired recipes remain hash-pinned. No renderer, controller, PSVR2 or other game-package change.
- Redownload this full ZIP into a fresh folder, then Update/Reinstall Singularity with Audio Remaster selected. Reopening an older 1.1.0 EXE does not update its embedded generator.
- Native codec and offline install/reinstall/repair/uninstall checks passed. The reported player's crash still requires player verification.

'''
for n in ('README.md','RELEASE-NOTES.md'):
    p=bundle/n;p.write_text(heading+p.read_text(encoding='utf-8-sig'),encoding='utf-8')
shutil.copy2(out/'publish/TrilexModInstaller.exe',bundle/'TrilexModInstaller.exe')
shutil.copy2(out/'catalog.signed.json',bundle/'catalog.signed.json')
files=sorted(p for p in bundle.rglob('*') if p.is_file() and p.name!='SHA256SUMS.txt')
(bundle/'SHA256SUMS.txt').write_text(''.join(sha(p)+'  '+p.relative_to(bundle).as_posix()+'\n' for p in files),encoding='ascii')
name='Trilex-Mod-Installer-1.1.0-win-x64.zip'
with zipfile.ZipFile(public/name,'x',zipfile.ZIP_DEFLATED) as z:
    for p in sorted(p for p in bundle.rglob('*') if p.is_file()):z.write(p,p.relative_to(bundle).as_posix())
with zipfile.ZipFile(public/name) as z:
    assert z.testzip() is None
    for p in bundle.rglob('*'):
        if p.is_file():assert z.read(p.relative_to(bundle).as_posix())==p.read_bytes()
base=root.parent/'TrilexModInstaller/artifacts/installer-1.1.0-gaze-20261109/bundle'
assert sorted(p.relative_to(base).as_posix() for p in base.rglob('*') if p.is_file())==sorted(p.relative_to(bundle).as_posix() for p in bundle.rglob('*') if p.is_file())
changed=[p.relative_to(base).as_posix() for p in base.rglob('*') if p.is_file() and p.read_bytes()!=(bundle/p.relative_to(base)).read_bytes()]
assert sorted(changed)==sorted(['README.md','RELEASE-NOTES.md','SHA256SUMS.txt','TrilexModInstaller.exe','catalog.signed.json','packages/audio-remaster.zip'])
for n in ('catalog.signed.json','singularity-audio-remaster-20261109-shotgun.zip'):shutil.copy2(out/n,public/n)
evidence=dict(status='OFFLINE_PASS',catalogVersion='1.1.6',installerVersion='1.1.0',coreTests=106,producerTests=15,
    nativeVorbisHeaders='54/54 PASS',decodedAudio='UNCHANGED',ownedSourceWorkerAndConsumer='EXACT_OUTPUT_PASS',
    installLifecycle='install/reinstall/damage-detection/repair/uninstall/exact-restore PASS',
    playerCrashCausality='UNCONFIRMED',physicalAcceptance='PENDING',steamWrites=False,changedBundleFiles=changed,
    bankSha256='88548AA9F8F20044418B9CE121FDF3C74923A6BBC0DB6D39A2F2AEE340AE383F',
    workerSha256=sha(root/'.build/asset-worker/Trilex.AssetWorker.exe'),installerSha256=sha(bundle/'TrilexModInstaller.exe'),
    completeZipSha256=sha(public/name),catalogSha256=sha(out/'catalog.signed.json'))
(public/'installer-1.1.0-shotgun-evidence.json').write_text(json.dumps(evidence,indent=2)+'\n')
prior=root.parent/'TrilexModInstaller/artifacts/installer-1.1.0-gaze-20261109/release-assets/SHA256SUMS.txt'
checks={line.split('  ',1)[1]:line.split('  ',1)[0] for line in prior.read_text(encoding='utf-8-sig').splitlines() if line.strip()}
checks.update({p.name:sha(p) for p in public.iterdir() if p.is_file()})
(public/'SHA256SUMS.txt').write_text(''.join(h+'  '+n+'\n' for n,h in sorted(checks.items())),encoding='ascii')
(out/'release-body-prefix.md').write_text(heading,encoding='utf-8')
print(json.dumps(evidence,indent=2))
