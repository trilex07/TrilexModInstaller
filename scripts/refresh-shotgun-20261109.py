"""Scoped signed-catalog input and offline bundle refresh; never touches games."""
import argparse,base64,copy,hashlib,io,json,shutil,zipfile
from pathlib import Path

def sha(b):return hashlib.sha256(b).hexdigest().upper()
def main():
    p=argparse.ArgumentParser()
    for name in ('base','repair','out'):p.add_argument('--'+name,type=Path,required=True)
    a=p.parse_args();assert not a.out.exists(),'Fresh output required'
    raw=(a.base/'catalog.signed.json').read_bytes()
    assert sha(raw)=='E5E855405104BF9C819137DDEB154593451168BCF5E60E085661D6D40CD05F17'
    old=json.loads(base64.b64decode(json.loads(raw)['payload']));cat=copy.deepcopy(old)
    assert old['version']=='1.1.5'
    game=next(g for g in cat['games'] if g['id']=='singularity')
    mod=next(m for m in game['modules'] if m['id']=='audio-remaster')
    f=next(f for f in mod['files'] if f['path']=='RvGame/CookedPC/wp_SP_ARShotgun_SF.xxx')
    assert f['sha256']=='57E8E5A37B667A4CB0A4EC4C2095DDDA3782063E92A7AA559CF314D4B84F33A4'
    target=sha((a.repair/'wp_SP_ARShotgun_SF.xxx').read_bytes())
    assert target=='88548AA9F8F20044418B9CE121FDF3C74923A6BBC0DB6D39A2F2AEE340AE383F'
    data=(a.base/'packages/audio-remaster.zip').read_bytes();assert sha(data)==mod['archiveSha256']
    entry=f['recipe'][0]['entry'];result=io.BytesIO();delta=0
    with zipfile.ZipFile(io.BytesIO(data)) as src,zipfile.ZipFile(result,'w',zipfile.ZIP_DEFLATED) as dst:
        for item in src.infolist():
            value=src.read(item)
            if item.filename==entry:
                before=json.loads(value);op=dict(before,encodedSha256=sha((a.repair/'fixed-shotgunFire35.ogg').read_bytes()),
                    logicalSha256=sha((a.repair/'fixed-logical.u').read_bytes()),targetSha256=target)
                revised=(json.dumps(op,indent=2)+'\n').encode();delta=len(revised)-len(value);value=revised
            dst.writestr(item,value)
    new=result.getvalue();f['sha256']=target
    name='singularity-audio-remaster-20261109-shotgun.zip'
    mod.update(version='v7-20261109-shotgun-header',archiveSha256=sha(new),downloadBytes=len(new),
        expandedBytes=mod['expandedBytes']+delta,
        downloadUrl='https://github.com/trilex07/TrilexModInstaller/releases/download/installer-1.1.0/'+name)
    restored=copy.deepcopy(cat);restored['games'][cat['games'].index(game)]['modules'][game['modules'].index(mod)]=copy.deepcopy(next(m for g in old['games'] if g['id']=='singularity' for m in g['modules'] if m['id']=='audio-remaster'))
    assert restored==old
    with zipfile.ZipFile(io.BytesIO(data)) as src,zipfile.ZipFile(io.BytesIO(new)) as dst:
        assert src.namelist()==dst.namelist()
        assert [n for n in src.namelist() if src.read(n)!=dst.read(n)]==[entry]
    cat['version']='1.1.6';a.out.mkdir(parents=True)
    shutil.copytree(a.base,a.out/'bundle')
    (a.out/name).write_bytes(new);(a.out/'bundle/packages/audio-remaster.zip').write_bytes(new)
    (a.out/'catalog.unsigned.json').write_text(json.dumps(cat,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    (a.out/'shotgun-recipe.json').write_text(json.dumps(op,indent=2)+'\n',encoding='utf-8')
    print('PASS catalog1.1.6: only shotgun output hash, audio module metadata and one recipe changed')
if __name__=='__main__':main()
