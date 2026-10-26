"""Fixed DSP from the owned native wave; never ships recordings or voice models."""
import hashlib
import json
import re
import struct
import subprocess
import wave
from pathlib import Path
from audio_profiles import PROFILES
from vorbis_fixed_allocation import pages, crc, pad_to_allocation

CODECS = {'3256173F3F8BFFD7DF12227C68ADF68025EDB1832273A9530688A7BB1ED8EDEC',
          '589E50B766D251AFDF181DD664D40BD94407E200B019989FD7468C7D118A28D0'}


def sha(data):
    return hashlib.sha256(data).hexdigest().upper()


def run(codec, args):
    result = subprocess.run([str(codec), '-nostdin', *map(str, args)],
                            capture_output=True, timeout=90, creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0))
    if result.returncode:
        raise ValueError('Fixed audio operation failed: '+result.stderr[-1000:].decode(errors='replace'))
    return result


def pcm(path):
    with wave.open(str(path), 'rb') as w:
        if w.getsampwidth() != 2 or w.getnchannels() not in (1, 2, 6) or w.getnframes() > 20_000_000:
            raise ValueError('Unsupported PCM format')
        return (w.getnframes(), w.getnchannels(), w.getframerate()), w.readframes(w.getnframes())


def true_peak(codec, path):
    result = run(codec, ['-nostats', '-i', path, '-filter_complex', 'ebur128=peak=true', '-f', 'null', '-'])
    hits = re.findall(rb'^\s*Peak:\s*(-?[0-9]+(?:\.[0-9]+)?)\s+dBFS', result.stderr, re.M)
    if not hits:
        raise ValueError('True peak measurement missing; refusing silent fallback')
    return float(hits[-1])


def master(codec, original, output, profile):
    import numpy as np
    info, _ = pcm(original)
    floating = output.with_suffix('.float.wav')
    run(codec, ['-v', 'error', '-n', '-i', original, '-af', ','.join(PROFILES[profile]), '-c:a', 'pcm_f32le', floating])
    raw = run(codec, ['-v', 'error', '-i', floating, '-f', 'f32le', '-']).stdout
    samples = np.frombuffer(raw, dtype='<f4').copy().reshape(-1, info[1]).astype(np.float64)
    if len(samples) != info[0] or not np.isfinite(samples).all():
        raise ValueError('DSP frame/finite contract failed')
    peak = true_peak(codec, floating)
    if peak > -1.0:
        samples *= 10 ** ((-1.0-peak)/20)
    ceiling = 10 ** (-1.0/20)
    threshold = .70 * ceiling
    delta = ceiling-threshold
    for ch in range(info[1]):
        sig = samples[:, ch]
        mask = np.abs(sig) > threshold
        sig[mask] = np.sign(sig[mask])*(threshold+delta*np.tanh((np.abs(sig[mask])-threshold)/delta))
    def write():
        data = np.clip(np.round(samples*32767), -32768, 32767).astype('<i2').tobytes()
        with wave.open(str(output), 'wb') as w:
            w.setnchannels(info[1]); w.setsampwidth(2); w.setframerate(info[2]); w.writeframes(data)
    write()
    peak = true_peak(codec, output)
    if peak > -1.0:
        samples *= 10 ** ((-1.0-peak-.05)/20)
        write()
    if true_peak(codec, output) > -1.0:
        raise ValueError('True peak ceiling not met')


def canonical_serial(data, serial):
    result = []
    for page in pages(data):
        q = bytearray(page)
        struct.pack_into('<I', q, 14, serial)
        struct.pack_into('<I', q, 22, 0)
        struct.pack_into('<I', q, 22, crc(q))
        result.append(q)
    return b''.join(result)


def literal_block(data):
    if len(data) < 19:
        return data
    remaining = len(data)-18
    prefix = bytearray([0])
    while remaining > 255:
        prefix.append(0); remaining -= 255
    prefix.append(remaining)
    return bytes(prefix)+data+b'\x11\0\0'


def pack(logical, whole):
    body = logical if whole else logical[0x71:]
    raw = [body[p:p+0x20000] for p in range(0, len(body), 0x20000)]
    chunks = [literal_block(p) for p in raw]
    container = struct.pack('<4I', 0x9E2A83C1, 0x20000, sum(map(len, chunks)), len(body))
    container += b''.join(struct.pack('<2I', len(c), len(r)) for c, r in zip(chunks, raw))
    container += b''.join(chunks)
    if whole:
        return container
    if len(logical) < 0x71:
        raise ValueError('Truncated native header')
    header = bytearray(logical[:0x71]); struct.pack_into('<2I', header, 0x61, 2, 1)
    result = header[:0x69]+struct.pack('<4I', 0x71, len(body), 0x81, len(container))+header[0x69:]+container
    return bytes(result)+b'\0'*((-len(result)) % 65536)


def generate(source, output, metadata, codec):
    if sha(codec.read_bytes()) not in CODECS:
        raise ValueError('Audio codec identity not approved')
    if output.exists() or source.stat().st_size > 256*1024*1024 or metadata.stat().st_size > 16384:
        raise ValueError('Fresh bounded audio output required')
    op = json.loads(metadata.read_text(encoding='utf-8'))
    required = {'profile', 'sourceLogicalSha256', 'payloadOffset', 'payloadBytes', 'sourceOggSha256',
                'sourcePcmSha256', 'masteredPcmSha256', 'encodedSha256', 'serial', 'quality', 'gainDb',
                'frames', 'channels', 'sampleRate', 'whole', 'logicalSha256', 'targetSha256'}
    if set(op) != required or op['profile'] not in PROFILES:
        raise ValueError('Unsupported audio metadata')
    if op['quality'] not in (6, 5, 4, 3, 2.5, 2, 1.5, 1, .5, 0, -.5, -1) or op['gainDb'] not in (0, -1, -2, -3):
        raise ValueError('Unsupported audio codec settings')
    if type(op['serial']) is not int or not 0 <= op['serial'] <= 0xffffffff or type(op['whole']) is not bool:
        raise ValueError('Invalid audio container metadata')
    logical = source.read_bytes()
    at, size = op['payloadOffset'], op['payloadBytes']
    if type(at) is not int or type(size) is not int or at < 0 or size < 64 or size > 16*1024*1024 or at > len(logical)-size:
        raise ValueError('Invalid native audio range')
    original = logical[at:at+size]
    if sha(logical) != op['sourceLogicalSha256'] or sha(original) != op['sourceOggSha256']:
        raise ValueError('Owned audio source changed')
    folder = output.parent / ('audio-'+output.stem)
    folder.mkdir(exist_ok=False)
    ogg = folder/'original.ogg'; ogg.write_bytes(original)
    wav = folder/'original.wav'; mastered = folder/'mastered.wav'; encoded = folder/'encoded.ogg'
    run(codec, ['-v', 'error', '-n', '-i', ogg, '-c:a', 'pcm_s16le', wav])
    expected = (op['frames'], op['channels'], op['sampleRate'])
    info, data = pcm(wav)
    if info != expected or sha(data) != op['sourcePcmSha256']:
        raise ValueError('Native decoded PCM changed')
    master(codec, wav, mastered, op['profile'])
    info, data = pcm(mastered)
    if info != expected or sha(data) != op['masteredPcmSha256']:
        raise ValueError('Mastered PCM differs from accepted recipe')
    args = ['-v', 'error', '-n', '-i', mastered, '-map_metadata', '-1']
    if op['gainDb']:
        args += ['-af', 'volume='+str(op['gainDb'])+'dB']
    run(codec, args+['-c:a', 'libvorbis', '-q:a', str(op['quality']), encoded])
    replacement = pad_to_allocation(canonical_serial(encoded.read_bytes(), op['serial']), size)
    if sha(replacement) != op['encodedSha256']:
        raise ValueError('Encoded audio differs from accepted recipe')
    revised = logical[:at]+replacement+logical[at+size:]
    result = pack(revised, op['whole'])
    if sha(revised) != op['logicalSha256'] or sha(result) != op['targetSha256']:
        raise ValueError('Native remaster output identity mismatch')
    with output.open('xb') as stream:
        stream.write(result)
