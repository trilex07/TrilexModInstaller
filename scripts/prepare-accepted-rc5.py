"""Freeze the accepted October 30 runtime into existing installer modules.

Private files stay in artifacts. Originals are hash-matched, never modified.
All changed game-derived resources retain local-original reconstruction.
"""
import configparser
import hashlib
import importlib.util
import json
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT.parent / 'SingularityVR-centurion-runtime'
GAME = Path('C:/Program Files (x86)/Steam/steamapps/common/Singularity')
OUT = ROOT / 'artifacts/accepted-rc5'
ORIGINALS = Path('C:/Users/Trilex/AppData/Local/Trilex/ModInstaller/installations/BAC1ED60D201BC08DFCB1ABACEB9778AECAE1F5FC14CBF3605DAD652106DCCA1/originals')

def sha(p):
    with p.open('rb') as f: return hashlib.file_digest(f, 'sha256').hexdigest().upper()

def read(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def write(p, v): p.write_text(json.dumps(v, indent=2)+'\n', encoding='utf-8')

def main():
    if OUT.exists(): raise ValueError('Fresh output required')
    assert sha(GAME/'Binaries/d3d9.dll') == '147C3297211CFA6CA6B964E29FBBD79538B7EE69D7BC155861B66F67B24BDC96'
    assert sha(GAME/'Binaries/SingularityVR.ini') == '81E9F33BE4D6A5A968DE0D96A45C59A262F2FDD3DB8ECF7DED89D40CF12A87E2'
    spec=importlib.util.spec_from_file_location('delta', ROOT/'scripts/prepare-v7-deltas.py')
    delta=importlib.util.module_from_spec(spec); spec.loader.exec_module(delta)
    OUT.mkdir(); frozen=OUT/'originals'; frozen.mkdir()
    def original(relative, digest):
        if relative.endswith('Textures_P0.tfc'): relative=relative.replace('Textures_P0.tfc','coalesced.tfc')
        target=frozen/relative
        if target.exists(): assert sha(target)==digest; return target
        candidates=[GAME/relative, ROOT.parent/'dependencies/Singularity'/relative]
        candidates += [p/relative for p in ORIGINALS.iterdir()]
        found=next((p for p in candidates if p.exists() and sha(p)==digest), None)
        if found is None: raise ValueError('Missing exact owned original: '+relative)
        target.parent.mkdir(parents=True,exist_ok=True); shutil.copyfile(found,target)
        return target
    graph=read(ROOT/'artifacts/runtime-resources-rc4b/inputs.json'); modules={}; report=[]
    for row in graph:
        m=read(ROOT/row['module']); mid='vr-core' if m['id']=='runtime-code' else m['id']
        stage=OUT/mid/'stage'; shutil.copytree(ROOT/row['stage'],stage)
        modules[mid]=(m,stage)
        m['version']='v7-20261030-rc5'
        for f in m['files']:
            if f['kind']=='ini': continue
            path=GAME/f['path']
            if not path.exists(): raise ValueError('Missing accepted resource '+f['path'])
            digest=sha(path)
            if digest==f['sha256']: continue
            if f['kind']=='copy': shutil.copyfile(path,stage/f['entry']); f['sha256']=digest
            else:
                relative=f.get('sourcePath', f['path']); before=original(relative,f.get('sourceSha256',f.get('beforeSha256')))
                recipe=delta.delta(before,path,stage,'rc5/'+hashlib.sha256(f['path'].encode()).hexdigest()[:20])
                replacement=dict(path=f['path'],entry='',sha256=digest,kind='recipe',ownership='local-transform',recipe=recipe)
                if 'sourcePath' in f: replacement.update(sourcePath=relative.replace('Textures_P0.tfc','coalesced.tfc'),sourceSha256=sha(before))
                else: replacement['beforeSha256']=sha(before)
                f.clear(); f.update(replacement)
                m['rightsVerified']=False
            report.append(dict(module=mid,path=f['path'],sha256=digest))
    # Exact runtime-format geometry/animation outputs, not GLBs or Blender files.
    hd=read(SOURCE/'docs/handoffs/kasimov-centurion-shared-runtime-candidate-20261029.json')
    names={x['path'] for x in hd['assets'] if '/reload/' in x['path']}
    names.update('Binaries/vr-assets/reload/volk/'+x for x in ['volk_clip.svrk','volk_clip.svrt','volk_hd.svvh','volk_reload.svwa'])
    m,stage=modules['centurion-assets']; known={f['path'] for m,_ in modules.values() for f in m['files']}
    stock=read(ROOT/'artifacts/local-audio-essentials-v1/module.json')['files'][0]
    before=original(stock['path'],stock['beforeSha256'])
    for name in sorted(names-known):
        path=GAME/name
        recipe=delta.delta(before,path,stage,'rc5/'+hashlib.sha256(name.encode()).hexdigest()[:20])
        m['files'].append(dict(path=name,entry='',sha256=sha(path),kind='recipe',ownership='local-transform',sourcePath=stock['path'],sourceSha256=sha(before),recipe=recipe))
        report.append(dict(module='centurion-assets',path=name,sha256=sha(path)))
    m['rightsVerified']=False
    for mid in ['vr-core','hd-textures']:
        modules[mid][0]['dependencies']=sorted(set(modules[mid][0]['dependencies']+['centurion-assets'])) if mid!='vr-core' else modules[mid][0]['dependencies']
    m,stage=modules['portable-settings']
    for f in m['files']:
        p=configparser.ConfigParser(interpolation=None,strict=False);p.optionxform=str;p.read(GAME/f['path'],encoding='utf-8-sig')
        f['iniValues']={s+'/'+k:v for s in p.sections() for k,v in p[s].items()}
    graph=[]
    for mid,(m,stage) in modules.items():
        # Remove obsolete recipe payloads from copied stages: extractor requires exact allowlists.
        used={f['entry'] for f in m['files'] if f.get('entry')}
        used.update(p['entry'] for f in m['files'] for p in f.get('recipe',[]) if p.get('entry'))
        # Ancillary worker inputs are referenced from JSON and retained; packer selects entries.
        write(OUT/mid/'module.json',m)
        graph.append(dict(module=str((OUT/mid/'module.json').relative_to(ROOT)),stage=str(stage.relative_to(ROOT))))
    write(OUT/'inputs.json',graph)
    resources=read(ROOT/'inventory/v7-runtime-resources.json')
    resources['runtimeSource']='e814f137675b8ab075c4adb10c1fc9fb1eca5f4a'
    for row in resources['files']:
        p=GAME/row['path'];row.update(sha256=sha(p),bytes=p.stat().st_size)
    write(ROOT/'inventory/v7-runtime-resources.json',resources)
    write(OUT/'changes.json',report)
    print('Staged',len(report),'changed/new resources; no Steam writes')

if __name__=='__main__': main()
