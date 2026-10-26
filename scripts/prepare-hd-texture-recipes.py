"""Prepare metadata-only HD recipes; original texture bytes remain on the owner's PC."""
import argparse,hashlib,json,struct,sys,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest().upper()
def prepare(source,assets,output):
 source=source.resolve();assets=assets.resolve();output=output.resolve()
 if output.exists() or not output.is_relative_to(ROOT/'artifacts'):raise ValueError('Fresh private artifacts required')
 sys.path.insert(0,str(source/'tools/textures'))
 from audit_cooked_texture import inspect,properties,mips
 tfc=assets/'cooked/Textures_P0.tfc';cache_sha=sha(tfc)
 if cache_sha!='6BE9134DF84D23B49089DA39B05594EE813B350949318A54684273104D865EE0':raise ValueError('Unsupported owned cache')
 packages={'RvGame':ROOT/'artifacts/decoded-original-RvGame.u'}
 for name in ['RvGameMPLoader_SF','wp_SP_ARPistol_SF']:packages[name]=assets/'material-remaster-hires/recovery-20261026'/(name+'.u')
 loaded={name:(p.read_bytes(),inspect(p.read_bytes())) for name,p in packages.items()}
 rows=[('AR_shotgun_d','RvGameMPLoader_SF','AR_shotgun_d','diffuse',2),('AR_shotgun_n','RvGameMPLoader_SF','AR_shotgun_n','normal',2),('inview_glove_d','RvGame','inview_glove_d','diffuse',2),('inview_glove_n','RvGame','inview_glove_n','normal',2),('AR_E99Rifle_alt_d','RvGame','AR_E99Rifle_alt_d','weapon-diffuse-v2',2),('AR_E99Rifle_n','RvGame','AR_E99Rifle_n','weapon-normal-v2',2),('handgun_d','wp_SP_ARPistol_SF','AR_Pistol_d','diffuse',2),('handgun_n','wp_SP_ARPistol_SF','AR_Pistol_n','normal',2),('ar_sniper_rifle_d','RvGameMPLoader_SF','ar_sniper_rifle_d','diffuse',4),('ar_sniper_rifle_n','RvGameMPLoader_SF','ar_sniper_rifle_n','weapon-normal-procedural-v2',2)]
 pins={Path(f['destination']).name:f for f in json.loads((ROOT/'inventory/singularity-v7.json').read_text())['files']}
 output.mkdir(parents=True);stage=output/'stage';stage.mkdir();rawdir=output/'private-proof';rawdir.mkdir();files=[];evidence=[];slices={}
 for target,package,name,role,factor in rows:
  data,pkg=loaded[package];exports=[e for e in pkg['exports'] if e['class_name']=='Texture2D' and e['name']==name]
  if len(exports)!=1:raise ValueError('Ambiguous texture '+name)
  e=exports[0];props,q=properties(data,pkg['names'],e['offset']+4,e['offset']+e['size']);fmt=props['Format'][3:]
  mip=next(m for m in mips(data,q,e['offset']+e['size'],props['Format']) if m['flags']!=0x21)
  if mip['flags']!=0x11:raise ValueError('Not external bulk')
  raw=rawdir/(name+'.raw');subprocess.run([str(ROOT/'.build/ue3decode/Trilex.Ue3Decode.exe'),'ue3-bulk-lzo',str(tfc),str(raw),str(mip['bulk_offset']),str(mip['stored_size'])],check=True,capture_output=True)
  original=assets/'textures/original'/package/'Texture2D'/(name+'.dds');dds=original.read_bytes()
  if dds[128:128+mip['raw_size']]!=raw.read_bytes():raise ValueError('Original mip mismatch '+name)
  w,h=struct.unpack_from('<II',dds,12)[::-1]
  if (w,h)!=(mip['width'],mip['height']):raise ValueError('Mip dimensions mismatch')
  slices[name]=dict(offset=mip['bulk_offset'],bytes=mip['stored_size'],sha256=sha(raw))
  operation=dict(width=w,height=h,role=role,factor=factor,fourcc=fmt)
  if role=='weapon-normal-v2':operation['diffuse']=slices['AR_E99Rifle_alt_d']
  entry=target+'.json';(stage/entry).write_text(json.dumps(operation,indent=2)+'\n')
  accepted=assets/'final-complete-weapon-hd-review-20261026'/(target+'.dds');pin=pins[target+'.dds']
  if sha(accepted)!=pin['sha256']:raise ValueError('Accepted output mismatch')
  files.append(dict(path=pin['destination'],entry='',sha256=pin['sha256'],kind='recipe',ownership='local-transform',sourcePath='RvGame/CookedPC/coalesced.tfc',sourceSha256=cache_sha,sourceCodec='ue3-bulk-lzo',sourceOffset=mip['bulk_offset'],sourceBytes=mip['stored_size'],decodedSha256=sha(raw),decodedBytes=mip['raw_size'],recipe=[dict(source='image',offset=0,length=accepted.stat().st_size,entry=entry)]))
  evidence.append(dict(texture=target,originalMip=mip['index'],originalRawSha256=sha(raw),targetSha256=sha(accepted),operation=operation))
 module=dict(id='hd-textures',labels={'en':'HD weapon textures','pl':'Tekstury HD broni'},version='v7-20261026',state='preview',required=False,dependencies=['vr-core'],downloadBytes=0,downloadUrl=None,archiveSha256='0'*64,expandedBytes=sum(p.stat().st_size for p in stage.iterdir())+sum(f['recipe'][0]['length'] for f in files),rightsVerified=False,rightsEvidence='',files=files,reason='Metadata-only local generation, catalog review pending')
 (output/'module.json').write_text(json.dumps(module,indent=2)+'\n');(output/'evidence.json').write_text(json.dumps(evidence,indent=2)+'\n')
 print(json.dumps(dict(textures=len(files),originalPixelsArchived=False,steamAccess='NONE')))
if __name__=='__main__':
 p=argparse.ArgumentParser()
 for key in ['source','assets','output']:p.add_argument('--'+key,required=True,type=Path)
 prepare(**vars(p.parse_args()))
