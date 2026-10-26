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

### Native hand cache closure

`scripts/prepare-native-hand-recipes.py` proves original PSK index streams occur uniquely in the exact decoded original core, then emits COPY operations for every retained original triangle. Only mod headers, generated cap triangles and authored finger-pose metadata are literals. The archive need not contain the original index streams or PSK exports. Python and the C# recipe consumer independently reproduced exact accepted hashes for SVNI (3694 original + 98 cap faces), SVNC (8360 original + 76 cap faces) and SVTG (7087 original + 23 cap faces). Source and decoded core identities remain mandatory. No PolyOne geometry is used.

Private reproduction (inputs stay outside Git):

```powershell
python scripts/prepare-native-hand-recipes.py --source ../SingularityVR-final-completion --decoded artifacts/decoded-original-RvGame.u --original ../SingularityVR-assets-local/cooked/RvGame.xxx --psks ../research/native-hands-20261007/assets/RvGame/SkeletalMesh3 --output artifacts/FRESH_HAND_RECIPES
```

`verify-recipes` now safely supports an explicit cross-file source under the supplied original root, retaining preimage/decoded/final SHA checks. It never resolves an arbitrary system source. Native hand recipe validation is offline byte evidence, not another headset test.

### Local image and knife generation

The embedded fixed-operation image worker is built with Python 3.14.5, Pillow 12.3.0, NumPy 2.5.3 and PyInstaller 6.20.0. End users need no Python installation. Full dependency/license trees accompany portable builds; upstream MIT and the PyInstaller bootloader exception are retained. Downloaded packages contain data-only metadata, never executable Python.

The UE3 decoder now also reads an explicitly bounded texture bulk range from an exact-hash owned TFC. Both the compressed cache identity and decoded mip identity are verified. Paired diffuse input for the accepted Valkyrie normal algorithm must come from that same verified cache, with its own exact bounded range and decoded SHA. Unsupported operations and dimensions fail closed.

Private C# consumer evidence:
- `artifacts/hd-texture-verified-v2/recipe-report.json`: all 10 accepted weapon/glove DDS hashes PASS from owned TFC, including original first-present Kasimov diffuse mip (not falsely called recovered mip0).
- `artifacts/knife-verified-v2/recipe-report.json`: accepted knife geometry, tangent lighting and all three SVRT material maps PASS. Geometry recipes contain source offsets only. Original reflected cooked position/half-UV/index data are read from the owned core; accepted calibration and lighting math run locally.
- `artifacts/image-pipeline-fixtures3/test-report.json`: 50 core regressions PASS, including real embedded worker execution from synthetic original texture bulk and invalid paired/geometry metadata rejection.

Reproduce private metadata preparation with `prepare-hd-texture-recipes.py --source ../SingularityVR-final-completion --assets ../SingularityVR-assets-local --output artifacts/FRESH_HD` and `prepare-knife-recipes.py --source ../SingularityVR-final-completion --mesh <private-original-2010_knife.pskx> --output artifacts/FRESH_KNIFE`. Build the embedded worker before compiling C# using `scripts/build-asset-worker.ps1 -InstallDependencies`.

These close local generation for three native hand caches, ten HD textures and five knife resources. They do not close authored UI/pose resources, portable preset/module integration, media provenance, production signing or clean-Windows acceptance. Bootstrap availability remains unchanged until full package closure.

### Final local closure follow-up

The earlier pending UI/preset items are now prepared: 20 UI/pose/localization
resources, native font reconstruction, all 381 accepted INI keys and texture INI,
and five native Centurion reload resources. The latter are generated locally from
the exact owned pistol package, with bounded exporter commands and fixed worker
operations; all five match accepted final hashes in the compiled C# consumer.
Inline shell texture data is read from the decoded package; it is not incorrectly
treated as an external compressed TFC mip.

`compose-v7-candidate.py` normalizes runtime-code to required vr-core, requires
native hands, checks target ownership/dependency cycles, adds the language-manifest
entry and refuses unexplained accepted-pin omissions. It emits unavailable private
prepared manifests, never a fake approved public catalog. Five exclusions are
explicit: owned EXE/core are read-only, three inactive PolyOne reference caches
are omitted in favor of accepted native hands.

The full actual transaction consumer passed 178 targets, including all 124 cooked
package reconstructions, three native hand caches, ten HD DDS, knife, Centurion,
UI/fonts and settings. Reinstall, corruption detection and repair passed; uninstall
restored exactly 127 original source files. No Steam writes, DLL loading or game
launch occurred. This uses the known owned snapshot, not every retail edition.

Latest evidence: `docs/evidence/v7-complete-local-integration.json`: 54 C# tests,
5 producer tests, 8 normal WPF cases and 8 self-contained portable cases PASS.
The real frozen image worker also passed a data path longer than 260 characters.
An initial combined fixture exposed a Win32 path problem; absolute worker data
paths now use extended Windows paths while executable caches stay short.

Accepted V7 couples Polish Bink movie audio to the Polish text flag. English text
with complete Polish movie voice is explicitly refused before writing; the other
three choices set the reviewed text/native-voice INI flags. No runtime binary was
silently changed to invent independence. Production media rights, linked-exporter
notice review, signing and clean-Windows/physical acceptance remain separate gates.
