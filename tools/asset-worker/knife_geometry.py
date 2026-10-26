# Copyright (c) 2026 letsgosportsteam; MIT, see third_party/SingularityVR-MIT.txt.
# Fixed accepted original-derived knife calibration. Source geometry is read locally.
import struct,json,math
from pathlib import Path
import numpy as np

def frames(vertices,faces,uv):
    n=np.zeros_like(vertices);t=np.zeros_like(vertices);b=np.zeros_like(vertices)
    volume=float(np.sum(vertices[faces[:,0]]*np.cross(vertices[faces[:,1]],vertices[faces[:,2]]))/6)
    for face in faces:
        p=vertices[face];a=p[1]-p[0];c=p[2]-p[0];normal=np.cross(a,c)*(1 if volume>0 else -1)
        delta=uv[face[1:]]-uv[face[0]];det=np.linalg.det(delta)
        n[face]+=normal
        if abs(det)>1e-12:
            t[face]+=(a*delta[1,1]-c*delta[0,1])/det
            b[face]+=(c*delta[0,0]-a*delta[1,0])/det
    for i in range(len(vertices)):
        if np.linalg.norm(n[i])<1e-12:raise ValueError('degenerate normal')
        n[i]/=np.linalg.norm(n[i]);t[i]-=n[i]*(n[i]@t[i])
        if np.linalg.norm(t[i])<1e-12:
            axis=np.eye(3)[np.argmin(np.abs(n[i]))];t[i]=np.cross(axis,n[i])
        t[i]/=np.linalg.norm(t[i])
    sign=np.where(np.sum(np.cross(n,t)*b,axis=1)<0,-1.,1.)
    return np.c_[vertices,n,t,sign,uv].astype('<f4')

def generate(source,output,metadata):
 op=json.loads(metadata.read_text(encoding='utf-8-sig'))
 if set(op)!={'kind','positions','uvs','indices'} or op['kind'] not in ('svrk','svnl'):raise ValueError('Unsupported geometry recipe')
 positions=op['positions'];uvs=op['uvs'];at=op['indices']
 if len(positions)!=662 or len(uvs)!=662 or type(at)!=int:raise ValueError('Unsupported accepted mesh dimensions')
 data=source.read_bytes()
 def bounded(offset,size):
  if type(offset)!=int or offset<0 or offset>len(data)-size:raise ValueError('Geometry reference outside owned source')
  return offset
 points=[struct.unpack_from('<3f',data,bounded(x,12)) for x in positions]
 uv=[struct.unpack_from('<2e',data,bounded(x,4)) for x in uvs]
 if any(not all(math.isfinite(v) for v in p) for p in points+uv):raise ValueError('Nonfinite geometry')
 extent=max(-p[1] for p in points)-min(-p[1] for p in points)
 if extent<1:raise ValueError('Original blade extent invalid')
 scale=.30/extent;vertices=[]
 for x,y,z in points:
  # Original cooked +Y is ActorX -Y. Preserve accepted float32 bake boundary.
  x,y,z=struct.unpack('<3f',struct.pack('<3f',z*scale,x*scale,-y*scale))
  if z<-.08:
   factor=1+.25*min(1,(-.08-z)/.02);x*=factor;y*=factor;z=-.08+(z+.08)*1.4
  vertices.append(struct.unpack('<3f',struct.pack('<3f',x,y,z)))
 indices=np.frombuffer(data,'<u2',3030,bounded(at,6060)).reshape(-1,3)[:,[1,0,2]].copy()
 if int(indices.max())>=662:raise ValueError('Invalid original mesh indices')
 if op['kind']=='svrk':
  result=struct.pack('<4sIII',b'SVRK',2,662,3030)+b''.join(struct.pack('<3fI2f',*p,0xffffffff,*t) for p,t in zip(vertices,uv))+indices.astype('<u2').tobytes()
 else:
  rows=frames(np.array(vertices,dtype=np.float64),indices,np.array(uv,dtype=np.float64))
  result=struct.pack('<4sIII',b'SVNL',1,662,3030)+rows.tobytes()+indices.astype('<u2').tobytes()
 with output.open('xb') as stream:stream.write(result)
