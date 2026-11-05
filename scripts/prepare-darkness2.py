"""Prepare unsigned Darkness II modules from reviewed inputs; no game writes or uploads."""
import argparse
import base64
import hashlib
import importlib.util
import json
from pathlib import Path
import shutil
import subprocess
import sys

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[1]
CORE = ("d3d9.dll", "Darkness2VR.ini", "Darkness2VR-diagnostic.ini", "openxr_loader.dll")
DLC = "Cache.DLC/PCPatchManifests/Cache.Windows/"
ASSETS = tuple(DLC+stem+ext for stem in ("H.Script","B.Font","H.Font") for ext in (".cache",".toc"))
CACHE = ("H.Misc.cache", "H.Misc.toc")

def sha(path):
    with path.open("rb") as f:
        return hashlib.file_digest(f, "sha256").hexdigest().upper()

def save(path, obj):
    path.write_text(json.dumps(obj, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")

def main():
    p = argparse.ArgumentParser(description=__doc__)
    for arg in ("catalog", "runtime", "vanilla-cache", "polish-languages", "english-languages", "menu-cache", "asset-originals", "asset-targets", "cache-tool", "output"):
        p.add_argument("--"+arg, type=Path, required=True)
    for arg in ("version", "core-url", "text-url", "menu-url", "assets-url", "core-review", "text-review", "assets-review", "exe-sha256", "runtime-sha256"):
        p.add_argument("--"+arg, required=True)
    a = p.parse_args()
    if len(a.exe_sha256)!=64 or any(c not in "0123456789abcdefABCDEF" for c in a.exe_sha256):
        p.error("Exact supported DarknessII.exe SHA-256 is required")
    out = a.output.resolve()
    if not out.is_relative_to(ROOT / "artifacts"):
        p.error("Use a fresh output directory under artifacts/")
    if sha(a.runtime/"d3d9.dll")!=a.runtime_sha256.upper():
        p.error("Runtime does not match the reviewed SHA-256")
    out.mkdir(parents=True)
    catalog = json.loads(a.catalog.read_text(encoding="utf-8-sig"))
    if "payload" in catalog:
        catalog = json.loads(base64.b64decode(catalog["payload"], validate=True))
    spec = importlib.util.spec_from_file_location("darkness_cache_tool", a.cache_tool)
    tool = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(tool)
    cache = tool.Cache(a.menu_cache, "H.Misc")
    keys = [name for name in cache.entries if Path(name).name=="Languages.cl"]
    if len(keys)!=1:
        raise ValueError("Cannot identify unique live Languages.cl")
    variants = {}
    for mid, language in (("polish-text", a.polish_languages), ("darkness-menu", a.english_languages)):
        variants[mid] = out/("private-"+mid)
        cache.write(variants[mid], {keys[0]: language.read_bytes()})
    tools = ROOT/"src/Trilex.Installer.Tools/bin/Release/net10.0/Trilex.Installer.Tools.dll"
    maker = ROOT/".build/recipe-maker/recipe-maker.exe"
    modules = []
    for mid, en, pl, required, url, evidence in (
        ("vr-core", "VR Core", "Rdzeń VR", True, a.core_url, a.core_review),
        ("darkness-assets", "VR fixes and fonts", "Poprawki VR i czcionek", False, a.assets_url, a.assets_review),
        ("darkness-menu", "VR menus and shrine fixes", "Menu VR i poprawki kapliczek", False, a.menu_url, a.assets_review),
        ("polish-text", "Polish subtitles and game text", "Polskie napisy i tekst gry", False, a.text_url, a.text_review),
    ):
        stage = out/(mid+"-stage")
        stage.mkdir()
        files = []
        if required:
            for name in CORE:
                source = a.runtime/name
                shutil.copyfile(source, stage/name)
                files.append(dict(path=name, entry=name, sha256=sha(stage/name), kind="copy",
                                  ownership="licensed" if name=="openxr_loader.dll" else "mod-authored"))
        else:
            pairs = ((path, a.asset_originals/path, a.asset_targets/path) for path in ASSETS) if mid=="darkness-assets" else (
                ("Cache.Windows/"+name, a.vanilla_cache/name, variants[mid]/name) for name in CACHE)
            for path, original, target in pairs:
                entry = Path(path).name+".svrp"
                subprocess.run([maker, original, target, stage/entry], check=True)
                files.append(dict(path=path, entry="", sha256=sha(target),
                                  beforeSha256=sha(original), kind="recipe", ownership="local-transform",
                                  recipe=[dict(source="instructions", offset=0, length=target.stat().st_size, entry=entry)]))
        module = dict(id=mid, labels=dict(en=en, pl=pl), version=a.version, state="stable", required=required,
                      dependencies=[] if required else ["vr-core"], downloadBytes=0, downloadUrl=url,
                      archiveSha256=None, expandedBytes=0, rightsVerified=False, rightsEvidence="",
                      publisherReview=dict(basis="fan-mod-publisher-approved", evidence=evidence,
                                           notice="Fan project; third-party rights remain reserved."),
                      files=files, reason="")
        manifest = out/(mid+".input.json")
        save(manifest,module)
        archive = out/"packages/darkness2"/(mid+".zip")
        subprocess.run(["dotnet",tools,"pack",manifest,stage,archive],cwd=ROOT,check=True)
        modules.append(json.loads(Path(str(archive)+".module.json").read_text(encoding="utf-8")))
    game = dict(id="darkness2", name="The Darkness II VR", state="preview", adapter="Darkness2GameAdapter",
                steamAppId="67370", executable="DarknessII.exe", exeSha256=[a.exe_sha256.upper()], modules=modules)
    catalog["games"] = [g for g in catalog["games"] if g["id"]!="darkness2"] + [game]
    catalog["version"] = a.version
    save(out/"catalog.unsigned.json", catalog)

if __name__=="__main__":
    main()
