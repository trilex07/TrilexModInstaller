"""Repair UTF-8 text that was decoded as Windows-1250 (e.g. 'dĹşwiÄ™ku' -> 'dźwięku')."""
import json, re, sys
from pathlib import Path

MOJIBAKE = re.compile('[ĹÄĂÂ][\u0080-\u017f\u2018-\u203a]')

def repair(value):
    if isinstance(value, dict):
        return {k: repair(v) for k, v in value.items()}
    if isinstance(value, list):
        return [repair(v) for v in value]
    if isinstance(value, str):
        while MOJIBAKE.search(value):
            fixed = value.encode('cp1250').decode('utf-8')
            if fixed == value:
                break
            value = fixed
    return value

def main(source, target, version=None):
    data = json.loads(Path(source).read_text(encoding='utf-8-sig'))
    fixed = repair(data)
    if version:
        fixed['version'] = version
    text = json.dumps(fixed, ensure_ascii=False, indent=2) + '\n'
    if MOJIBAKE.search(text):
        raise SystemExit('mojibake remains after repair')
    Path(target).parent.mkdir(parents=True, exist_ok=True)
    Path(target).write_text(text, encoding='utf-8')
    print(f'{target}: {sum(a != b for a, b in zip(json.dumps(data, ensure_ascii=False), json.dumps(fixed, ensure_ascii=False)))} chars changed')

if __name__ == '__main__':
    main(*sys.argv[1:])
