"""Prepare an UNSIGNED catalog with on-demand Drive URLs; retain package identities.

Run the normal publisher signing/verification step before distribution. No upload,
game writes, downloaded code, or change to package/source/output hashes occurs here.
"""
import argparse
import base64
import copy
import json
import re
from pathlib import Path
from urllib.parse import parse_qs, urlparse


def direct_url(link):
    uri = urlparse(link)
    if uri.scheme != "https" or uri.netloc not in ("drive.google.com", "drive.usercontent.google.com") or uri.fragment:
        raise ValueError("Expected an HTTPS Google Drive file link")
    query = parse_qs(uri.query)
    match = re.fullmatch(r"/file/d/([A-Za-z0-9_-]+)/view", uri.path)
    if match and uri.netloc == "drive.google.com":
        identity = match[1]
    elif uri.path in ("/download", "/open", "/uc") and len(query.get("id", [])) == 1:
        identity = query["id"][0]
    else:
        raise ValueError("Expected a single file, not a Drive folder")
    if not re.fullmatch(r"[A-Za-z0-9_-]+", identity):
        raise ValueError("Invalid Drive file ID")
    return f"https://drive.usercontent.google.com/download?id={identity}&export=download&confirm=t"


def update(catalog, dubbing, textures):
    result = copy.deepcopy(catalog)
    game, = [g for g in result["games"] if g["id"] == "singularity"]
    for identity, link in (("polish-dubbing", dubbing), ("hd-textures", textures)):
        module, = [m for m in game["modules"] if m["id"] == identity]
        if module["state"] != "stable" or module["required"] or not module["files"]:
            raise ValueError(f"{identity}: expected an optional, released module")
        if not re.fullmatch(r"[A-Fa-f0-9]{64}", module.get("archiveSha256", "")) or module["downloadBytes"] <= 0:
            raise ValueError(f"{identity}: exact existing archive identity required")
        module["downloadUrl"] = direct_url(link)
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--catalog", type=Path, required=True)
    parser.add_argument("--dubbing-url", required=True)
    parser.add_argument("--textures-url", required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    data = json.loads(args.catalog.read_text(encoding="utf-8-sig"))
    if "payload" in data:
        data = json.loads(base64.b64decode(data["payload"], validate=True))
    result = update(data, args.dubbing_url, args.textures_url)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open("x", encoding="utf-8") as stream:
        json.dump(result, stream, indent=2, ensure_ascii=False)
        stream.write("\n")
    print("UNSIGNED catalog prepared; sign and verify before release. Archive hashes unchanged.")


if __name__ == "__main__":
    main()
