"""Refresh only the installer EXE/docs in an existing, verified 1.1.0 bundle."""
import hashlib
import json
from pathlib import Path
import shutil
import zipfile

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "artifacts/installer-1.1.0"
OUTPUT = ROOT / "artifacts/installer-1.1.0-psvr2"
EXE = ROOT / "artifacts/psvr2-candidate/TrilexModInstaller.exe"
EXPECTED_EXE = "12d808669c4ef68f5082a885e8ec85e02f880a4b2bbae4ae2f40915c3ec77ad3"


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def verify_manifest(root):
    for line in (root / "SHA256SUMS.txt").read_text(encoding="utf-8-sig").splitlines():
        expected, name = line.split("  ", 1)
        path = (root / name).resolve()
        assert path.is_relative_to(root.resolve()), name
        assert digest(path) == expected.lower(), name


def manifest(root):
    files = sorted(p for p in root.rglob("*") if p.is_file() and p.name != "SHA256SUMS.txt")
    (root / "SHA256SUMS.txt").write_text("".join(
        f"{digest(p).upper()}  {p.relative_to(root).as_posix()}\n" for p in files), encoding="utf-8")


assert digest(EXE) == EXPECTED_EXE, "Candidate differs from tested executable"
verify_manifest(SOURCE / "bundle")
verify_manifest(SOURCE / "release-assets")
OUTPUT.mkdir()  # Retain earlier releases; refuse accidental reuse.
bundle = OUTPUT / "bundle"
public = OUTPUT / "release-assets"
shutil.copytree(SOURCE / "bundle", bundle)
shutil.copytree(SOURCE / "release-assets", public)
shutil.copyfile(EXE, bundle / "TrilexModInstaller.exe")
shutil.copyfile(ROOT / "docs/releases/installer-1.1.0.md", bundle / "docs/releases/installer-1.1.0.md")
shutil.copyfile(ROOT / "docs/PSVR2-TOOLKIT.md", bundle / "docs/PSVR2-TOOLKIT.md")
with (bundle / "RELEASE-NOTES.md").open("a", encoding="utf-8") as stream:
    stream.write("\n\n" + (ROOT / "docs/PSVR2-TOOLKIT.md").read_text(encoding="utf-8"))
for path in (SOURCE / "bundle/packages").rglob("*"):
    if path.is_file():
        assert digest(path) == digest(bundle / path.relative_to(SOURCE / "bundle"))
assert digest(bundle / "catalog.signed.json") == digest(SOURCE / "bundle/catalog.signed.json")
manifest(bundle)
archive = public / "Trilex-Mod-Installer-1.1.0-win-x64.zip"
with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED, compresslevel=6) as output:
    for path in sorted(bundle.rglob("*")):
        if path.is_file():
            output.write(path, path.relative_to(bundle).as_posix())
with zipfile.ZipFile(archive) as output:
    assert output.testzip() is None
    for item in output.infolist():
        with output.open(item) as stream:
            assert hashlib.file_digest(stream, "sha256").hexdigest() == digest(bundle / item.filename)
manifest(public)
verify_manifest(bundle)
verify_manifest(public)
(OUTPUT / "receipt.json").write_text(json.dumps({
    "exeSha256": digest(EXE), "zipSha256": digest(archive),
    "gamePackages": "unchanged, SHA256 verified", "zipContents": "all entries verified",
    "publicUrl": "https://github.com/trilex07/TrilexModInstaller/releases/download/installer-1.1.0/" + archive.name,
}, indent=2), encoding="utf-8")
print((OUTPUT / "receipt.json").read_text(encoding="utf-8"))
