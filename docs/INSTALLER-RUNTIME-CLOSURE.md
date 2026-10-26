# RC4 clean-install runtime dependencies

The RC3 source of truth was the final V7 deployment delta, not a complete
installation. Files retained from earlier accepted candidates were missing.
An archive matching that incomplete list could install and verify successfully
while failing at runtime. The runtime DLL itself is unchanged (source
`38ca72f88112bfcbd26addb0d6aa4f76095aa15e`, DLL
`23A3A768C44F1C6EB5479D899141924C4C44D5C6A73A3175A2827241105370B1`).

## Evidence and correction

- `MaybeLoadWheelCatalog` reads
  `Binaries/vr-assets/hud/katorga/3d/weapon_wheel_catalog.json` once. The clean
  session logged `catalog missing/unparsable`, `verts=0,0,0,0`, M02 wheel result
  `0`. The enabled observed-selection route suppresses guessed native cycling;
  without the catalog it cannot select a target. Restore the exact working
  catalog, not different controller mappings or rendering offsets.
- Backup versus clean install hashes prove four custom Bink startup/loading
  slots and the boot splash were omitted. Each now requires the exact stock
  preimage and reconstructs the working version locally; originals remain
  under the transaction backup for uninstall. No `.orig` files are packaged.
- The accepted texture INI selects `texture-override-8K`, but that directory
  was absent. The actual bridge logged `LoadDDS: cannot open` for effects and
  surfaces. The pinned inventory includes only DDS names registered by the
  accepted `targets_generated.h`, in 2X, 8K and remaster directories. Ten
  existing weapon/glove generation recipes remain unchanged; the other
  reviewed derivative textures are staged from the user's working backup.
  These are fan-content distribution decisions, not newly verified licenses.
- `PSVR2/AdaptiveTriggers=1` needs the source-owned
  `SingularityPsvr2Bridge.exe`. The installer had refused every `.exe` target.
  Allow only that exact sibling path; retain the signed package/hash checks.
  The game EXE and every other executable path remain forbidden. The helper is
  x64, separate from the unchanged x86 game DLL. It uses the locally installed
  PlayStation VR2 application; no Sony SDK DLL is redistributed.
  Its actual PE imports include `VCRUNTIME140.dll` and `VCRUNTIME140_1.dll`;
  review checks their x64 presence/architecture and offers Microsoft's x64
  prerequisite link separately from the game's x86 prerequisite.

`inventory/v7-runtime-resources.json` pins all 191 runtime dependencies, their
owners, consumers, sizes, output hashes and (where relevant) stock preimages.
`scripts/prepare-runtime-resources.py` verifies every source before copying,
extends existing module inputs and retains their license/review flags.
`compose-v7-candidate.py` now requires both the V7 delta and this independent
resource closure. Missing/mismatched entries fail before packing/signing.

Excluded: diagnostic captures, logs, obsolete DLLs, rollback files, loose
production WAVs, inactive PolyOne glove caches, unused priority folder and the
disabled VFX-host atlas/plugin. Native effects use the active texture bridge.
No game executable, complete cooked commercial package or save is copied.

## Reproduce

Use the read-only known-working backup and the existing reviewed module graph:

```powershell
python scripts/prepare-runtime-resources.py --inputs artifacts/combined-inputs-local-audio-v1.json --accepted-root ../dependencies/Singularity --output artifacts/runtime-resources-rc4
scripts/assemble-first-edition.ps1 -Inputs artifacts/runtime-resources-rc4/inputs.json -Output artifacts/Trilex-SingularityVR-1.0.0-rc.4-complete -Tag installer-1.0.0-rc.4
```

The asset source is private. Commit metadata, code and evidence, not extracted
private assets. The resulting signed fan-mod modules retain their existing
publisher decision and notices; no rights claim changes from RC3.

Validate with the real package/transaction consumer in a fresh marker-only
fixture: install, exact hashes, repeat install, damage detection, repair,
uninstall and exact stock restoration. Also test the RC3-to-RC4 managed upgrade.
No fixture test launches the game or proves headset rendering.

The controlled upgrade exercise is `exercise-bundle-upgrade NEW KEY ORIGINALS
FRESH_FIXTURE PREVIOUS`: both catalogs and all packages are verified, RC3 is
installed first, then RC4 must update, verify, repair and restore stock files.

Physical acceptance remains **PENDING**: custom startup/loading art, detailed
wheel artwork and selecting each owned ordinary weapon in PSVR2. No new
gameplay DLL, aim, tracking or input algorithm is introduced by this correction.
