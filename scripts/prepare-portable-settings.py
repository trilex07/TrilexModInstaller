"""Prepare a portable V7 preset; retain functional Diagnostic keys.

Language choices are applied by compiled SingularityV7Settings, not package code.
"""
import argparse,json,configparser,hashlib
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def prepare(output,texture_ini):
    output=output.resolve()
    if output.exists() or not output.is_relative_to(ROOT/'artifacts'):raise ValueError('Fresh artifacts directory required')
    inventory=json.loads((ROOT/'inventory/singularity-v7.json').read_text());values=inventory['iniValues'].copy()
    disabled=['Diagnostic/DualWeaponTmdProbe','Diagnostic/MenuAssetWitness','Diagnostic/ShoulderPickupProbe']
    for key in disabled:
        if key not in values:raise ValueError('Reviewed diagnostic absent: '+key)
        values[key]='0'
    output.mkdir(parents=True);(output/'stage').mkdir()
    pin=next(f['sha256'] for f in inventory['files'] if f['destination']=='Binaries/texture_override.ini')
    if hashlib.sha256(texture_ini.read_bytes()).hexdigest().upper()!=pin:raise ValueError('Accepted texture settings identity changed')
    parser=configparser.ConfigParser(interpolation=None);parser.optionxform=str;parser.read(texture_ini,encoding='utf-8-sig')
    texture_values={section+'/'+key:value for section in parser.sections() for key,value in parser[section].items()}
    module=dict(id='portable-settings',labels={'en':'Portable accepted VR preset','pl':'Przenosny zaakceptowany profil VR'},version='v7-20261026',state='preview',required=True,dependencies=['vr-core'],downloadBytes=0,downloadUrl=None,archiveSha256='0'*64,expandedBytes=0,rightsVerified=True,rightsEvidence='Authored INI settings; accepted functional parameters retained; bounded debug probes disabled',files=[dict(path=inventory['iniDestination'],entry='',sha256='',kind='ini',ownership='mod-authored',iniValues=values),dict(path='Binaries/texture_override.ini',entry='',sha256='',kind='ini',ownership='mod-authored',iniValues=texture_values)],reason='Runtime asset closure and final catalog pending')
    (output/'module.json').write_text(json.dumps(module,indent=2)+'\n')
    (output/'review.json').write_text(json.dumps(dict(settings=len(values),diagnosticsDisabled=disabled,functionalDiagnosticKeysPreserved=True,languageOverrides='SingularityV7Settings',englishTextPolishVoice='BLOCKED: accepted V7 Bink gate couples to Polish text',source=inventory['runtimeSource']),indent=2)+'\n')
    print('PASS portable settings:',len(values),'keys; language runtime limitation explicit')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--output',type=Path,required=True);p.add_argument('--texture-ini',type=Path,required=True);prepare(**vars(p.parse_args()))
