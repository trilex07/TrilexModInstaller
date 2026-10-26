"""Complete reviewed V7 module inputs from a pinned, read-only working backup.

Copy only listed remastered DDS/metadata and the source-owned trigger helper.
Movie/splash slots use exact-original local reconstruction, never replacement
of unrecognized third-party files. Existing generated hand/weapon recipes stay.
"""
import argparse
import importlib.util
import json
import shutil
from pathlib import Path

from runtime_resources import ROOT, inventory, require_closure, sha, validate_catalog, validate_dds

_spec = importlib.util.spec_from_file_location('owned_deltas', ROOT / 'scripts/prepare-v7-deltas.py')
_deltas = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(_deltas)


def prepare(inputs, accepted_root, output):
    accepted_root = accepted_root.resolve()
    output = output.resolve()
    if output.exists() or not output.is_relative_to(ROOT / 'artifacts'):
        raise ValueError('Fresh artifacts output required')
    spec = inventory()
    # Fail before staging if any identity has drifted. No scan/copy of arbitrary
    # backup files, audio sources, extracted GLBs, logs or .orig files.
    for row in spec['files']:
        path = accepted_root / row['path']
        if sha(path) != row['sha256']:
            raise ValueError('Accepted runtime source changed: ' + row['path'])
        if row['path'].endswith('weapon_wheel_catalog.json'):
            validate_catalog(path.read_bytes())
        if path.suffix.lower() == '.dds':
            validate_dds(path)
        if row.get('original') and sha(accepted_root / row['original']) != row['beforeSha256']:
            raise ValueError('Original runtime source changed: ' + row['path'])
    graph = json.loads(inputs.read_text(encoding='utf-8-sig'))
    output.mkdir(parents=True)
    evidence = []
    modules = []
    for item in graph:
        module = json.loads((ROOT / item['module']).read_text(encoding='utf-8-sig'))
        module_id = 'vr-core' if module['id'] == 'runtime-code' else module['id']
        additions = [row for row in spec['files'] if row['module'] == module_id]
        if not additions:
            modules.append(dict(module, id=module_id))
            continue
        folder = output / module_id
        stage = folder / 'stage'
        shutil.copytree(ROOT / item['stage'], stage)
        existing = {file['path'].lower(): file for file in module['files']}
        for row in additions:
            old = existing.get(row['path'].lower())
            if old:
                if old['sha256'] != row['sha256']:
                    raise ValueError('Existing module differs from accepted backup: ' + row['path'])
                evidence.append(dict(path=row['path'], route='existing recipe retained', sha256=row['sha256']))
                continue
            source = accepted_root / row['path']
            if row.get('original'):
                # Multimedia byte patterns can exceed the copy/literal maker's
                # instruction cap. Reuse the bounded streaming XOR producer,
                # which also independently verifies a complete round trip.
                recipe = _deltas.delta(accepted_root / row['original'], source, stage,
                                       'runtime-recipes/' + row['path'])
                file = dict(path=row['path'], entry='', sha256=row['sha256'], kind='recipe',
                            ownership='local-transform', beforeSha256=row['beforeSha256'],
                            recipe=recipe)
            else:
                target = stage / row['path']
                target.parent.mkdir(parents=True, exist_ok=True)
                shutil.copyfile(source, target)
                if sha(target) != row['sha256']:
                    raise ValueError('Source changed while copying')
                file = dict(path=row['path'], entry=row['path'], sha256=row['sha256'], kind='copy',
                            ownership='mod-authored')
            module['files'].append(file)
            evidence.append(dict(path=row['path'], route=file['kind'], sha256=row['sha256']))
        module['expandedBytes'] = sum(p.stat().st_size for p in stage.rglob('*') if p.is_file())
        (folder / 'module.json').write_text(json.dumps(module, indent=2) + '\n')
        item.update(module=str((folder / 'module.json').relative_to(ROOT)),
                    stage=str(stage.relative_to(ROOT)))
        modules.append(dict(module, id=module_id))
    count = require_closure(modules)
    (output / 'inputs.json').write_text(json.dumps(graph, indent=2) + '\n')
    (output / 'evidence.json').write_text(json.dumps(dict(
        schema=spec['schema'], verifiedResources=count, sources=str(accepted_root),
        runtimeSource=spec['runtimeSource'], files=evidence, steamWrites=False,
        gameLaunched=False, physicalAcceptance='PENDING',
        note='Fan-mod publisher decision preserved; no fabricated third-party license'), indent=2) + '\n')
    print('PASS accepted runtime-resource staging:', count, 'pinned dependencies')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    for key in ('inputs', 'accepted-root', 'output'):
        parser.add_argument('--' + key, type=Path, required=True)
    prepare(**vars(parser.parse_args()))
