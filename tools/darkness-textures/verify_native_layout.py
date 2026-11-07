"""Read-only native BC decoding audit against PNGs and an accepted raw cache.

Usage: python verify_native_layout.py ENCODED PNG_ROOT ACCEPTED_CACHE REPORT.json
Reads only targeted payloads; does not duplicate multi-gigabyte caches.
"""
import json,sys
from pathlib import Path
import numpy as np
from PIL import Image
from main import native_raw_payload
from polish_pack import Cache
from convert_raw_textures import dxt_chain_size

def audit(encoded,png_root,accepted,report):
    items=json.loads((encoded/"manifest.json").read_text())["items"]
    pngs={p.name.lower():p for p in png_root.rglob("*.png")}
    caches={tag:Cache(accepted,tag+".Texture") for tag in ("H","B","F")}
    results=[]
    for item in items:
        rel=item["path"]
        for tag in ("H","B","F"):
            if not item[tag.lower()+"_size"]:continue
            planar=(encoded/tag/rel).read_bytes()
            fixed=native_raw_payload(tag,item["format"],planar)
            _,off,packed,size=caches[tag].entries[rel]
            with (accepted/(tag+".Texture.cache")).open("rb") as stream:
                stream.seek(off);golden=stream.read(packed)
            row=dict(path=rel,cache=tag,raw=packed==size,acceptedBytesEqual=fixed==golden)
            assert row["raw"] and row["acceptedBytesEqual"],row
            if tag!="H":
                w,h=map(int,item["base_dim" if tag=="B" else "full_dim"].split("x"))
                block=8 if item["format"]=="DXT1" else 16
                count=((w+3)//4)*((h+3)//4)*block
                end=dxt_chain_size(w,h,block) if tag=="B" else len(fixed)
                mode=1 if block==8 else 3
                target=np.asarray(Image.open(pngs[Path(rel).name.lower()]).convert("RGB").resize((w,h),Image.Resampling.LANCZOS)).astype(float)
                for label,data in (("fixed",fixed),("oldPlanar",planar)):
                    decoded=Image.frombytes("RGBA",(w,h),data[end-count:end],"bcn",mode).convert("RGB")
                    row[label+"RgbMae"]=float(np.abs(np.asarray(decoded).astype(float)-target).mean())
                # BC compression is lossy. Independent native decoder must match intended pixels.
                assert row["fixedRgbMae"]<16,row
            results.append(row)
    result=dict(textures=len(items),payloads=len(results),acceptedPayloadBytesEqual=True,
                independentDecoder="Pillow bcn",results=results)
    report.parent.mkdir(parents=True,exist_ok=True)
    report.write_text(json.dumps(result,indent=2))
    print(json.dumps({k:v for k,v in result.items() if k!="results"}))

if __name__=="__main__":audit(*map(Path,sys.argv[1:]))
