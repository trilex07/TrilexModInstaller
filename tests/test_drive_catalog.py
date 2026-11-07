"""Offline publishing tests; no uploads, signing keys, or game data."""
import copy
import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location("drive_catalog", Path(__file__).resolve().parents[1] / "scripts/set-singularity-drive-packages.py")
drive = importlib.util.module_from_spec(spec)
spec.loader.exec_module(drive)


class DriveCatalogTests(unittest.TestCase):
    def test_only_two_download_urls_change(self):
        modules = [dict(id=i, state="stable", required=False, files=[dict(path=i)],
                        archiveSha256="A" * 64, downloadBytes=123, downloadUrl="original")
                   for i in ("polish-dubbing", "hd-textures", "vr-core")]
        original = dict(games=[dict(id="singularity", modules=modules), dict(id="darkness2", modules=[])])
        snapshot = copy.deepcopy(original)
        updated = drive.update(original, "https://drive.google.com/file/d/voice_1/view?usp=sharing",
                               "https://drive.google.com/open?id=textures_2")
        self.assertEqual(original, snapshot)
        self.assertIn("id=voice_1", updated["games"][0]["modules"][0]["downloadUrl"])
        for i in (0, 1):
            updated["games"][0]["modules"][i]["downloadUrl"] = "original"
        self.assertEqual(updated, original)

    def test_reject_folders_foreign_hosts_and_unreleased_modules(self):
        for link in ("http://drive.google.com/open?id=x", "https://drive.google.com.evil/open?id=x",
                     "https://drive.google.com/drive/folders/x", "https://drive.google.com/open?id=x&id=y",
                     "https://user@drive.google.com/open?id=x", "https://drive.google.com/open?id=../x"):
            with self.assertRaises(ValueError):
                drive.direct_url(link)
        for required in (False, True):
            module = dict(id="polish-dubbing", state="unavailable", required=required, files=[])
            with self.assertRaises(ValueError):
                drive.update(dict(games=[dict(id="singularity", modules=[module])]),
                             "https://drive.google.com/open?id=x", "https://drive.google.com/open?id=y")


if __name__ == "__main__":
    unittest.main()
