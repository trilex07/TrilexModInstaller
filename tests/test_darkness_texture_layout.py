"""Native raw cache contract, independent block fixtures across chunk boundaries."""
import sys,unittest
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/"tools/darkness-textures"))
from main import native_raw_payload
import manage_comic_textures as encoder
import numpy as np
from PIL import Image
from unittest.mock import patch

class NativeLayoutTests(unittest.TestCase):
    def test_bounded_encoder_matches_original_bytes_across_strip_boundaries(self):
        rng=np.random.default_rng(1234)
        for width,height in ((1,1),(4,4),(128,64),(128,68),(256,260)):
            image=Image.fromarray(rng.integers(0,256,(height,width,3),dtype=np.uint8))
            expected=encoder._encode_dxt1_strip(image)
            actual=encoder.encode_dxt1_blocks_np(image)
            for before,after in zip(expected,actual):self.assertEqual(before.tobytes(),after.tobytes())

    def test_4k_input_never_reaches_encoder_as_whole_image(self):
        calls=[]
        def record(image):
            calls.append(image.size)
            return np.zeros(image.width*image.height//16,dtype=np.uint32),np.zeros(image.width*image.height//16,dtype=np.uint32)
        with patch.object(encoder,'_encode_dxt1_strip',side_effect=record):
            result=encoder.encode_dxt1_blocks_np(Image.new('RGB',(4096,4096)))
        self.assertEqual(calls,[(4096,64)]*64)
        self.assertEqual(len(result[0]),4096*4096//16)

    def check_layout(self,fmt,widths):
        stride=sum(widths)
        # Distinct bytes in every field/block; includes two full 16KiB chunks and a tail.
        blocks=[bytes(((i*17+j*29)&255 for j in range(stride))) for i in range(32768//stride+7)]
        planar=bytearray()
        for start in range(0,len(blocks),16384//stride):
            chunk=blocks[start:start+16384//stride]
            field=0
            for width in widths:
                planar.extend(b"".join(block[field:field+width] for block in chunk))
                field+=width
        expected=b"".join(blocks)
        self.assertNotEqual(bytes(planar),expected)
        for tag in ("B","F"):
            self.assertEqual(native_raw_payload(tag,fmt,bytes(planar)),expected)

    def test_bc1_native_blocks(self):self.check_layout("DXT1",(4,4))
    def test_bc3_native_blocks_including_alpha(self):self.check_layout("DXT5",(2,6,4,4))
    def test_header_unchanged(self):self.assertEqual(native_raw_payload("H","DXT1",b"descriptor"),b"descriptor")
    def test_reject_invalid_contract(self):
        for tag,fmt,data in (("B","DXT1",b"x"),("F","DXT5",b"x"*8),("B","DXT3",b"x"*16),("X","DXT1",b"")):
            with self.assertRaises(ValueError):native_raw_payload(tag,fmt,data)

if __name__=="__main__":unittest.main()
