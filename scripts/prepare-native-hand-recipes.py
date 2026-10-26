"""Build exact native index-cache recipes; original triangles are COPY operations only.

Private source PSK exports verify the original index stream. Neither PSK nor
original indices are included in the archive. Authored caps/pose metadata remain
separate literals, with explicit counts and target hashes.
"""
import argparse, hashlib, json, struct, sys
from pathlib import Path
ROOT=Path(__file__).resolve().parent.parent

def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
def prepare(source,decoded,original,psks,output):
    if output.exists() or not output.resolve().is_relative_to((ROOT/'artifacts').resolve()):raise ValueError('Fresh artifacts directory required')
    if sha(original)!='4153C4889C90635069ED226D918CDB76BE618F46B1BAEA152AC10CFFDF59E9EF' or sha(decoded)!='09DE450D9F620952C3701D874BEB90A8BD9235263975157A641B9E3441336C92':raise ValueError('Original core identity changed')
    sys.path.insert(0,str(source/'tools/gloves'));from prepare_gloves import read_psk
    core=decoded.read_bytes();maps={}
    for name in ['ch_inview_noTMD','ch_inview_tmd_mesh']:
        faces=read_psk(psks/(name+'.psk'))[-1]
        native=b''.join(struct.pack('<3H',f[1],f[0],f[2]) for f in faces)
        at=core.find(native)
        if at<0 or core.find(native,at+1)>=0:raise ValueError('Ambiguous original index stream')
        maps[name]={native[i:i+6]:at+i for i in range(0,len(native),6)}
    pins={Path(f['destination']).name:f for f in json.loads((ROOT/'inventory/singularity-v7.json').read_text())['files']}
    inputs={
        'native_hands.svni':ROOT.parent/'SingularityVR-glove-animation/build/gloves/native-ue3/native_hands.svni',
        'native_cuffs.svnc':ROOT.parent/'SingularityVR-native-glove-cuffs/build/gloves/cuff-repair/native_cuffs.svnc',
        'tmd_short_gauntlet.svtg':ROOT.parent/'research/tmd-short-gauntlet-private/tmd_short_gauntlet.svtg'}
    output.mkdir(parents=True);stage=output/'stage';stage.mkdir();files=[];receipts=[]
    for name,path in inputs.items():
        if sha(path)!=pins[name]['sha256']:raise ValueError('Accepted cache changed: '+name)
        data=path.read_bytes();ops=[];original_faces=0;cap_faces=0
        def literal(b):
            if b:ops.append((1,b))
        def triangles(offset,count,caps,key):
            nonlocal original_faces,cap_faces
            if caps>count or count%3:raise ValueError('Bad cache geometry')
            end=offset+count*2;capstart=end-caps*6
            for i in range(offset,capstart,6):
                tri=data[i:i+6]
                if tri not in maps[key]:raise ValueError('Retained triangle not in original')
                addr=maps[key][tri]
                if ops and ops[-1][0]==0 and ops[-1][1]+ops[-1][2]==addr:ops[-1]=(0,ops[-1][1],ops[-1][2]+6)
                else:ops.append((0,addr,6))
                original_faces+=1
            literal(data[capstart:end]);cap_faces+=caps;return end
        if name.endswith('svni'):
            magic,version,count,verts,tris,source_count,caps,bones=struct.unpack_from('<4s7I',data)
            literal(data[:32]);end=triangles(32,count,caps,'ch_inview_noTMD');literal(data[end:])
        elif name.endswith('svtg'):
            magic,version,verts,tris,caps,source_count=struct.unpack_from('<4s5I',data)
            literal(data[:24]);end=triangles(24,tris*3,caps,'ch_inview_tmd_mesh')
            if end!=len(data):raise ValueError('Trailing geometry')
        else:
            literal(data[:16]);at=16
            for key in ['ch_inview_noTMD','ch_inview_tmd_mesh']:
                ident,verts,tris,source_count,count,caps=struct.unpack_from('<6I',data,at)
                literal(data[at:at+24]);at=triangles(at+24,count,caps,key)
            if at!=len(data):raise ValueError('Trailing cuff geometry')
        recipe=stage/(name+'.svrp')
        tape=bytearray(struct.pack('<4sIQI',b'SVRP',1,len(data),len(ops)))
        result=bytearray()
        for op in ops:
            if op[0]==0:tape+=struct.pack('<BQI',0,op[1],op[2]);result+=core[op[1]:op[1]+op[2]]
            else:tape+=struct.pack('<BI',1,len(op[1]))+op[1];result+=op[1]
        if result!=data:raise ValueError('Independent reconstruction mismatch')
        recipe.write_bytes(tape)
        files.append(dict(path=pins[name]['destination'],entry='',sha256=sha(path),kind='recipe',ownership='local-transform',sourcePath='RvGame/CookedPC/RvGame.xxx',sourceSha256=sha(original),sourceCodec='ue3-whole-lzo',decodedSha256=sha(decoded),decodedBytes=len(core),recipe=[dict(source='instructions',offset=0,length=len(data),entry=recipe.name)]))
        receipts.append(dict(name=name,originalTrianglesCopied=original_faces,authoredCapTriangles=cap_faces,targetSha256=sha(path),originalTriangleLiterals=0))
    (output/'files.json').write_text(json.dumps(files,indent=2)+'\n')
    module=dict(id='native-hand-assets',labels={'en':'Native tracked hands','pl':'Natywne sledzone dlonie'},version='v7-20261026',state='preview',required=True,dependencies=['vr-core'],downloadBytes=0,downloadUrl=None,archiveSha256='0'*64,expandedBytes=sum(p.stat().st_size for p in stage.iterdir())+sum(inputs[n].stat().st_size for n in inputs),rightsVerified=False,rightsEvidence='',files=files,reason='Local source reconstruction; catalog integration review pending')
    (output/'module.json').write_text(json.dumps(module,indent=2)+'\n')
    (output/'evidence.json').write_text(json.dumps(dict(status='PYTHON_EXACT_HASH_PASS_CSHARP_PENDING',files=receipts,steamAccess='NONE'),indent=2)+'\n')
    print(json.dumps(receipts))
if __name__=='__main__':
    p=argparse.ArgumentParser()
    for k in ['source','decoded','original','psks','output']:p.add_argument('--'+k,required=True,type=Path)
    a=p.parse_args();prepare(**vars(a))
