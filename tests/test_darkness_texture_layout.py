"""Native raw cache contract, independent block fixtures across chunk boundaries."""
import sys,unittest
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/"tools/darkness-textures"))
from main import native_raw_payload

class NativeLayoutTests(unittest.TestCase):
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
