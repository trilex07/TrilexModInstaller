"""Assemble the explicitly requested complete V7 fan-mod candidate, without publishing.

Keeps unverified third-party rights FALSE. Publisher approval is a separate signed
statement, never a fabricated license. Does not read Steam or launch a game.
"""
import argparse
import importlib.util
import json
import re
import subprocess
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
TOOLS=ROOT/'src/Trilex.Installer.Tools/bin/Release/net10.0/Trilex.Installer.Tools.dll'

def bind_owned_texture_cache(module):
    # Private extraction workspaces renamed the stock cache. Recipients have
    # Steam's coalesced.tfc, with the same bytes, not the extractor's alias.
    for file in module['files']:
        if file.get('sourcePath') != 'RvGame/CookedPC/Textures_P0.tfc':
            continue
        if (file.get('kind') != 'recipe' or file.get('ownership') != 'local-transform'
                or file.get('sourceCodec') != 'ue3-bulk-lzo'
                or file.get('sourceSha256') != '6BE9134DF84D23B49089DA39B05594EE813B350949318A54684273104D865EE0'):
            raise ValueError('Unsupported private texture-cache alias: '+file['path'])
        file['sourcePath']='RvGame/CookedPC/coalesced.tfc'

def prepare(inputs,output,tag):
    output=output.resolve()
    if output.exists() or not output.is_relative_to(ROOT/'artifacts'):
        raise ValueError('Fresh artifacts output required')
    if not re.fullmatch(r'installer-[0-9]+\.[0-9]+\.[0-9]+-rc\.[0-9]+',tag):
        raise ValueError('Explicit immutable RC tag required')
    spec=importlib.util.spec_from_file_location('composer',ROOT/'scripts/compose-v7-candidate.py')
    composer=importlib.util.module_from_spec(spec);spec.loader.exec_module(composer)
    composer.compose(inputs,output/'closure')
    catalog=json.loads((output/'closure/catalog.prepared.json').read_text())
    catalog.update(version=tag.removeprefix('installer-'),expiresUtc='2027-04-26T00:00:00Z')
    game=next(g for g in catalog['games'] if g['id']=='singularity')
    game.update(state='stable')
    stages={}
    for row in json.loads(inputs.read_text()):
        manifest=json.loads((ROOT/row['module']).read_text(encoding='utf-8-sig'))
        stages['vr-core' if manifest['id']=='runtime-code' else manifest['id']]=ROOT/row['stage']
    stages['polish-text']=output/'empty-stage';stages['polish-text'].mkdir()
    packages=output/'packages';packages.mkdir()
    labels={
        'vr-core':('V7 VR runtime','Silnik VR V7'),
        'polish-dubbing':('Polish dubbing','Polski dubbing'),
        'audio-remaster':('Audio remaster','Remaster dźwięków'),
        'ui-hud':('Game menus and HUD','Menu gry i HUD'),
        'native-hand-assets':('Tracked hands and TMD','Śledzone dłonie i TMD'),
        'hd-textures':('HD textures and effects (2X/8K)','Tekstury HD i efekty (2X/8K)'),
        'knife-assets':('Native knife','Natywny nóż'),
        'authored-ui-assets':('VR interface artwork','Grafika interfejsu VR'),
        'native-font-assets':('Polish font glyphs','Polskie znaki czcionki'),
        'portable-settings':('Accepted V7 settings','Zaakceptowane ustawienia V7'),
        'centurion-assets':('Centurion manual reload','Ręczne przeładowanie Centuriona'),
        'polish-text':('Polish game text','Polskie teksty gry')}
    declarations=[]
    for module in game['modules']:
        bind_owned_texture_cache(module)
        en,pl=labels[module['id']]
        module.update(state='stable',reason='',localizedReasons=None,labels=dict(en=en,pl=pl))
        if module['id']=='vr-core':
            for file in module['files']:
                if file.get('systemSource')=='windows-directinput8-x86':
                    file['useCurrentSystemVersion']=True
        if not module['rightsVerified']:
            module['publisherReview']=dict(basis='fan-mod-publisher-approved',
                evidence='docs/distribution/FIRST-EDITION.md; accepted source '+composer.json.loads((ROOT/'inventory/singularity-v7.json').read_text())['runtimeSource'],
                notice='Unofficial fan-made/derived content. Third-party rights reserved. Permissions not independently verified; not covered by the installer MIT license.')
        module['downloadUrl']=f"https://github.com/trilex07/TrilexModInstaller/releases/download/{tag}/{module['id']}.zip"
        manifest=output/(module['id']+'.input.json')
        manifest.write_text(json.dumps(module,indent=2)+'\n')
        archive=packages/(module['id']+'.zip')
        subprocess.run(['dotnet',str(TOOLS),'pack',str(manifest),str(stages[module['id']]),str(archive)],check=True)
        packed=json.loads(Path(str(archive)+'.module.json').read_text())
        module.update(packed)
        declarations.append(dict(id=module['id'],rightsVerified=module['rightsVerified'],publisherReview=module.get('publisherReview'),archiveSha256=module['archiveSha256'],downloadBytes=module['downloadBytes']))
    (output/'catalog.json').write_text(json.dumps(catalog,indent=2)+'\n')
    (output/'package-review.json').write_text(json.dumps(dict(status='ASSEMBLED_NOT_PUBLISHED',tag=tag,modules=declarations,steamAccess='NONE',physicalAcceptance='PENDING',authenticode='NOT_SIGNED'),indent=2)+'\n')
    print('Complete V7 packages assembled; sign catalog against pinned key before consumer use. No public upload.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--inputs',type=Path,required=True);p.add_argument('--output',type=Path,required=True);p.add_argument('--tag',required=True);prepare(**vars(p.parse_args()))
