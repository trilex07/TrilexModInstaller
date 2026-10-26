# Reused from MIT SingularityVR tools/audio/vorbis_fixed_allocation.py.
"""Fixed-allocation Vorbis replacement; no export relocation or runtime hooks.

Padding is a valid Vorbis vendor comment, never bytes after EOS. Original audio
packets/granules remain byte-identical. Refuse unfamiliar/multiplexed streams.
"""
import struct


def require(ok, why):
    if not ok: raise ValueError(why)


def _crc_table():
    table=[]
    for value in range(256):
        crc=value<<24
        for _ in range(8): crc=((crc<<1)^ (0x04c11db7 if crc&0x80000000 else 0))&0xffffffff
        table.append(crc)
    return table

_TABLE=_crc_table()
def crc(data):
    result=0
    for byte in data: result=((result<<8)&0xffffffff)^_TABLE[((result>>24)^byte)&255]
    return result


def pages(data):
    result=[]; at=0; serial=None
    while at<len(data):
        require(data[at:at+4]==b'OggS' and data[at+4]==0,'invalid Ogg header')
        require(at+27<=len(data),'truncated Ogg')
        count=data[at+26]; end=at+27+count
        require(end<=len(data),'truncated lacing')
        end+=sum(data[at+27:end]); require(end<=len(data),'truncated page')
        page=bytearray(data[at:end]); expected=struct.unpack_from('<I',page,22)[0]
        struct.pack_into('<I',page,22,0); require(crc(page)==expected,'Ogg CRC mismatch')
        sid=struct.unpack_from('<I',page,14)[0]
        if serial is None: serial=sid
        require(sid==serial,'multiplexed Ogg not supported')
        require(struct.unpack_from('<I',page,18)[0]==len(result),'nonsequential Ogg')
        result.append(bytes(data[at:end]));at=end
    require(result and result[0][5]&2 and result[-1][5]&4,'missing BOS/EOS')
    return result


def _page(body,lacing,serial,sequence,flags,granule):
    p=bytearray(b'OggS'+bytes([0,flags])+struct.pack('<QIII',granule,serial,sequence,0)+bytes([len(lacing)])+bytes(lacing)+body)
    struct.pack_into('<I',p,22,crc(p));return bytes(p)


def _headers_and_audio(source):
    pp=pages(source);packets=[];part=bytearray()
    for page_index,p in enumerate(pp):
        lace=p[27:27+p[26]];at=27+p[26]
        for segment_index,size in enumerate(lace):
            part+=p[at:at+size];at+=size
            if size<255:
                packets.append(bytes(part));part.clear()
                if len(packets)==3:
                    require(segment_index==len(lace)-1,'audio shares setup page')
                    require([p[:7] for p in packets]==[b'\x01vorbis',b'\x03vorbis',b'\x05vorbis'],'invalid Vorbis headers')
                    return packets,pp[page_index+1:],struct.unpack_from('<I',p,14)[0]
    raise ValueError('missing Vorbis headers')


def audio_packets(source):
    """CRC-checked compressed packets, excluding the three Vorbis headers."""
    _,audio,_=_headers_and_audio(source)
    result=[];part=bytearray()
    for page in audio:
        at=27+page[26]
        for size in page[27:at]:
            part+=page[at:at+size];at+=size
            if size<255:
                result.append(bytes(part));part.clear()
    require(not part and result,'incomplete/missing audio packet')
    return result


def pad_to_allocation(source,target_size):
    headers,audio,serial=_headers_and_audio(source)
    require(audio,'no audio pages')
    vendor_size=struct.unpack_from('<I',headers[1],7)[0]
    require(11+vendor_size<len(headers[1]),'invalid vendor comment')
    def build(padding):
        comment=headers[1][:7]+struct.pack('<I',vendor_size+padding)+headers[1][11:11+vendor_size]+b' '*padding+headers[1][11+vendor_size:]
        out=[]
        for packet_index,packet in enumerate([headers[0],comment,headers[2]]):
            lacing=[255]*(len(packet)//255)+[len(packet)%255];at=0;continued=False
            for i in range(0,len(lacing),255):
                lace=lacing[i:i+255];size=sum(lace)
                out.append(_page(packet[at:at+size],lace,serial,len(out),(2 if packet_index==0 else 0)|(1 if continued else 0),0 if lace[-1]<255 else 0xffffffffffffffff))
                at+=size;continued=True
        for p in audio:
            q=bytearray(p);struct.pack_into('<I',q,18,len(out));struct.pack_into('<I',q,22,0);struct.pack_into('<I',q,22,crc(q));out.append(bytes(q))
        return b''.join(out)
    base=build(0);require(len(base)<=target_size,'encoded Vorbis exceeds native allocation')
    lo=0;hi=target_size-len(base)
    # Length is monotonic. Header lacing/page overhead can leave unreachable sizes.
    while lo<hi:
        mid=(lo+hi+1)//2
        # Avoid repeated CRC of long audio pages during size search.
        packet_len=len(headers[1])+mid
        lace_count=packet_len//255+1
        old_len=len(headers[1]);old_laces=old_len//255+1
        n=len(base)+mid+lace_count-old_laces+27*((lace_count+254)//255-(old_laces+254)//255)
        if n<=target_size:lo=mid
        else:hi=mid-1
    output=build(lo);require(len(output)==target_size,'native allocation not exactly reachable')
    _,new_audio,_=_headers_and_audio(output)
    require(len(audio)==len(new_audio),'audio pages changed')
    for a,b in zip(audio,new_audio):
        require(a[:18]==b[:18] and a[26:]==b[26:],'audio packets/granules changed')
    return output
