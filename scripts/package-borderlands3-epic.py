"""Add the separately built Epic module to the released BL3 Steam catalog."""
import argparse
import base64
from copy import deepcopy
from hashlib import sha256
import json
from pathlib import Path
import subprocess
import zipfile

ROOT = Path(__file__).resolve().parents[1]
PACKAGE = "borderlands3-vr-epic-20261125.zip"
PREVIOUS_EPIC = "666aaea137fab4b1ec9f86d8cc28c438ed7cba9d27334bcadef405447c0e64b8"


def write_json(path, value):
    path.write_text(json.dumps(value, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def digest(data):
    return sha256(data).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for key in ("base", "steam-module", "epic-dll", "steam-exe", "epic-exe", "store-pins", "output"):
        parser.add_argument("--" + key, required=True, type=Path)
    parser.add_argument("--epic-dll-sha256", required=True)
    args = parser.parse_args()
    import pefile
    tool = ROOT / "src/Trilex.Installer.Tools/bin/Release/net10.0/Trilex.Installer.Tools.dll"
    subprocess.run(["dotnet", str(tool), "verify", str(args.base),
                    str(ROOT / "catalog/trusted-publisher.pem")], check=True)
    catalog = json.loads(base64.b64decode(json.loads(args.base.read_bytes())["payload"]))
    game = next(g for g in catalog["games"] if g["id"] == "borderlands3")
    if catalog["version"] != "1.1.14" or "buildProfiles" in game and game["buildProfiles"]:
        raise ValueError("Expected the released Steam-only catalog 1.1.14")
    steam = game["modules"][0]
    if digest(args.steam_module.read_bytes()).lower() != steam["archiveSha256"].lower():
        raise ValueError("Unexpected Steam module digest")
    dll = args.epic_dll.read_bytes()
    if digest(dll) != args.epic_dll_sha256.lower():
        raise ValueError("Unexpected Epic DLL digest")
    with pefile.PE(data=dll) as pe:
        if pe.FILE_HEADER.Machine != 0x8664 or not pe.is_dll():
            raise ValueError("Expected the native x64 mod proxy")
        names = {symbol.name for symbol in pe.DIRECTORY_ENTRY_EXPORT.symbols}
        if not {b"XInputGetState", b"XInputSetState", b"XInputGetCapabilities"} <= names:
            raise ValueError("Epic proxy is missing expected exports")

    pins = json.loads(args.store_pins.read_bytes())
    profiles = []
    exe_hashes = []
    for store, path in (("Steam", args.steam_exe), ("Epic", args.epic_exe)):
        data = path.read_bytes()
        row = pins[store]
        exe_hash = digest(data)
        if exe_hash != row["exe_sha256_provenance"]:
            raise ValueError("Unexpected " + store + " executable provenance")
        exe_hashes.append(exe_hash)
        probes = []
        with pefile.PE(data=data, fast_load=True) as pe:
            for pin in row["pins"]:
                rva = int(pin["rva"], 16)
                # The legacy installer hashes the ASCII hex representation.
                if digest(pe.get_data(rva, pin["size"]).hex().encode("ascii")) != pin["sha256"]:
                    raise ValueError(f"Existing {store} pin mismatch at {rva:#x}")
                witness = pe.get_data(rva, 32)
                if len(witness) != 32:
                    raise ValueError("Truncated code witness")
                probes.append(dict(rva=rva, length=len(witness), sha256=digest(witness)))
        profiles.append(dict(id=store.lower(), probes=probes, modules=[steam]))

    out = args.output.resolve()
    stage = out / "stage"
    stage.mkdir(parents=True)
    epic = deepcopy(steam)
    epic.update(labels={"en": "Borderlands 3 VR (Epic preview)", "pl": "Borderlands 3 VR (Epic, wersja testowa)"},
                version="2026.11.25-epic-preview",
                downloadUrl="https://github.com/trilex07/TrilexModInstaller/releases/download/installer-1.1.5/" + PACKAGE)
    loader = next(f for f in epic["files"] if f["path"].endswith("/xinput1_3.dll"))
    if digest(dll).lower() == loader["sha256"].lower():
        raise ValueError("Epic must have a separate store build")
    loader["sha256"] = digest(dll)
    loader["beforeSha256"] = PREVIOUS_EPIC
    with zipfile.ZipFile(args.steam_module) as archive:
        for file in epic["files"]:
            if file["kind"] != "copy":
                continue
            entry = file["entry"]
            body = dll if file is loader else archive.read(entry)
            if file["path"].endswith("/README-Borderlands3VR.txt"):
                body = (ROOT / "docs/BORDERLANDS3-VR.md").read_bytes()
                file["sha256"] = digest(body)
            target = stage / entry
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(body)
    manifest = out / "epic.module.json"
    write_json(manifest, epic)
    package = out / PACKAGE
    subprocess.run(["dotnet", str(tool), "pack", str(manifest), str(stage), str(package)], check=True)
    profiles[1]["modules"] = [json.loads(package.with_suffix(".zip.module.json").read_bytes())]
    game["buildProfiles"] = profiles
    game["exeSha256"] = exe_hashes
    catalog["version"] = "1.1.15"
    write_json(out / "catalog.unsigned.json", catalog)


if __name__ == "__main__":
    main()
