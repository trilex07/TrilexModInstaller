"""Check real packed RC5 runtime, settings and dependency identities."""
import hashlib
import json
from pathlib import Path
import zipfile

root=Path(__file__).resolve().parents[1]
bundle=root/'artifacts/Trilex-SingularityVR-1.0.0-rc.5'
catalog=json.loads((bundle/'catalog.json').read_text())
modules={m['id']:m for m in catalog['games'][0]['modules']}
files={f['path']:f for m in modules.values() for f in m['files']}
with zipfile.ZipFile(bundle/'packages/vr-core.zip') as z:
    dll=hashlib.sha256(z.read('Binaries/d3d9.dll')).hexdigest().upper()
assert dll=='147C3297211CFA6CA6B964E29FBBD79538B7EE69D7BC155861B66F67B24BDC96'
settings=next(f for f in modules['portable-settings']['files'] if f['path']=='Binaries/SingularityVR.ini')['iniValues']
for key,value in {'Render/EquipmentWristSuppressHealth':'1','Render/HealthPlateEnabled':'0','Render/WristHealth':'0','Render/EquipmentWristPreviewNoDepth':'0'}.items():
    assert settings[key]==value,(key,settings.get(key))
resources=json.loads((root/'inventory/v7-runtime-resources.json').read_text())
for row in resources['files']:
    assert files[row['path']]['sha256']==row['sha256'],row['path']
changes=json.loads((root/'artifacts/accepted-rc5/changes.json').read_text())
for row in changes:
    assert files[row['path']]['sha256']==row['sha256']
assert modules['centurion-assets']['required']
print('PASS packed runtime, accepted HUD settings,',len(resources['files']),'resource pins and',len(changes),'updated resources')

