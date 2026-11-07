import copy,importlib.util,io,struct,unittest,zipfile
from pathlib import Path
spec=importlib.util.spec_from_file_location('refresh',Path(__file__).resolve().parents[1]/'scripts/refresh-darkness-runtime.py')
m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)

def dll(machine=0x14c):
    result=bytearray(256);result[:2]=b'MZ';struct.pack_into('<I',result,60,128)
    result[128:132]=b'PE\0\0';struct.pack_into('<H',result,132,machine)
    struct.pack_into('<H',result,150,0x2000);struct.pack_into('<H',result,152,0x10b)
    return bytes(result)

class RefreshTests(unittest.TestCase):
    def fixture(self):
        entries={'d3d9.dll':b'old','Darkness2VR.ini':b'preserved settings'}
        data=io.BytesIO()
        with zipfile.ZipFile(data,'w') as z:
            for name,value in entries.items():z.writestr(name,value)
        packed=data.getvalue()
        module=dict(id='vr-core',version='1.0.8',archiveSha256=m.sha(packed),files=[dict(path=k,entry=k,sha256=m.sha(v)) for k,v in entries.items()])
        return dict(version='1.0.8',games=[dict(id='singularity',modules=[dict(id='untouched')]),dict(id='darkness2',modules=[module,dict(id='darkness-menu')])]),packed

    def test_scoped_refresh_preserves_other_modules_and_settings(self):
        original,archive=self.fixture();snapshot=copy.deepcopy(original);native=dll()
        updated,packed=m.refresh(original,archive,native,m.sha(native))
        self.assertEqual(original,snapshot)
        self.assertEqual(updated['version'],original['version'])
        self.assertEqual(updated['games'][0],original['games'][0])
        self.assertEqual(updated['games'][1]['modules'][1:],original['games'][1]['modules'][1:])
        with zipfile.ZipFile(io.BytesIO(packed)) as z:
            self.assertEqual(z.read('d3d9.dll'),native)
            self.assertEqual(z.read('Darkness2VR.ini'),b'preserved settings')
        self.assertEqual(updated['games'][1]['modules'][0]['archiveSha256'],m.sha(packed))

    def test_reject_wrong_hash(self):
        original,archive=self.fixture()
        with self.assertRaises(ValueError):m.refresh(original,archive,dll(),'0'*64)
        with self.assertRaises(ValueError):m.refresh(original,archive+b'x',dll(),m.sha(dll()))

    def test_reject_wrong_architecture(self):
        original,archive=self.fixture();native=dll(0x8664)
        with self.assertRaises(ValueError):m.refresh(original,archive,native,m.sha(native))

if __name__=='__main__':unittest.main()
