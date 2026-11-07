"""Fixed offline PNG -> Evolution texture worker. Never writes to game input."""
import hashlib,json,sys
from pathlib import Path
import manage_comic_textures as m
from convert_raw_textures import deinterleave_dxt1_stream,deinterleave_dxt5_stream

def native_raw_payload(tag,format,data):
    """Raw TOC entries require interleaved BC blocks, unlike compressed cache streams.

    The source encoder emits 16 KiB planar chunks. Keep the H descriptor intact;
    unplane B/F exactly once, before publishing packed == size in the TOC.
    See Darkness2VR ef56b259 (physically accepted in c8015216).
    """
    if tag=="H":return data
    if tag not in ("B","F"):raise ValueError("Unknown texture cache")
    if format not in ("DXT1","DXT5"):raise ValueError("Unsupported native block format")
    if len(data)%(8 if format=="DXT1" else 16):raise ValueError("Native block alignment")
    return (deinterleave_dxt1_stream if format=="DXT1" else deinterleave_dxt5_stream)(data)

def main():
    original,png,output=map(Path,sys.argv[1:])
    print("Preparing native texture cache",flush=True)
    if output.exists():raise ValueError("Fresh output required")
    output.mkdir(parents=True)
    bundle=Path(getattr(sys,"_MEIPASS",Path(__file__).parent))
    m.QBMS_EXE=bundle/"quickbms.exe"
    m.BMS_SCRIPT=bundle/"darkness2.bms"
    # Decode PNGs before invoking the encoder, bound allocation and reject ambiguous names.
    from PIL import Image
    images=list(png.rglob("*.png"))
    if not 1<=len(images)<=128:raise ValueError("Texture count")
    names=set()
    for image in images:
        if image.name.lower() in names:raise ValueError("Duplicate PNG basename")
        names.add(image.name.lower())
        with Image.open(image) as im:
            if im.format!="PNG" or min(im.size)<4 or max(im.size)>4096:raise ValueError("PNG dimensions")
            im.verify()
    package=output/"encoded"
    m.build_package(original,png,package)
    manifest=json.loads((package/"manifest.json").read_text())
    files=[]
    for tag in ("H","B","F"):
        stem=tag+".Texture"
        cache=m.Cache(original,stem)
        toc=bytearray(cache.toc)
        source=original/(stem+".cache")
        tail=output/(stem+".append")
        with source.open("rb") as f:
            digest=hashlib.file_digest(f,"sha256")
        length=source.stat().st_size
        with tail.open("xb") as stream:
            for item in manifest["items"]:
                if not item[tag.lower()+"_size"]:continue
                rel=item["path"]
                data=(package/tag/rel).read_bytes()
                data=native_raw_payload(tag,item["format"],data)
                pos=cache.entries[rel][0]
                m.struct.pack_into("<Q",toc,pos,length+stream.tell())
                m.struct.pack_into("<III",toc,pos+16,len(data),len(data),0)
                stream.write(data);digest.update(data)
        tocfile=output/(stem+".toc")
        tocfile.write_bytes(toc)
        files.extend([
            dict(path="Cache.Windows/"+stem+".cache",sourceBytes=length,append=tail.name,
                 sha256=digest.hexdigest().upper(),beforeSha256=m.sha(source).upper(),bytes=length+tail.stat().st_size),
            dict(path="Cache.Windows/"+stem+".toc",payload=tocfile.name,sha256=m.sha(tocfile).upper(),
                 beforeSha256=m.sha(original/(stem+".toc")).upper(),bytes=len(toc))
        ])
    (output/"result.json").write_text(json.dumps(files),encoding="utf-8")
if __name__=="__main__":main()
