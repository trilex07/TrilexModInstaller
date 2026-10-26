"""Read accepted metadata only. Never copies/extracts game payloads."""
import argparse, hashlib, json
from pathlib import Path

p=argparse.ArgumentParser();p.add_argument('source',type=Path);p.add_argument('--output',type=Path,default=Path('inventory/singularity-v7.json'));a=p.parse_args()
def read(path):return json.loads(path.read_text(encoding='utf-8-sig'))
profile=read(a.source/'deployment/accepted-profile.json')
release=read(a.source/'docs/release/first-release-baseline-20261026.json')
stage=a.source/'build/next-weapon-reload/final-complete-v7-safe-20261026'
delta={f['path'].lower():f for f in read(stage/'manifest.json')['files']}
def classify(path):
    s=path.lower()
    if s.endswith('.exe') or s.endswith('/rvgame.xxx') or 'dinput8_orig' in s:return 'owned-game-prerequisite','original-game','verify-only',[]
    if s.endswith('.dll'):return 'vr-core','mod-code-review-required','reproducible-build-and-notices',[]
    if s.endswith('.xxx'):
        module='polish-dubbing' if '_loc_' in s else 'ui-hud' if '/ui_' in s else 'audio-remaster'
        return module,'original-derived','verified-local-reconstruction-required',['vr-core']
    if 'texture-override' in s:return 'hd-textures','original-derived','source-rights-or-local-generation-required',['vr-core']
    if '/localization/' in s:return 'polish-text' if 'polish_text' in s else 'polish-dubbing','mixed-review-required','text-voice-rights-and-native-bank-review',['vr-core']
    if any(x in s for x in ('glove-animation','/knife/')):return 'vr-core','original-derived','verified-local-generation-required',[]
    if '/hud/' in s:return 'ui-hud','mixed-review-required','atlas-provenance-review',['vr-core']
    if s.endswith('.ini'):return 'configuration','mod-configuration','merge-keys-not-whole-user-file',['vr-core']
    return 'unclassified','review-required','release-blocked',['vr-core']
files=[]
for f in profile['files']:
    path=f['path'];module,ownership,gate,deps=classify(path);d=delta.get(path.lower(),{})
    files.append(dict(destination=path,sha256=f['sha256'].upper(),module=module,ownership=ownership,version='accepted-v7-20261026',dependencies=deps,source=('accepted-runtime-build' if path.lower().endswith('.dll') else 'accepted-v7-payload' if d else 'accepted-profile-dependency'),beforeSha256=d.get('before'),distributionGate=gate,distributable=False,acceptance='V7 general PSVR2 acceptance; individual asset rights not established'))
assert len(files)==release['accepted_file_pins'] and len({x['destination'].lower() for x in files})==len(files)
out=dict(schema='trilex-v7-inventory-v1',runtimeSource=release['runtime_source_commit'],profileSha256=hashlib.sha256((a.source/'deployment/accepted-profile.json').read_bytes()).hexdigest().upper(),acceptedPins=len(files),iniDestination=profile['iniPath'],iniValues=profile['ini'],ignoredIniKeys=profile['ignoredIniKeys'],physicalAcceptance=release['physical'],files=files,withheld=['Original RvGame.xxx core remains unchanged; TMD impulse sound replacement withheld','No generated voice or original-derived texture rights inferred from runtime acceptance'],requirements=['Steam owned Singularity compatible fingerprint','Active OpenXR runtime and configured controllers','Per-asset redistribution or local reconstruction audit','OpenXR/MinHook and all bridge dependency license notices'],releaseStatus='BLOCKED_FOR_PUBLIC_GAME_PACKAGES')
a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');print(f'{len(files)} accepted pins + {len(profile["ini"])} INI keys inventoried; no payload copied')
