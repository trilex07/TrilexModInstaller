# Installer RC5 — accepted October 30 runtime

Runtime source: `e814f137675b8ab075c4adb10c1fc9fb1eca5f4a` (runtime code `b239bf30`, subsequent documentation only).
DLL SHA256: `147C3297211CFA6CA6B964E29FBBD79538B7EE69D7BC155861B66F67B24BDC96`.
Accepted installed INI: `81E9F33BE4D6A5A968DE0D96A45C59A262F2FDD3DB8ECF7DED89D40CF12A87E2`.

## Included changes

- Accepted inner-wrist HUD with real E99 and health-pack counts; legacy health display disabled.
- Accepted native muzzle VFX correction and owned Seeker wheel selection.
- Volk, Valkyrie, Kasimov and Centurion native-format HD geometry and physical reload resources.
- Updated 4K weapon materials and Centurion/Kasimov native audio compatibility packages.
- First native menu button polish, preserving GFx interaction.
- Existing Polish text, voice, movies, remaster and clean-install dependencies retained.

The preparation script compares the accepted installed outputs with every existing module. Changed cooked packages/materials and additional source-derived geometry use exact-original local reconstruction. Existing publisher fan-mod notices remain; no new license permission is inferred. No stock executable, saves, whole cooked package or private key is shipped.

All 13 additional reload resources are pinned in `inventory/v7-runtime-resources.json`. The generator and release consumer verify original and output hashes. Settings use the accepted values as defaults and retain the installer's existing three-way merge for user preferences.

## Reproduction

Private accepted sources and hash-matching stock originals are required. No Steam writes occur.

1. `python scripts/prepare-accepted-rc5.py`
2. `scripts/assemble-first-edition.ps1 -Inputs artifacts/accepted-rc5/inputs.json -Output artifacts/Trilex-SingularityVR-1.0.0-rc.5 -Tag installer-1.0.0-rc.5`
3. Run actual signed-bundle clean/RC4-upgrade lifecycle tests with frozen originals, then published EXE PL/EN render smoke.

Installer EXE remains unsigned; catalog is signed by the existing pinned publisher key. Gameplay user acceptance applies to installed build 147C3297. No new FPS or exhaustive level/audio acceptance is claimed. A recipient's physical clean-install PSVR2 acceptance remains separate.

## Verification

- Core: 64 PASS; package producers: 14 PASS.
- Published self-contained EXE: 8 PL/EN layout smoke tests PASS, no binding errors.
- Actual packed runtime/settings and all 204 resource identities PASS.
- Clean signed-bundle lifecycle: 12 modules / 372 targets, reinstall, detect damage, repair, uninstall and 132 byte-exact stock restorations PASS.
- All 15 uploaded primary assets matched GitHub SHA256 digests and sizes.

Full evidence: `installer-rc5-evidence.json`.
