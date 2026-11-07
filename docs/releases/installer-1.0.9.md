# Installer 1.0.9 — Wonderlands and manual installation

## Inputs and preservation

Base: installer source 56a133427b41fda01ea33e5796b068ddd5bd21d1, existing signed 1.0.8 catalog/packages.
Tiny Tina source: 2eed8c8 (full SHA in release evidence), clean source checkout; built x64 xinput1_3.dll SHA256 00919C692D06B031DBFB36DB368ADBE45762EBEC0E016124B34716F7A1E2D809, byte-identical to currently installed runtime. OpenXR loader SHA256 66946EA6E9CC2649E299EC412521C5EAFE56C635D05055097FE4DB81E11932DF. Import audit: proxy imports Windows system DLLs only; loader imports ADVAPI32/KERNEL32. Static CRT, no extra local redistributable DLL omitted.

`config/WonderlandsVR.ini` preserves installed key values, removing machine-local Simulator paths. Simulator=0. No change to the game, source mod or installed files. EXE is a reference identity, never a blanket refusal. x64 PE validation and consent are exercised. Actual runtime hooks retain their own native byte checks.

Singularity and optional Darkness module archives stay identical to 1.0.8. The November 7 refresh updates only Darkness vr-core d3d9.dll and its catalog hashes. Optional Drive links and hashes remain pinned. Package paths are isolated: packages/, packages/darkness2/, packages/wonderlands/.

## Manual export contract

All selected modules use the same downloader, allowlisted extraction and bounded reconstruction as automatic mode. Export requires a source game for original-derived content and uses verified managed original backups when available. Finishes a fresh directory outside the game via rename; failure/cancellation removes only its own temporary export tree. Does not apply transactions, deselect managed modules, modify game files or write installed receipts. Merge selected INI keys into the existing profile; deliver exact prepared payloads, SHA256SUMS, original/output identities and PL/EN instructions. Includes original runtime dependency notices from the modules. Private reconstructed game data must not be treated as a universal redistributable pack.

Exports are per selected game/options. No pre-existing output directory is overwritten, no game/save EXE is included, and no automatic uninstall is claimed for manual copying.

## Verification and limits

87 Core tests (existing 83 plus manual/no-write/cancellation/original-backup/x64-adapter lifecycle checks). Production signed Wonderlands module is installed, verified, exported and byte-compared, then originals restored in a synthetic game tree, without executing the PE fixture. Tiny Tina CTest 8/8. Bilingual WPF layout/binding checks cover all three games and manual export. Final exact results and binary hashes are recorded in installer-1.0.9-evidence.json.

No Steam deployment or game launch in this release task. No new PSVR2 acceptance or universal performance claim. Preserve 1.0.8 as the prior release.

## Reproduce

Prepare: scripts/prepare-1.0.9.py (requires private local build inputs); sign/verify catalog using existing Tools and publisher key; publish Desktop using the signed catalog; run Tools exercise-wonderlands with a fresh fixture directory; run --render-smoke with isolated TRILEX_HOME; assemble using scripts/assemble-1.0.9.ps1. No private keys or game EXEs belong in Git.

## November 7: Darkness II gaze stability refresh

Accepted runtime SHA256: D72F8E1CBED5CE41D7F90B5775C6CC89A55F39CA0F69933D3A01021C4CA498EE. Darkness2VR implementation 03e7b38d, frozen package 0c0511e1, installed-state handoff 3eed105c. Shared tentacle acquisition/throw filtering retains brief tracking loss up to 160 ms, rejects isolated gaze jumps and confirms deliberate large changes. User reports improved behavior. No configuration, loader, optional module or other game payload changes.

Runtime verification: 51 eye-tracking, 444 acquisition and 319 native-throw checks passed. Steam simulator exercised jitter, short blink, isolated spike, sustained fixation and long-loss fallback. Full live held-object flight and new physical headset acceptance remain unverified. This refresh preserves the 1.0.9 application version and download URL; download/extract the refreshed full ZIP. Previously downloaded ZIPs do not update themselves. Packaging verification and exact artifact identities are in installer-1.0.9-evidence.json.
