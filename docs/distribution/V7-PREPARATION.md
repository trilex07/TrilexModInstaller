# V7 package preparation — 2026-10-26

The accepted runtime is frozen at `38ca72f88112bfcbd26addb0d6aa4f76095aa15e`. Nothing was deployed to Steam or launched.

## Glove provenance finding

The accepted INI enables `GloveNativeEnabled=1`, `GloveIsolate=2`, native cuff closure and the short TMD gauntlet. In that source, `d3d9.cpp` loads native indices/materials in the native branch (10459–10472); GLB loading is in the alternative branch. CustomGlovesEligible also requires native mode to be disabled; DrawCustomGloves returns when native mode is active. This establishes runtime selection, not a license for extracted original geometry.

Omit the three inactive `glove-animation/glove_left.svrg`, `glove_right.svrg`, and `glove.svrt` files. The PolyOne reference pack is unnecessary and must not be redistributed. Active SVNI/SVNC/SVTG and knife caches need reproducible local generation from the player's own game. Do not relabel native game material as MIT.

## Prepared and tested

- Exact accepted renderer and texture bridge plus Apache-2.0 OpenXR x86 loader, with upstream MIT/BSD/Apache/NVIDIA notices. Source attribution remains intact.
- DirectInput's x86 Windows proxy is copied locally from the fixed Windows SystemX86 directory, never included in the download. Its exact source hash is mandatory; this candidate is host-version specific, not universal Windows compatibility.
- Actual code archive passed fixture install, repeat install, corruption detection, repair and uninstall. DLLs were copied as data, never loaded.
- 124 native package upgrade recipes: 116 dubbing banks, 7 audio packages, 1 UI package. Python producer and C# consumer independently reconstructed every final accepted SHA256.
- 44 core regressions and 3 producer tests PASS. Producer subcases include empty/growing/shrinking/multichunk deltas and normal/delay PE imports.

## Scope and remaining gates

These native recipes require the exact pre-V7 backup hashes. They are not a clean-game installation recipe. The separate asset-local cooked snapshot has all inputs, but 115 differ from the upgrade baseline; provenance and first-install reconstruction must be established explicitly. XOR encoding does not create redistribution rights. Inserted voice/texture/media provenance remains a separate release gate.

No complete playable catalog was activated. Seven bootstrap modules stay unavailable until local cache generation, portable configuration, language combinations and complete file/dependency closure are integrated. The original 164-pin inventory omitted the OpenXR loader and x86 Visual C++ prerequisite; these are now identified, not silently assumed present. Visual C++ x64 alone is insufficient for the x86 runtime.

Do not call the installer complete, clean-Windows tested, physically accepted or signed on this evidence. A production Authenticode certificate is absent; unsigned RC preparation can continue. No public native-media upload occurred.

## Reproduce

Run `scripts/build.ps1 -Publish` and `python tests/test_package_producers.py` for public fixtures. Private preparation:

```powershell
python scripts/prepare-v7.py --source ../SingularityVR-final-completion --sdk ../dependencies/openxr-local --output artifacts/FRESH_CODE
python scripts/prepare-v7-deltas.py --source ../SingularityVR-final-completion --output artifacts/FRESH_DELTAS
```

The tools require fresh output directories, retain source files unchanged and never search or write Steam. Review emitted manifests before publication. `exercise` refuses any directory without its synthetic fixture EXE; `verify-recipes` writes only under fresh installer artifacts directories.

## Local owned-source reconstruction implementation

The bundled standalone GPL-2.0-or-later decoder now handles bounded UE3 whole-file and logical LZO containers. Compressed input, decoded output and final reconstructed file each require an exact SHA256. Fixed copy/literal instruction tapes run in the installer without arbitrary script execution. Long data paths are supported; the embedded verified helper uses a short ancestor cache because Windows CreateProcess rejects long executable paths.

Private first-install preparation against `SingularityVR-assets-local/cooked` reconstructed **all 124 accepted final package hashes independently in the C# consumer**: 116 dubbing, 7 audio, 1 UI. This proves that exact local snapshot, not compatibility with every retail build or permission for inserted media. No originals were archived or uploaded. Source snapshot files were copied only to disposable verification fixtures and left unchanged.

47 core regression tests PASS, including padded UE3 input, cross-file reconstruction, cache tampering, binary copy/literal output, malformed opcodes/ranges/truncation/trailing bytes and transactional original restoration. Build the decoder first with `scripts/build-decoder.ps1`; the offline recipe producer with `scripts/build-recipe-maker.ps1`. Prepare recipes with `scripts/prepare-v7-native.py --source ../SingularityVR-final-completion --cooked ../SingularityVR-assets-local/cooked --output artifacts/FRESH_NATIVE`.

Remaining release gates above still apply: native glove/knife caches, HD image generation, portable settings and language combinations, complete catalog closure, creative-media provenance and human/clean-Windows verification. This package reconstruction milestone does not enable unavailable modules.
