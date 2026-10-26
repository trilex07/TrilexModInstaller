"""Replace literal audio recipes with fixed local DSP metadata; no audio is archived."""
import argparse
import hashlib
import json
import struct
import sys
import wave
import zipfile
from pathlib import Path
ROOT = Path(__file__).resolve().parents[1]


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def sha(data):
    return hashlib.sha256(data).hexdigest().upper()


def pcm_sha(path):
    with wave.open(str(path), 'rb') as w:
        return sha(w.readframes(w.getnframes()))


def prepare(native, report, audition, output, codec, verify=False):
    output = output.resolve()
    if output.exists() or not output.is_relative_to(ROOT/'artifacts'):
        raise ValueError('Fresh private artifacts output required')
    module = read(native/'audio-remaster/module.json')
    banks = {b['package']: b for b in read(report)['banks']}
    sys.path.insert(0, str(ROOT/'tools/asset-worker'))
    import audio_remaster
    stage = output/'stage'; stage.mkdir(parents=True)
    verification = []
    for f in module['files']:
        bank = banks[Path(f['path']).name]
        # Exact accepted-V7 identity: omit RvGame / TMD impulse, not part of V7.
        if f['sha256'].upper() != bank['candidate_sha256'].upper() or bank['id'] not in audio_remaster.PROFILES:
            raise ValueError('Unreviewed native remaster target')
        decoded = native/'decoded'/(Path(f['path']).stem+'.u')
        target_ogg = report.parent/(bank['id']+'.ogg')
        encoded = target_ogg.read_bytes()
        if sha(encoded) != bank['candidate_ogg_sha256'].upper():
            raise ValueError('Accepted private Ogg identity changed')
        serial = struct.unpack_from('<I', encoded, 14)[0]
        folder = audition/bank['id']
        op = dict(profile=bank['id'], sourceLogicalSha256=sha(decoded.read_bytes()),
                  payloadOffset=bank['payload_offset'], payloadBytes=bank['payload_bytes'],
                  sourceOggSha256=bank['source_ogg_sha256'].upper(),
                  sourcePcmSha256=pcm_sha(folder/'original.wav'),
                  masteredPcmSha256=pcm_sha(folder/'restored_conservative.wav'),
                  encodedSha256=sha(encoded), serial=serial, quality=bank['vorbis_quality'],
                  gainDb=bank['codec_headroom_gain_db'], frames=bank['pcm']['frames'],
                  channels=bank['pcm']['channels'], sampleRate=bank['pcm']['sample_rate'],
                  whole=f['sourceCodec']=='ue3-whole-lzo', logicalSha256=bank['candidate_logical_sha256'].upper(),
                  targetSha256=f['sha256'].upper())
        entry=bank['id']+'.json'; meta=stage/entry
        meta.write_text(json.dumps(op,indent=2)+'\n',encoding='utf-8')
        f['recipe']=[dict(source='audio',offset=0,length=bank['bytes'],entry=entry)]
        if verify:
            destination=output/(bank['id']+'.xxx')
            audio_remaster.generate(decoded,destination,meta,codec)
            verification.append(dict(path=f['path'],sha256=sha(destination.read_bytes()),status='EXACT_ACCEPTED_BYTES'))
            print('PASS '+f['path'],flush=True)
    archive=output/'audio-remaster-local.zip'
    with zipfile.ZipFile(archive,'x',zipfile.ZIP_DEFLATED) as z:
        for p in sorted(stage.iterdir()):
            info=zipfile.ZipInfo(p.name,(2026,1,1,0,0,0));info.compress_type=zipfile.ZIP_DEFLATED
            z.writestr(info,p.read_bytes())
    module.update(downloadBytes=archive.stat().st_size,archiveSha256=sha(archive.read_bytes()),
                  expandedBytes=sum(p.stat().st_size for p in stage.iterdir())+sum(x['length'] for f in module['files'] for x in f['recipe']),
                  rightsVerified=True,rightsEvidence='docs/distribution/LOCAL-AUDIO.md: authored fixed DSP metadata only; no recordings, cloned voices or original game bytes in ZIP',
                  reason='Local generation from exact owned sources; pinned optional codec required; public promotion remains separate')
    (output/'module.json').write_text(json.dumps(module,indent=2)+'\n')
    (output/'report.json').write_text(json.dumps(dict(files=verification,archivedMedia=False,steamAccess='NONE',
        codecSha256=sha(codec.read_bytes()) if verify else None,publicPromotion=False),indent=2)+'\n')


if __name__=='__main__':
    p=argparse.ArgumentParser()
    for name in ('native','report','audition','output','codec'):p.add_argument('--'+name,type=Path,required=True)
    p.add_argument('--verify',action='store_true')
    prepare(**vars(p.parse_args()))
