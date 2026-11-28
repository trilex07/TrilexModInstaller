"""Stage the verified Steam-only BL3VR module and extend the installer catalog."""
import argparse
import base64
import configparser
import hashlib
import json
from pathlib import Path
import subprocess

BIN = "OakGame/Binaries/Win64/"
PACKAGE = "borderlands3-vr-steam-20261125.zip"
DLL = "8834f8effa231cfa442e52fce92a221830d82c81034038412ed986da7d9925de"
INI = "30cb3f001645c4b6bdb414345197d91989b94460c4f534e5f48af8e0d4781f2c"
LOADER = "66946ea6e9cc2649e299ec412521c5eafe56c635d05055097fe4db81e11932df"
PREVIOUS = "1a6c9bdd1a7b8b56b11e8a315b9549886e647e7b4ff508ad056f1d3f5f704e05"
EXE = "923afd263631681aff88037add504bf353fc6f4caa9045cfdb709e049fe4f101"


def dump(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def digest(data):
    return hashlib.sha256(data).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for key in ("mod", "profile", "loader", "txfg-license", "base", "output"):
        parser.add_argument("--" + key, required=True, type=Path)
    args = parser.parse_args()
    dll = args.mod / "build/mechanics-tracking-20261124/Release/xinput1_3.dll"
    for path, expected in ((dll, DLL), (args.profile, INI), (args.loader, LOADER)):
        if digest(path.read_bytes()) != expected:
            raise ValueError(f"Unexpected SHA-256: {path}")
    catalog = json.loads(base64.b64decode(json.loads(args.base.read_bytes())["payload"]))
    if catalog["version"] != "1.1.13" or any(g["id"] == "borderlands3" for g in catalog["games"]):
        raise ValueError("Expected the 1.1.13 base catalog")

    out = args.output.resolve()
    stage = out / "stage"
    stage.mkdir(parents=True)
    files = []

    def add(relative, data, ownership, before=None):
        entry = "runtime/" + relative
        target = stage / entry
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)
        row = dict(path=BIN + relative, entry=entry, sha256=digest(data), kind="copy", ownership=ownership)
        if before:
            row["beforeSha256"] = before
        files.append(row)

    add("xinput1_3.dll", dll.read_bytes(), "mod-authored", PREVIOUS)
    add("BL3VR/openxr_loader.dll", args.loader.read_bytes(), "licensed")
    add("BL3VR/LICENSE-OpenXR.txt", (args.mod / "third_party/openxr/LICENSE.txt").read_bytes(), "licensed")
    add("BL3VR/LICENSE-MinHook.txt", (args.mod / "third_party/minhook/LICENSE.txt").read_bytes(), "licensed")
    add("BL3VR/LICENSE-TXFG.txt", args.txfg_license.read_bytes(), "licensed")
    add("BL3VR/README-Borderlands3VR.txt",
        (Path(__file__).resolve().parents[1] / "docs/BORDERLANDS3-VR.md").read_bytes(), "mod-authored")
    for art in sorted((args.mod / "assets/vrui").glob("*.png")):
        add("vrui/" + art.name, art.read_bytes(), "mod-authored")

    cfg = configparser.ConfigParser(interpolation=None)
    cfg.optionxform = str
    cfg.read(args.profile, encoding="utf-8-sig")
    values = {f"{section}/{key}": value for section in ("Experimental", "Grips")
              if cfg.has_section(section) for key, value in cfg[section].items()}
    files.append(dict(path=BIN + "Borderlands3VR.ini", entry="", sha256="", kind="ini",
                      ownership="mod-authored", iniValues=values))
    module = dict(id="vr-core", labels={"en": "Borderlands 3 VR (Steam)", "pl": "Borderlands 3 VR (Steam)"},
                  version="2026.11.25", state="stable", required=True, dependencies=[],
                  downloadUrl="https://github.com/trilex07/TrilexModInstaller/releases/download/installer-1.1.5/" + PACKAGE,
                  downloadBytes=0, archiveSha256="", expandedBytes=0, rightsVerified=True,
                  rightsEvidence="Original Trilex proxy and panel art. OpenXR, MinHook and TXFG license texts included.",
                  files=files, reason="")
    dump(out / "module.json", module)
    subprocess.run(["dotnet", "run", "--project", "src/Trilex.Installer.Tools", "-c", "Release", "--",
                    "pack", str(out / "module.json"), str(stage), str(out / PACKAGE)],
                   cwd=Path(__file__).resolve().parents[1], check=True)
    catalog["version"] = "1.1.14"
    catalog["games"].append(dict(id="borderlands3", name="Borderlands 3 VR", state="stable",
                                 adapter="Borderlands3GameAdapter", steamAppId="397540",
                                 executable=BIN + "Borderlands3.exe", exeSha256=[EXE],
                                 modules=[json.loads((out / (PACKAGE + ".module.json")).read_bytes())]))
    dump(out / "catalog.unsigned.json", catalog)


if __name__ == "__main__":
    main()
