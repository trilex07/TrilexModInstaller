"""Repack verified Darkness II release recipes without rebuilding game data."""
import argparse
import base64
import hashlib
import json
from pathlib import Path
import subprocess
import zipfile


def save(path, data):
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("signed", "packages", "tools", "output"):
        parser.add_argument(name, type=Path)
    a = parser.parse_args()
    a.output.mkdir()
    catalog = json.loads(base64.b64decode(json.loads(a.signed.read_text(encoding="utf-8-sig"))["payload"], validate=True))
    game = next(g for g in catalog["games"] if g["id"] == "darkness2")
    modules = {m["id"]: m for m in game["modules"]}
    assets, polish = (modules[mid] for mid in ("darkness-assets", "polish-text"))
    fonts = [f for f in assets["files"] if Path(f["path"]).name in ("B.Font.cache", "B.Font.toc", "H.Font.cache", "H.Font.toc")]
    if len(fonts) != 4 or len(assets["files"]) != 6 or len(polish["files"]) != 2:
        raise ValueError("Expected the combined legacy Darkness II release")
    stage = a.output / "stage"
    stage.mkdir()
    seen = set()
    for module in (assets, polish):
        source = a.packages / (module["id"] + ".zip")
        with source.open("rb") as stream:
            if hashlib.file_digest(stream, "sha256").hexdigest().lower() != module["archiveSha256"].lower():
                raise ValueError(f"Source archive mismatch: {source}")
        with zipfile.ZipFile(source) as archive:
            for file in module["files"]:
                for part in file["recipe"]:
                    entry = part["entry"]
                    if part["source"] != "instructions" or not entry.endswith(".svrp") or Path(entry).name != entry or entry in seen:
                        raise ValueError(f"Unexpected recipe entry: {entry}")
                    seen.add(entry)
                    (stage / entry).write_bytes(archive.read(entry))
    assets["files"] = [f for f in assets["files"] if f not in fonts]
    polish["files"] += fonts
    assets["labels"] = {"en": "VR fixes", "pl": "Poprawki VR"}
    polish["labels"] = {"en": "Polish game text and fonts", "pl": "Polski tekst gry i czcionki"}
    for module in (assets, polish):
        mid = module["id"]
        manifest = a.output / (mid + ".input.json")
        archive = a.output / (mid + ".zip")
        save(manifest, module)
        subprocess.run(["dotnet", str(a.tools), "pack", str(manifest), str(stage), str(archive)], check=True)
        module.update(json.loads(Path(str(archive) + ".module.json").read_text(encoding="utf-8")))
    save(a.output / "catalog.unsigned.json", catalog)


if __name__ == "__main__":
    main()
