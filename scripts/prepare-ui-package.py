"""Stage authored V7 UI, never commercial meshes or textures.

The optional accepted-root is read-only and limited to named mod HUD artwork.
Every legacy file must match the accepted inventory before copying. Provenance
of user-supplied legacy art remains a separate release gate.
"""
import argparse, hashlib, json, shutil
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
def prepare(source,output,accepted_root=None):
    source=source.resolve();output=output.resolve()
    if output.exists() or not output.is_relative_to(ROOT/'artifacts'):raise ValueError('Fresh artifacts directory required')
    pins={f['destination']:f['sha256'] for f in json.loads((ROOT/'inventory/singularity-v7.json').read_text())['files']}
    output.mkdir(parents=True);stage=output/'stage';stage.mkdir();files=[];evidence=[]
    def add(path,dest,origin):
        digest=sha(path)
        if dest in pins and digest!=pins[dest]:raise ValueError('Accepted identity mismatch: '+dest)
        target=stage/dest;target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(path,target)
        files.append(dict(path=dest,entry=dest,sha256=digest,kind='copy',ownership='mod-authored'))
        evidence.append(dict(path=dest,sha256=digest,source=str(path),provenance=origin))
    neutral=source/'assets/processed/ui_remaster/atlas/ui_atlas_neutral.png'
    if sha(neutral)!='3EF1782696B4A5E1E9D93F409C5B0F6FF014B78CC42FAB06EC29110661981818':raise ValueError('Reviewed detailed icons changed')
    for name in ['neutral','localized_en','localized_pl']:
        add(source/('assets/processed/ui_remaster/atlas/ui_atlas_'+name+'.png'),'Binaries/vr-assets/ui/remaster/ui_atlas_'+name+'.png','MIT authored illustrations; side-m02-detailed-weapon-icons-20261024.md')
    add(source/'assets/processed/hud/diegetic/diegetic_hud_atlas.png','Binaries/vr-assets/hud/diegetic/diegetic_hud_atlas.png','MIT authored digital HUD artwork')
    add(source/'assets/processed/ui/runtime/vr_prompt_atlas.bmp','Binaries/vr-assets/vr_prompt_atlas.bmp','Kenney Input Prompts CC0-1.0; assets/LICENSES.md')
    add(source/'assets/LICENSES.md','Binaries/vr-assets/licenses/UI-ASSET-NOTICES.md','Original asset provenance notices; excludes reference-only assets')
    poses=ROOT.parent/'SingularityVR-knife-shading-thumb/build/knife-shading-thumb/retuned-candidate/vr-assets'
    for relative in ['knife/knife_hand.svng','hud/katorga/glove-animation/hand_thumb.svnt']:
        add(poses/relative,'Binaries/vr-assets/'+relative,'Authored numeric grip/thumb pose; fit_hand_grip.py and retune_knife_grip.py')
    if accepted_root is not None:
        # Explicit allowlist, not recursive copying of a development installation.
        names=['atlas/atlas.json','atlas/katorga-hud-atlas.png','ammo/ammo-digits.png']+['ammo/plate-'+n+'.png' for n in ['centurion','rifle','shotgun','sniper']]+['plates-atlas/katorga-plates-atlas.png','plates-atlas/plates.json']
        for name in names:add(accepted_root/('Binaries/vr-assets/hud/katorga/'+name),'Binaries/vr-assets/hud/katorga/'+name,'User-supplied legacy HUD art; redistribution provenance review pending')
        for name in ['polish_text.jsonl','polish_voice.jsonl','polish_story_voice.jsonl']:
            dest='Binaries/vr-assets/localization/pl/'+name
            add(accepted_root/dest,dest,'Accepted localization metadata; translation/media provenance review pending; no WAV files copied')
    module=dict(id='authored-ui-assets',labels={'en':'VR interface artwork','pl':'Grafika interfejsu VR'},version='v7-20261026',state='preview',required=True,dependencies=['vr-core'],downloadBytes=0,downloadUrl=None,archiveSha256='0'*64,expandedBytes=sum(p.stat().st_size for p in stage.rglob('*') if p.is_file()),rightsVerified=False,rightsEvidence='',files=files,reason='Exact UI resources staged; legacy artwork provenance and full runtime closure remain release gates')
    (output/'module.json').write_text(json.dumps(module,indent=2)+'\n')
    (output/'evidence.json').write_text(json.dumps(dict(files=evidence,steamWrites=False,gameLaunched=False),indent=2)+'\n')
    print('PASS exact authored UI staging:',len(files),'files; NOT public redistribution approval')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--source',type=Path,required=True);p.add_argument('--output',type=Path,required=True);p.add_argument('--accepted-root',type=Path);prepare(**vars(p.parse_args()))
