"""Offline package producer regressions; no game or private assets required."""
import hashlib
import importlib.util
import struct
import tempfile
import subprocess
import random
import unittest
import json
import sys
from unittest.mock import patch
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent.parent/"tools/asset-worker"))
import audio_remaster as audio

ROOT=Path(__file__).resolve().parent.parent
sys.path.insert(0,str(ROOT/'scripts'))
import runtime_resources as resources

def load(name,path):
    spec=importlib.util.spec_from_file_location(name,ROOT/path)
    module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module);return module

pe=load('pe_dependencies','scripts/pe_dependencies.py')
deltas=load('deltas','scripts/prepare-v7-deltas.py')
composer=load('composer','scripts/compose-v7-candidate.py')
payload_audit=load('payload_audit','scripts/audit-recipe-payloads.py')
first_edition=load('first_edition','scripts/prepare-first-edition.py')
runtime_prepare=load('runtime_prepare','scripts/prepare-runtime-resources.py')

class ProducerTests(unittest.TestCase):
    def test_runtime_resource_closure_rejects_every_missing_or_changed_dependency(self):
        rows=resources.inventory()['files']
        modules=[dict(id=name,files=[dict(path=r['path'],kind='copy',sha256=r['sha256'],
                 **({'beforeSha256':r['beforeSha256']} if 'beforeSha256' in r else {}))
                 for r in rows if r['module']==name])
                 for name in sorted({r['module'] for r in rows})]
        self.assertEqual(resources.require_closure(modules),len(rows))
        for module in modules:
            for index,file in enumerate(list(module['files'])):
                with self.subTest(file=file['path']):
                    module['files'].remove(file)
                    with self.assertRaisesRegex(ValueError,'Missing accepted runtime'):
                        resources.require_closure(modules)
                    module['files'].insert(index,file)
                    saved=file['sha256'];file['sha256']='0'*64
                    with self.assertRaisesRegex(ValueError,'identity/owner'):
                        resources.require_closure(modules)
                    file['sha256']=saved

    def test_wheel_catalog_actual_gun_mapping_and_no_false_cycle_verification(self):
        catalog=dict(schema='singularityvr-weapon-wheel-catalog-v1',cycle_order_verified=False,
                     cycle_order=[5799,11387,8297,11389],sectors=[dict(index=i,gun_verts=v,atlas_icon=n)
                     for i,(v,n) in enumerate([(5799,'gun-centurion'),(11387,'gun-valkyrie'),
                                               (8297,'gun-spikeshot'),(11389,'gun-kasimov')])])
        self.assertFalse(resources.validate_catalog(json.dumps(catalog))['cycle_order_verified'])
        catalog['sectors'][0]['atlas_icon']='gun-valkyrie'
        with self.assertRaisesRegex(ValueError,'gun/sprite'):resources.validate_catalog(json.dumps(catalog))

    def test_runtime_stager_preserves_existing_recipes_and_reconstructs_movie(self):
        with tempfile.TemporaryDirectory(dir=ROOT/'artifacts') as temp:
            root=Path(temp);backup=root/'backup';stage=root/'source-stage';stage.mkdir();backup.mkdir()
            old=backup/'original.bik';old.write_bytes(b'OWNED OLD MOVIE'*200)
            movie=backup/'RvGame/Movies/loading_screen.bik';movie.parent.mkdir(parents=True)
            movie.write_bytes(old.read_bytes()[:500]+b'AUTHORED REMASTER'*300)
            tex=backup/'Binaries/texture-override-8K/fixture.dds';tex.parent.mkdir(parents=True)
            header=bytearray(128);header[:4]=b'DDS ';struct.pack_into('<II',header,12,4,4)
            header[84:88]=b'DXT1';tex.write_bytes(header+b'\x00'*8)
            rows=[dict(path=str(p.relative_to(backup)).replace('\\','/'),module='hd-textures' if p==tex else 'authored-ui-assets',
                       sha256=resources.sha(p),bytes=p.stat().st_size) for p in [tex,movie]]
            rows[1].update(original='original.bik',beforeSha256=resources.sha(old))
            unchanged=dict(path=rows[0]['path'],entry='',kind='recipe',ownership='local-transform',
                           sha256=rows[0]['sha256'],recipe=[dict(source='original',offset=0,length=136)])
            inputs=[]
            for name,files in [('hd-textures',[unchanged]),('authored-ui-assets',[])]:
                manifest=root/(name+'.json');manifest.write_text(json.dumps(dict(id=name,files=files,rightsVerified=False)))
                inputs.append(dict(module=str(manifest),stage=str(stage)))
            graph=root/'inputs.json';graph.write_text(json.dumps(inputs))
            spec=dict(schema='fixture',runtimeSource='fixture',files=rows)
            with patch.object(runtime_prepare,'inventory',return_value=spec),patch.object(resources,'inventory',return_value=spec):
                runtime_prepare.prepare(graph,backup,root/'out')
                result=json.loads((root/'out/hd-textures/module.json').read_text())
                self.assertEqual(result['files'],[unchanged]);self.assertFalse(result['rightsVerified'])
                result=json.loads((root/'out/authored-ui-assets/module.json').read_text())
                file=result['files'][0];output=bytearray();original=old.read_bytes()
                for part in file['recipe']:
                    offset=part['offset'];length=part['length']
                    data=(root/'out/authored-ui-assets/stage'/part['entry']).read_bytes()[offset:offset+length]
                    output.extend(bytes(a^b for a,b in zip(original[offset:offset+length],data))
                                  if part['source']=='xor-original' else data)
                self.assertEqual(bytes(output),movie.read_bytes())
                tex.write_bytes(tex.read_bytes()+b'corrupt')
                with self.assertRaisesRegex(ValueError,'source changed'):
                    runtime_prepare.prepare(graph,backup,root/'refused')
                self.assertFalse((root/'refused').exists())

    def test_private_texture_cache_alias_binds_only_exact_owned_stock_bytes(self):
        file=dict(path='Binaries/texture-override-remaster/test.dds',kind='recipe',
            ownership='local-transform',sourceCodec='ue3-bulk-lzo',
            sourcePath='RvGame/CookedPC/Textures_P0.tfc',
            sourceSha256='6BE9134DF84D23B49089DA39B05594EE813B350949318A54684273104D865EE0',
            sha256='A'*64,sourceOffset=128,sourceBytes=64,recipe=[dict(source='image',entry='test.json')])
        module=dict(files=[dict(file)])
        first_edition.bind_owned_texture_cache(module)
        self.assertEqual(module['files'],[dict(file,sourcePath='RvGame/CookedPC/coalesced.tfc')])
        first_edition.bind_owned_texture_cache(module)
        self.assertEqual(module['files'][0]['sourceSha256'],file['sourceSha256'])
        for key,value in [('sourceSha256','B'*64),('sourceCodec','ue3-whole-lzo'),
                          ('kind','copy'),('ownership','mod-authored')]:
            with self.subTest(key=key),self.assertRaisesRegex(ValueError,'Unsupported private'):
                first_edition.bind_owned_texture_cache(dict(files=[dict(file,**{key:value})]))
        other=dict(file,sourcePath='RvGame/CookedPC/another.tfc')
        untouched=dict(files=[dict(other)])
        first_edition.bind_owned_texture_cache(untouched)
        self.assertEqual(untouched['files'],[other])

    def test_complete_bundle_requires_private_output_and_immutable_tag(self):
        with self.assertRaisesRegex(ValueError,'artifacts'):
            first_edition.prepare(ROOT/'missing-inputs',ROOT/'catalog/forbidden-output','installer-1.0.0-rc.1')
        with tempfile.TemporaryDirectory(dir=ROOT/'artifacts') as temp:
            with self.assertRaisesRegex(ValueError,'artifacts'):
                first_edition.prepare(ROOT/'missing-inputs',Path(temp),'installer-1.0.0-rc.1')
            with self.assertRaisesRegex(ValueError,'immutable'):
                first_edition.prepare(ROOT/'missing-inputs',Path(temp)/'fresh','latest')
            self.assertFalse((Path(temp)/'fresh').exists())
    def test_audio_rejects_unapproved_executable_before_processing(self):
        with tempfile.TemporaryDirectory(dir=ROOT/'artifacts') as temp:
            folder=Path(temp); codec=folder/'not-a-codec.exe'; codec.write_bytes(b'not executable')
            with self.assertRaisesRegex(ValueError,'identity not approved'):
                audio.generate(folder/'missing',folder/'out.xxx',folder/'missing.json',codec)
            self.assertFalse((folder/'out.xxx').exists())

    def test_audio_serial_rewrite_preserves_packet_and_valid_crc(self):
        payload=b'owned synthetic packet'; header=bytearray(27)
        header[:4]=b'OggS'; header[5]=6; header[26]=1
        page=header+bytes([len(payload)])+payload
        struct.pack_into('<I',page,22,audio.crc(page))
        result=audio.canonical_serial(bytes(page),0x12345678)
        self.assertEqual(result[28:],payload)
        self.assertEqual(struct.unpack_from('<I',result,14)[0],0x12345678)
        expected=struct.unpack_from('<I',result,22)[0]
        cleared=bytearray(result);struct.pack_into('<I',cleared,22,0)
        self.assertEqual(audio.crc(cleared),expected)

    def test_audio_fixed_profiles_exclude_unaccepted_impulse(self):
        self.assertEqual(len(audio.PROFILES),7)
        self.assertFalse(any('impulse' in key.lower() for key in audio.PROFILES))
        for filters in audio.PROFILES.values():
            self.assertTrue(filters)
            self.assertTrue(all(isinstance(f,str) and ';' not in f for f in filters))

    def test_recipe_payload_audit_counts_literals_and_rejects_corruption(self):
        recipe=struct.pack('<4sIQI',b'SVRP',1,10,2)+b'\x00'+struct.pack('<QI',0,6)+b'\x01'+struct.pack('<I',4)+b'OggS'
        result=payload_audit.inspect(recipe)
        self.assertEqual(result['copiedBytes'],6)
        self.assertEqual(result['literalBytes'],4)
        self.assertEqual(result['literalOggPageMarkers'],1)
        for bad in (recipe[:-1],recipe+b'x',recipe[:20]+b'\x02',b'bad'):
            with self.subTest(data=bad),self.assertRaises(ValueError):payload_audit.inspect(bad)

    def test_composition_preserves_rights_gates_and_rejects_missing_inventory(self):
        with tempfile.TemporaryDirectory(dir=ROOT/'artifacts') as temp:
            root=Path(temp);inventory=json.loads((ROOT/'inventory/singularity-v7.json').read_text())
            files=[dict(path=f['destination'],kind='copy') for f in inventory['files'] if f['destination'] not in composer.EXCLUDED]
            allfiles={f['path'].lower():f for f in files}
            extra={'authored-ui-assets':[], 'hd-textures':[], 'centurion-assets':[]}
            for row in resources.inventory()['files']:
                f=dict(path=row['path'],kind='copy',sha256=row['sha256'],
                       **({'beforeSha256':row['beforeSha256']} if 'beforeSha256' in row else {}))
                allfiles.pop(row['path'].lower(),None)
                if row['module'] in extra:extra[row['module']].append(f)
                else:allfiles[row['path'].lower()]=f
            core=dict(id='runtime-code',required=False,dependencies=[],files=list(allfiles.values()),rightsVerified=False)
            settings=dict(id='portable-settings',required=True,dependencies=['vr-core'],files=[],rightsVerified=True)
            specs=[('core',core),('settings',settings)]+[(name,dict(id=name,required=False,dependencies=['vr-core'],files=value,rightsVerified=False)) for name,value in extra.items()]
            for name,m in specs:(root/(name+'.json')).write_text(json.dumps(m))
            inputs=root/'inputs.json';inputs.write_text(json.dumps([dict(module=str(root/(name+'.json'))) for name,m in specs]))
            composer.compose(inputs,root/'composed')
            report=json.loads((root/'composed/closure.json').read_text());self.assertFalse(report['productionPromotion']);self.assertEqual(report['unexplainedOmissions'],[])
            catalog=json.loads((root/'composed/catalog.prepared.json').read_text());mods=catalog['games'][0]['modules']
            self.assertTrue(all(m['state']=='unavailable' for m in mods));self.assertFalse(mods[0]['rightsVerified']);self.assertEqual(mods[0]['id'],'vr-core');self.assertTrue(mods[0]['required'])
            core['files'].pop(0);(root/'core.json').write_text(json.dumps(core))
            with self.assertRaisesRegex(ValueError,'omissions'):composer.compose(inputs,root/'incomplete')

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
