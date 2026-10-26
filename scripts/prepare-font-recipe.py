"""Read exact original and accepted font packages; write only private artifacts.

recipe-maker reads argv[1] and argv[2] with rb; only argv[3] is opened wbx
(tools/recipe-maker/main.cpp:11-17). The accepted package is never an output.
"""
import argparse,hashlib,json,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
def prepare(original,accepted_input,output):
    output=output.resolve();original=original.resolve();accepted_input=accepted_input.resolve()
    if output.exists() or not output.is_relative_to(ROOT/'artifacts'):raise ValueError('Fresh artifacts output required')
    if original.is_relative_to(output) or accepted_input.is_relative_to(output):raise ValueError('Inputs must be outside output')
    if sha(original)!='3F7BF0BEF08D73AED3B1BA249730B4BBB16EC427DD49992A6F4C4A7CE6C8B51B' or sha(accepted_input)!='60E7287185300CAA2A8E64FBE14B741475D4D959944B6B0E51F84547841E8DB8':raise ValueError('Font source identities changed')
    before=(sha(original),sha(accepted_input));output.mkdir(parents=True);stage=output/'stage';stage.mkdir()
    recipe=stage/'font.svrp'
    # This exact source is already a logical UE3 package, not whole-file LZO.
    # Copy/literal recipes can safely reference its verified bytes directly.
    subprocess.run([str(ROOT/'.build/recipe-maker/recipe-maker.exe'),str(original),str(accepted_input),str(recipe)],check=True,capture_output=True)
    if before!=(sha(original),sha(accepted_input)):raise ValueError('Read-only input changed')
    file=dict(path='RvGame/CookedPC/ui_GFx_fonts.xxx',entry='',sha256=before[1],kind='recipe',ownership='local-transform',beforeSha256=before[0],recipe=[dict(source='instructions',offset=0,length=accepted_input.stat().st_size,entry='font.svrp')])
    module=dict(id='native-font-assets',labels={'en':'Native Polish font compatibility','pl':'Natywne polskie znaki'},version='v7-20261026',state='preview',required=True,dependencies=['vr-core'],downloadBytes=0,downloadUrl=None,archiveSha256='0'*64,expandedBytes=recipe.stat().st_size+accepted_input.stat().st_size,rightsVerified=False,rightsEvidence='',files=[file],reason='Exact local font reconstruction; inserted glyph provenance review pending')
    (output/'module.json').write_text(json.dumps(module,indent=2)+'\n');print('PASS font recipe staged; both input hashes unchanged; no game writes')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--original',type=Path,required=True);p.add_argument('--accepted-input',type=Path,required=True);p.add_argument('--output',type=Path,required=True);prepare(**vars(p.parse_args()))
