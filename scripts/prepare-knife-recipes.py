"""Knife geometry recipe stores only source references, not original mesh bytes."""
import argparse,hashlib,json,struct,sys,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
def prepare(source,mesh,output):
 output=output.resolve();source=source.resolve()
 if output.exists() or not output.is_relative_to(ROOT/'artifacts'):raise ValueError('Fresh private output required')
 sys.path.insert(0,str(ROOT.parent/'SingularityVR-left-hip-knife/tools/knife'))
 from prepare_knife import parse
 core=ROOT/'artifacts/decoded-original-RvGame.u';data=core.read_bytes()
 if sha(core)!='09DE450D9F620952C3701D874BEB90A8BD9235263975157A641B9E3441336C92':raise ValueError('Original core mismatch')
 chunks=parse(mesh.read_bytes());points=[struct.unpack_from('<3f',chunks['PNTS0000'][2],i*12) for i in range(chunks['PNTS0000'][1])];wedges=[struct.unpack_from('<IffBBH',chunks['VTXW0000'][2],i*16) for i in range(chunks['VTXW0000'][1])]
 positions=[];uvs=[]
 for point,u,v,*_ in wedges:
  x,y,z=points[point];positions.append(data.find(struct.pack('<3f',x,-y,z)));uvs.append(data.find(struct.pack('<2e',u,v)))
 faces=[struct.unpack_from('<3HBBI',chunks['FACE0000'][2],i*12)[:3] for i in range(chunks['FACE0000'][1])]
 indices=data.find(b''.join(struct.pack('<3H',b,a,c) for a,b,c in faces))
 if min(positions+uvs+[indices])<0:raise ValueError('Original source reference absent')
 pins={Path(f['destination']).name:f for f in json.loads((ROOT/'inventory/singularity-v7.json').read_text())['files']}
 output.mkdir(parents=True);stage=output/'stage';stage.mkdir();files=[]
 for kind in ['svrk','svnl']:
  name='knife.'+kind;entry=name+'.json';(stage/entry).write_text(json.dumps(dict(kind=kind,positions=positions,uvs=uvs,indices=indices),indent=2)+'\n')
  generated=output/name
  subprocess.run([sys.executable,str(ROOT/'tools/asset-worker/main.py'),'--knife',str(core),str(generated),str(stage/entry)],check=True)
  if sha(generated)!=pins[name]['sha256']:raise ValueError('Accepted knife mismatch '+name+' '+sha(generated))
  files.append(dict(path=pins[name]['destination'],entry='',sha256=sha(generated),kind='recipe',ownership='local-transform',sourcePath='RvGame/CookedPC/RvGame.xxx',sourceSha256='4153C4889C90635069ED226D918CDB76BE618F46B1BAEA152AC10CFFDF59E9EF',sourceCodec='ue3-whole-lzo',decodedSha256=sha(core),decodedBytes=len(data),recipe=[dict(source='knife',offset=0,length=generated.stat().st_size,entry=entry)]))
 sys.path.insert(0,str(source/'tools/textures'))
 from audit_cooked_texture import inspect,properties,mips
 pkg=inspect(data);tfc=ROOT.parent/'SingularityVR-assets-local/cooked/Textures_P0.tfc'
 with tfc.open('rb') as stream:cache_sha=hashlib.file_digest(stream,'sha256').hexdigest().upper()
 if cache_sha!='6BE9134DF84D23B49089DA39B05594EE813B350949318A54684273104D865EE0':raise ValueError('Original texture cache mismatch')
 for original,name in [('knife_d','knife.svrt'),('knife_n','knife_normal.svrt'),('knife_s','knife_specular.svrt')]:
  exports=[e for e in pkg['exports'] if e['class_name']=='Texture2D' and e['name']==original]
  if len(exports)!=1:raise ValueError('Ambiguous knife material')
  e=exports[0];props,q=properties(data,pkg['names'],e['offset']+4,e['offset']+e['size']);mip=next(m for m in mips(data,q,e['offset']+e['size'],props['Format']) if m['flags']!=0x21)
  if mip['flags']!=0x11:raise ValueError('Unsupported knife bulk')
  raw=output/(original+'.raw');subprocess.run([str(ROOT/'.build/ue3decode/Trilex.Ue3Decode.exe'),'ue3-bulk-lzo',str(tfc),str(raw),str(mip['bulk_offset']),str(mip['stored_size'])],check=True,capture_output=True)
  entry=name+'.json';(stage/entry).write_text(json.dumps(dict(width=mip['width'],height=mip['height'],role='svrt',factor=1,fourcc=props['Format'][3:]),indent=2)+'\n')
  files.append(dict(path=pins[name]['destination'],entry='',sha256=pins[name]['sha256'],kind='recipe',ownership='local-transform',sourcePath='RvGame/CookedPC/coalesced.tfc',sourceSha256=cache_sha,sourceCodec='ue3-bulk-lzo',sourceOffset=mip['bulk_offset'],sourceBytes=mip['stored_size'],decodedSha256=sha(raw),decodedBytes=mip['raw_size'],recipe=[dict(source='image',offset=0,length=16+mip['width']*mip['height']*4,entry=entry)]))
 module=dict(id='knife-assets',labels={'en':'Native knife','pl':'Natywny noz'},version='v7-20261026',state='preview',required=True,dependencies=['vr-core'],downloadBytes=0,downloadUrl=None,archiveSha256='0'*64,expandedBytes=sum(p.stat().st_size for p in stage.iterdir())+sum(f['recipe'][0]['length'] for f in files),rightsVerified=False,rightsEvidence='',files=files,reason='Original-derived geometry generated locally; remaining material closure pending')
 (output/'module.json').write_text(json.dumps(module,indent=2)+'\n');print('PASS accepted knife geometry and lighting exact hashes; C# verification pending')
if __name__=='__main__':
 p=argparse.ArgumentParser()
 for key in ['source','mesh','output']:p.add_argument('--'+key,required=True,type=Path)
 prepare(**vars(p.parse_args()))
