"""Offline package producer regressions; no game or private assets required."""
import hashlib
import importlib.util
import struct
import tempfile
import subprocess
import random
import unittest
from pathlib import Path

ROOT=Path(__file__).resolve().parent.parent

def load(name,path):
    spec=importlib.util.spec_from_file_location(name,ROOT/path)
    module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module);return module

pe=load('pe_dependencies','scripts/pe_dependencies.py')
deltas=load('deltas','scripts/prepare-v7-deltas.py')

class ProducerTests(unittest.TestCase):
    def test_delta_roundtrips_empty_grow_shrink_and_multichunk(self):
        for before,after in [(b'',b'x'),(b'abc',b''),(b'abcd',b'xy'),(b'a'*1100000,b'b'*1100000+b'tail')]:
            with self.subTest(sizes=(len(before),len(after))),tempfile.TemporaryDirectory(dir=ROOT/'artifacts') as temp:
                root=Path(temp);old=root/'old';new=root/'new';stage=root/'stage';stage.mkdir()
                old.write_bytes(before);new.write_bytes(after)
                parts=deltas.delta(old,new,stage,'fixture');result=bytearray()
                for part in parts:
                    offset=part['offset'];length=part['length'];payload=(stage/part['entry']).read_bytes()[offset:offset+length]
                    result.extend(bytes(a^b for a,b in zip(before[offset:offset+length],payload)) if part['source']=='xor-original' else payload)
                self.assertEqual(bytes(result),after)
    def test_pe_normal_and_delay_imports_without_loading(self):
        image=bytearray(2048);image[:2]=b'MZ';struct.pack_into('<I',image,60,128);image[128:132]=b'PE\0\0'
        struct.pack_into('<HH',image,132,0x14c,1);struct.pack_into('<H',image,148,224)
        opt=152;struct.pack_into('<H',image,opt,0x10b);struct.pack_into('<I',image,opt+28,0x400000);struct.pack_into('<I',image,opt+60,512);struct.pack_into('<I',image,opt+92,16)
        struct.pack_into('<IIII',image,opt+224+8,1536,0x1000,1536,512)
        struct.pack_into('<II',image,opt+96+8,0x1000,40);struct.pack_into('<I',image,512+12,0x1100)
        image[768:781]=b'KERNEL32.dll\0'
        struct.pack_into('<II',image,opt+96+13*8,0x1200,64);struct.pack_into('<II',image,1024,1,0x1300);image[1280:1296]=b'D3DCOMPILER.dll\0'
        self.assertEqual(pe.inspect(image)['imports'],['d3dcompiler.dll','kernel32.dll'])
        image[768:781]=b'../evil.dll\0\0'
        with self.assertRaises(ValueError):pe.inspect(image)
    def test_native_instruction_maker_roundtrips(self):
        tool=ROOT/'.build/recipe-maker/recipe-maker.exe'
        self.assertTrue(tool.is_file(), 'Run scripts/build-recipe-maker.ps1 first')
        rng=random.Random(42)
        block=bytes(rng.randrange(256) for _ in range(8192))
        for before,after in [(b'',b'fresh'),(b'old',b''),(block,block[128:4096]+b'insert'+block[4096:]),(b'a'*16384,b'a'*5000+b'tail')]:
            with tempfile.TemporaryDirectory(dir=ROOT/'artifacts') as temp:
                root=Path(temp);old=root/'original';new=root/'target';tape=root/'recipe'
                old.write_bytes(before);new.write_bytes(after)
                subprocess.run([str(tool),str(old),str(new),str(tape)],check=True,capture_output=True)
                data=tape.read_bytes();magic,version,size,count=struct.unpack_from('<4sIQI',data)
                self.assertEqual((magic,version,size),(b'SVRP',1,len(after)))
                cursor=20;result=bytearray()
                for _ in range(count):
                    opcode=data[cursor];cursor+=1
                    if opcode==0:
                        offset,length=struct.unpack_from('<QI',data,cursor);cursor+=12;result+=before[offset:offset+length]
                    else:
                        self.assertEqual(opcode,1);length=struct.unpack_from('<I',data,cursor)[0];cursor+=4;result+=data[cursor:cursor+length];cursor+=length
                self.assertEqual(cursor,len(data));self.assertEqual(bytes(result),after)
    def test_reject_non_x86(self):
        for image in [b'',b'not an executable']:
            with self.assertRaises((ValueError,struct.error)):pe.inspect(image)

if __name__=='__main__':unittest.main()
