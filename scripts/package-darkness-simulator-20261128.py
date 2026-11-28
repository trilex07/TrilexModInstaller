"""Release the simulator-verified Darkness II runtime with the existing 1.1.5 packages."""

import argparse
import base64
import hashlib
import importlib.util
import json
from pathlib import Path
import subprocess
import zipfile


CATALOG_VERSION = "1.1.33"
CORE_VERSION = "1.1.5-20261128-darkness-simulator"
DLL_SHA = "55BB00D09AD16D11752DF0178A17B10330FDF5135C9B03C3E9BCB4199106C833"
BASE_CATALOG_SHA = "647ACA65533EDB833DFF3900A6E8947C0588D108E028B1D6B9B3E53AE219E988"
BASE_BUNDLE_SHA = "B15F99D5C914EBA4801A4AAF874FFA7B4711F7C2DE30BECEEE2656F3C8E1BBF1"
CORE_ASSET = "darkness2-vr-core-20261128.zip"
CORE_ENTRY = "packages/darkness2/vr-core.zip"
CATALOG_ENTRY = "catalog.signed.json"

spec = importlib.util.spec_from_file_location(
    "darkness_runtime", Path(__file__).with_name("refresh-darkness-runtime.py")
)
runtime = importlib.util.module_from_spec(spec)
spec.loader.exec_module(runtime)


def signed_catalog(tool, path):
    key = Path(__file__).resolve().parents[1] / "catalog/trusted-publisher.pem"
    subprocess.run([str(tool.resolve()), "verify", str(path), str(key)], check=True)
    envelope = json.loads(path.read_bytes())
    return json.loads(base64.b64decode(envelope["payload"], validate=True))


def stage(args, original):
    previous = args.catalog.read_bytes()
    if runtime.sha(previous) != BASE_CATALOG_SHA or original["version"] != "1.1.32":
        raise ValueError("Unexpected source catalog")
    with zipfile.ZipFile(args.base) as bundle:
        if bundle.read(CATALOG_ENTRY) != previous:
            raise ValueError("Bundle and source catalog differ")
        old_core = bundle.read(CORE_ENTRY)
    updated, packed = runtime.refresh(original, old_core, args.dll.read_bytes(), DLL_SHA)
    core = next(module for game in updated["games"] if game["id"] == "darkness2"
                for module in game["modules"] if module["id"] == "vr-core")
    core.update(version=CORE_VERSION, downloadUrl=(
        "https://github.com/trilex07/TrilexModInstaller/releases/download/installer-1.1.5/"
        + CORE_ASSET))
    updated["version"] = CATALOG_VERSION
    args.output.mkdir(exist_ok=False)
    (args.output / CORE_ASSET).write_bytes(packed)
    (args.output / "catalog.unsigned.json").write_text(
        json.dumps(updated, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )


def assemble(args, catalog):
    if catalog["version"] != CATALOG_VERSION:
        raise ValueError("Unexpected replacement catalog version")
    core = next(module for game in catalog["games"] if game["id"] == "darkness2"
                for module in game["modules"] if module["id"] == "vr-core")
    packed = args.core.read_bytes()
    if len(packed) != core["downloadBytes"] or runtime.sha(packed) != core["archiveSha256"]:
        raise ValueError("Bundle core differs from signed hash")
    with zipfile.ZipFile(args.core) as package:
        if runtime.sha(package.read("d3d9.dll")) != DLL_SHA:
            raise ValueError("Bundle core differs from the tested runtime")
    replacements = {
        CORE_ENTRY: packed,
        CATALOG_ENTRY: args.catalog.read_bytes(),
        "TrilexModInstaller.exe": args.installer.read_bytes(),
    }
    notice = args.notes.read_bytes() + b"\n\n"
    with zipfile.ZipFile(args.base) as old, zipfile.ZipFile(args.output, "x") as new:
        if runtime.sha(old.read(CATALOG_ENTRY)) != BASE_CATALOG_SHA:
            raise ValueError("Unexpected catalog inside source bundle")
        names = old.namelist()
        if len(names) != len(set(names)) or not replacements.keys() <= set(names):
            raise ValueError("Unexpected source bundle entries")
        sums = {}
        for info in old.infolist():
            name = info.filename
            if name == "SHA256SUMS.txt":
                continue
            data = replacements[name] if name in replacements else old.read(name)
            if name in ("README.md", "RELEASE-NOTES.md"):
                data = notice + data
            sums[name] = runtime.sha(data)
            new.writestr(info, data)
        checksum_manifest = "".join(f"{digest}  {name}\n" for name, digest in sorted(sums.items()))
        new.writestr("SHA256SUMS.txt", checksum_manifest, compress_type=zipfile.ZIP_DEFLATED)
    with zipfile.ZipFile(args.output) as new:
        if set(new.namelist()) != set(names) or new.read("SHA256SUMS.txt").decode() != checksum_manifest:
            raise ValueError("Output archive names or checksums differ")
        for name, digest in sums.items():
            if runtime.sha(new.read(name)) != digest:
                raise ValueError(f"Output checksum mismatch: {name}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("step", choices=("stage", "assemble"))
    for name in ("tools", "base", "catalog", "output"):
        parser.add_argument(f"--{name}", type=Path, required=True)
    for name in ("dll", "core", "installer", "notes"):
        parser.add_argument(f"--{name}", type=Path)
    args = parser.parse_args()
    required = ("dll",) if args.step == "stage" else ("core", "installer", "notes")
    if any(getattr(args, name) is None for name in required):
        parser.error(f"{args.step} requires " + ", ".join(f"--{name}" for name in required))
    with args.base.open("rb") as bundle:
        if hashlib.file_digest(bundle, "sha256").hexdigest().upper() != BASE_BUNDLE_SHA:
            raise ValueError("Unexpected source bundle bytes")
    (stage if args.step == "stage" else assemble)(args, signed_catalog(args.tools, args.catalog))


if __name__ == "__main__":
    main()
