"""Stage 1.0.8 from reviewed packages, retaining accepted Singularity content."""
import argparse,base64,copy,hashlib,json,shutil
from pathlib import Path

def read(path):
    data=json.loads(path.read_text(encoding='utf-8-sig'))
    return json.loads(base64.b64decode(data['payload'])) if 'payload' in data else data

def sha(path):
    with path.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest().upper()

def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--base-catalog',type=Path,required=True)
    p.add_argument('--darkness-candidate',type=Path,required=True)
    p.add_argument('--output',type=Path,required=True)
    a=p.parse_args()
    root=Path(__file__).resolve().parents[1]
    out=a.output.resolve()
    if not out.is_relative_to(root/'artifacts'):raise ValueError('Fresh artifacts output required')
    out.mkdir(parents=True)
    catalog=read(a.base_catalog)
    darkness=copy.deepcopy(next(g for g in read(a.darkness_candidate/'catalog.unsigned.json')['games'] if g['id']=='darkness2'))
    assert darkness['state']=='preview' and len(darkness['modules'])==4
    darkness['state']='stable'
    hashes={}
    for module in darkness['modules']:
        module['version']='1.0.8'
        source=a.darkness_candidate/'packages/darkness2'/(module['id']+'.zip')
        assert sha(source)==module['archiveSha256'].upper() and source.stat().st_size==module['downloadBytes']
        destination=out/'packages/darkness2'/source.name
        destination.parent.mkdir(parents=True,exist_ok=True)
        shutil.copyfile(source,destination)
        name='darkness2-'+source.name
        module['downloadUrl']='https://github.com/trilex07/TrilexModInstaller/releases/download/installer-1.0.8/'+name
        hashes[name]=sha(destination)
    catalog['version']='1.0.8'
    catalog['games']=[g for g in catalog['games'] if g['id']!='darkness2']+[darkness]
    (out/'catalog.unsigned.json').write_text(json.dumps(catalog,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
    (out/'darkness-package-sha256.json').write_text(json.dumps(hashes,indent=2)+'\n')
    print('Staged reviewed Darkness packages; Singularity modules unchanged. Sign before publishing.')

if __name__=='__main__':main()
