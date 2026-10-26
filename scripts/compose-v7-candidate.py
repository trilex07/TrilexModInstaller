"""Compose private reviewed modules without manufacturing rights or a playable release.

Input paths and outputs are workspace artifacts. Does not pack, sign, publish or
touch an installed game. Missing distribution identities remain explicit gates.
"""
import argparse,copy,json
from runtime_resources import require_closure
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
EXCLUDED={
 'Binaries/Singularity.exe':'Owned executable: verify only, never distribute',
 'RvGame/CookedPC/RvGame.xxx':'Owned core: verify/read only, never replace',
 **{'Binaries/vr-assets/hud/katorga/glove-animation/'+x:'Inactive PolyOne reference cache: accepted native hand route replaces it' for x in ['glove_left.svrg','glove_right.svrg','glove.svrt']}}

def compose(inputs,output):
    output=output.resolve()
    if output.exists() or not output.is_relative_to(ROOT/'artifacts'):raise ValueError('Fresh private artifacts output required')
    modules=[];seen=set();paths={}
    for item in json.loads(inputs.read_text()):
        module=copy.deepcopy(json.loads((ROOT/item['module']).read_text()))
        if module['id']=='runtime-code':module.update(id='vr-core',required=True)
        if module['id']=='native-hand-assets':module['required']=True
        if module['id'] in seen:raise ValueError('Duplicate module identity')
        seen.add(module['id'])
        for f in module['files']:
            key=f['path'].lower()
            if f['kind']!='ini' and key in paths:raise ValueError('Duplicate payload owner '+key)
            paths[key]=module['id']
        module['state']='unavailable'
        module['reason']='Private prepared candidate; distribution approval and immutable published package required'
        modules.append(module)
    for m in modules:
        if any(d not in seen for d in m['dependencies']):raise ValueError('Missing dependency '+m['id'])
    def visit(name,stack):
        if name in stack:raise ValueError('Dependency cycle')
        for d in next(m for m in modules if m['id']==name)['dependencies']:visit(d,stack+[name])
    for name in seen:visit(name,[])
    # Language selector must resolve a real manifest, not a nonexistent alias.
    settings=next(m for m in modules if m['id']=='portable-settings')
    text=copy.deepcopy(settings);text.update(id='polish-text',required=False,files=[dict(path='Binaries/SingularityVR.ini',entry='',sha256='',kind='ini',ownership='mod-authored',iniValues={'Localization/PolishLanguage':'1'})])
    modules.append(text)
    inventory=json.loads((ROOT/'inventory/singularity-v7.json').read_text())
    missing=[f['destination'] for f in inventory['files'] if f['destination'].lower() not in paths and f['destination'] not in EXCLUDED]
    if missing:raise ValueError('Unexplained accepted inventory omissions: '+str(missing))
    resource_count=require_closure(modules)
    catalog=json.loads((ROOT/'catalog/catalog.json').read_text())
    game=next(g for g in catalog['games'] if g['id']=='singularity');game['modules']=modules
    output.mkdir(parents=True)
    (output/'catalog.prepared.json').write_text(json.dumps(catalog,indent=2)+'\n')
    report=dict(status='PREPARED_NOT_RELEASED',runtimeSource=inventory['runtimeSource'],moduleCount=len(modules),targets=len(paths),inventoryPins=inventory['acceptedPins'],runtimeResources=resource_count,intentionalExclusions=EXCLUDED,unexplainedOmissions=missing,rightsReview=[m['id'] for m in modules if not m['rightsVerified']],productionPromotion=False,steamAccess='NONE')
    (output/'closure.json').write_text(json.dumps(report,indent=2)+'\n')
    print('PASS manifest closure and dependencies; NOT a published playable catalog')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--inputs',required=True,type=Path);p.add_argument('--output',required=True,type=Path);compose(**vars(p.parse_args()))
