"""Fail CI before publishing private keys, binaries or extracted game data."""
import re, subprocess
from pathlib import Path

files=subprocess.check_output(['git','ls-files','-z']).decode().split('\0')
for name in filter(None,files):
    path=Path(name)
    if path.parts[0] in ('private','artifacts','.build') or path.suffix.lower() in {'.pfx','.key','.dll','.exe','.xxx','.psk','.psa','.dds','.wav','.bik','.mp3','.u','.svtg','.svnc','.svni','.svrt','.svrg'}:
        raise SystemExit('Forbidden distributable source entry: '+name)
    if path.suffix.lower() not in {'.png','.ico'}:
        data=path.read_bytes()
        if re.search(rb'-----BEGIN (?:EC |RSA |OPENSSH |ENCRYPTED )?PRIVATE KEY-----',data):
            raise SystemExit('Private key detected: '+name)
print('PASS tracked-file source audit: no game payload, executable or private signing key')
