"""Fixed local image operations. No downloaded Python or package code is executed.

The source image must be generated from a separately hash-verified owned input.
Final output identities are verified by the installer, independently of this worker.
"""
import argparse, json, struct, sys
from knife_geometry import generate as generate_knife
from pathlib import Path
import numpy as np
from PIL import Image, ImageFilter
from accepted_algorithms import unsharp_luminance, signed_upsample_2x, enhance_normal, enhance_normal_procedural
from dds_io import load_dds, resize_rgba_independent, save_dds, save_normal_dds

def transform(source,output,role,factor,fourcc,diffuse=None):
    if factor not in (1,2,4) or role not in ('diffuse','normal','svrt','weapon-diffuse-v2','weapon-normal-v2','weapon-normal-procedural-v2') or fourcc not in ('DXT1','DXT5'):raise ValueError('Unsupported fixed image operation')
    if output.exists() or source.stat().st_size>64*1024*1024:raise ValueError('Fresh bounded output required')
    image,header=load_dds(source)
    if max(image.size)>4096 or min(image.size)<4 or max(image.size)*factor>8192:raise ValueError('Image dimensions exceed budget')
    if role=='svrt':
        if factor!=1:raise ValueError('SVRT is exact original pixels only')
        result=struct.pack('<4sIII',b'SVRT',1,*image.size)+np.asarray(image)[:,:,[2,1,0,3]].tobytes()
        with output.open('xb') as stream:stream.write(result)
        return
    if role.startswith('weapon-'):
        if factor!=2:raise ValueError('Accepted legacy weapon recipe is 2x only')
        if role=='weapon-diffuse-v2':
            sharp=unsharp_luminance(image,radius=2.0,percent=20,threshold=5)
            enlarged=resize_rgba_independent(sharp,(image.width*2,image.height*2),Image.Resampling.LANCZOS)
            payload=enlarged if np.asarray(enlarged)[:,:,3].min()<255 else enlarged.convert('RGB')
            save_dds(output,payload,fourcc)
        else:
            if role=='weapon-normal-v2':
                if diffuse is None:raise ValueError('Verified diffuse input required')
                paired,_=load_dds(diffuse)
                if paired.size!=image.size:raise ValueError('Paired source size mismatch')
                sharp=unsharp_luminance(paired,radius=2.0,percent=20,threshold=5)
                native,_=enhance_normal(image,sharp,'weapon')
            else:native,_=enhance_normal_procedural(image,'weapon')
            save_normal_dds(output,signed_upsample_2x(native))
        return
    if role=='normal':
        if factor!=2:raise ValueError('Accepted normal recipe is 2x only')
        rgb=np.asarray(image.convert('RGB'),dtype=np.float32)/127.5-1.0
        channels=[Image.fromarray(np.uint8(np.clip((rgb[:,:,i]+1.0)*127.5,0,255))).resize((image.width*factor,image.height*factor),Image.Resampling.LANCZOS) for i in range(3)]
        up=np.stack([np.asarray(ch,dtype=np.float32)/127.5-1.0 for ch in channels],axis=2)
        up/=np.maximum(np.linalg.norm(up,axis=2,keepdims=True),1e-6)
        enlarged=Image.fromarray(np.uint8(np.clip((up+1.0)*127.5+0.5,0,255)),'RGB')
        save_normal_dds(output,enlarged)
    else:
        clean=resize_rgba_independent(image,(image.width*factor,image.height*factor),Image.Resampling.LANCZOS)
        rgb=clean.convert('RGB').filter(ImageFilter.UnsharpMask(radius=1.0,percent=38,threshold=5))
        enlarged=rgb.convert('RGBA');enlarged.putalpha(clean.getchannel('A'));save_dds(output,enlarged,fourcc)
if __name__=='__main__':
    if len(sys.argv)==5 and sys.argv[1]=='--knife':
        generate_knife(Path(sys.argv[2]),Path(sys.argv[3]),Path(sys.argv[4]));sys.exit(0)
    p=argparse.ArgumentParser();p.add_argument('source',type=Path);p.add_argument('output',type=Path);p.add_argument('role',choices=['diffuse','normal','svrt','weapon-diffuse-v2','weapon-normal-v2','weapon-normal-procedural-v2']);p.add_argument('factor',type=int,choices=[1,2,4]);p.add_argument('fourcc',choices=['DXT1','DXT5']);p.add_argument('--diffuse',type=Path);a=p.parse_args()
    transform(a.source,a.output,a.role,a.factor,a.fourcc,a.diffuse)
    print(json.dumps({'status':'GENERATED_REQUIRES_FINAL_HASH_CHECK','bytes':a.output.stat().st_size}))
