"""Centurion recipes contain only fixed operation names and owned texture references."""
import argparse,hashlib,json,subprocess,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
def prepare(source,decoded,tfc,output):
    output=output.resolve()
    if output.exists() or not output.is_relative_to(ROOT/'artifacts'):raise ValueError('Fresh private output required')
    if sha(decoded)!='0534C5C9A5878D280294168BD12711B99CB3948503D4ED53608A0316479D1B18' or sha(tfc)!='6BE9134DF84D23B49089DA39B05594EE813B350949318A54684273104D865EE0':raise ValueError('Owned source changed')
    output.mkdir(parents=True);stage=output/'stage';stage.mkdir();files=[]
    for name,mode,size,digest in [('centurion_clip.svrk','clip',33136,'F21961145D6645EE93A8A2C73AE89380535D918352BABB1FD4F811C67422D89B'),('centurion_shell.svrk','shell',3880,'AB61034E9E35D2C5BEAC937E3E3BB90D15272828FF2BBBE2CD26ED443E5E5CAC'),('centurion_reload.svca','animation',3208,'85ABCDDE7C9F1971C7B3BD5457672D69987FF332F8EC8C52953604505A945EA6')]:
        files.append(dict(path='Binaries/vr-assets/reload/centurion/'+name,entry='',sha256=digest,kind='recipe',ownership='local-transform',sourcePath='RvGame/CookedPC/wp_SP_ARPistol_SF.xxx',sourceSha256='B06D1516E5F4AE15BD9225A38E52BA706EC32D9C6D0E060320149A2F38DA44AF',sourceCodec='ue3-logical-lzo',decodedSha256=sha(decoded),decodedBytes=decoded.stat().st_size,recipe=[dict(source='centurion-'+mode,offset=0,length=size)]))
    sys.path.insert(0,str(source/'tools/textures'));from audit_cooked_texture import inspect,properties,mips
    data=decoded.read_bytes();package=inspect(data)
    for original,name in [('AR_Pistol_d','centurion_clip.svrt'),('shell_rifle_d','centurion_shell.svrt')]:
        e=next(e for e in package['exports'] if e['class_name']=='Texture2D' and e['name']==original)
        props,q=properties(data,package['names'],e['offset']+4,e['offset']+e['size']);mip=next(m for m in mips(data,q,e['offset']+e['size'],props['Format']) if m['flags']!=0x21)
        if mip['flags'] not in (0,0x11):raise ValueError('Unsupported original texture storage')
        raw=output/(name+'.raw')
        if mip['flags']==0:raw.write_bytes(data[mip['bulk_offset']:mip['bulk_offset']+mip['raw_size']])
        else:subprocess.run([str(ROOT/'.build/ue3decode/Trilex.Ue3Decode.exe'),'ue3-bulk-lzo',str(tfc),str(raw),str(mip['bulk_offset']),str(mip['stored_size'])],check=True,capture_output=True)
        entry=name+'.json';(stage/entry).write_text(json.dumps(dict(width=mip['width'],height=mip['height'],role='svrt',factor=1,fourcc=props['Format'][3:],rawOffset=mip['bulk_offset'] if mip['flags']==0 else None),indent=2)+'\n')
        pins={'centurion_clip.svrt':'17A3355AD6CC7E2F35A03883FDF4B314577FEE908773262C5BC199C006C6CD60','centurion_shell.svrt':'F804945B8160EC47C6C5B5C94844C9A8B8CCE43E28499799606051B5909FE8D0'}
        files.append(dict(path='Binaries/vr-assets/reload/centurion/'+name,entry='',sha256=pins[name],kind='recipe',ownership='local-transform',sourcePath='RvGame/CookedPC/coalesced.tfc',sourceSha256=sha(tfc),sourceCodec='ue3-bulk-lzo',sourceOffset=mip['bulk_offset'],sourceBytes=mip['stored_size'],decodedSha256=sha(raw),decodedBytes=mip['raw_size'],recipe=[dict(source='image',offset=0,length=16+mip['width']*mip['height']*4,entry=entry)]))
        if mip['flags']==0:
            files[-1].update(sourcePath='RvGame/CookedPC/wp_SP_ARPistol_SF.xxx',sourceSha256='B06D1516E5F4AE15BD9225A38E52BA706EC32D9C6D0E060320149A2F38DA44AF',sourceCodec='ue3-logical-lzo',sourceOffset=None,sourceBytes=None,decodedSha256=sha(decoded),decodedBytes=len(data))
    module=dict(id='centurion-assets',labels={'en':'Native manual revolver reload','pl':'Natywne reczne przeladowanie rewolweru'},version='v7-20261026',state='preview',required=True,dependencies=['vr-core'],downloadBytes=0,downloadUrl=None,archiveSha256='0'*64,expandedBytes=sum(f['recipe'][0]['length'] for f in files)+4096,rightsVerified=False,rightsEvidence='',files=files,reason='Local exact original reconstruction; extractor distribution review pending')
    (output/'module.json').write_text(json.dumps(module,indent=2)+'\n');print('PREPARED five Centurion recipes; C# consumer verification required')
if __name__=='__main__':
    p=argparse.ArgumentParser()
    for k in ['source','decoded','tfc','output']:p.add_argument('--'+k,type=Path,required=True)
    prepare(**vars(p.parse_args()))
