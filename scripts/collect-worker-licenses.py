"""Keep full license trees for the frozen worker and Python runtime."""
import importlib.metadata as md,shutil,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parent.parent
out=ROOT/'.build/asset-worker/licenses';out.mkdir(parents=True,exist_ok=True)
for name in ['numpy','Pillow','PyInstaller','pyinstaller-hooks-contrib','charset-normalizer']:
    try:dist=md.distribution(name)
    except md.PackageNotFoundError:
        if name=='charset-normalizer':continue
        raise
    copied=0
    for f in dist.files or []:
        if any(x.lower() in ('licenses','license','copying','license.txt','copying.txt') for x in f.parts) and dist.locate_file(f).is_file():
            target=out/name/f;target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(dist.locate_file(f),target);copied+=1
    if not copied:raise ValueError('Missing full license for '+name)
license=Path(sys.base_prefix)/'LICENSE.txt'
if not license.is_file():raise ValueError('Full Python license unavailable')
shutil.copyfile(license,out/'Python-LICENSE.txt')
shutil.copyfile(ROOT/'LICENSE',out/'Trilex-MIT-LICENSE.txt')
shutil.copyfile(ROOT/'third_party/SingularityVR-MIT.txt',out/'SingularityVR-MIT-LICENSE.txt')
print('PASS frozen image-worker license notices collected')
