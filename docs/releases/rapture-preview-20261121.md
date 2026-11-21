# Everybody's Gone to the Rapture VR — installer support (preview, 2026-11-21)

Status: **prepared locally, not signed with the publisher key, not uploaded.** The game entry is `preview` in the
unsigned catalog, so the consumer app shows it but installs nothing until a maintainer promotes it.

## What the installer knows

- Game `rapture`: Steam app 417880, `Bin64/Rapture_Release.exe` (x64, SHA-256 `4B9E527D…1FEF`), data folder `rapture`,
  `RaptureGameAdapter`, x64 PE check, process guard on `Rapture_Release.exe`, Steam launch `steam://rungameid/417880`.
- Allowed targets only: `Bin64/dinput8.dll`, `Bin64/openxr_loader.dll`, `Bin64/RaptureVR.ini`, `Bin64/LICENSE-RaptureVR.txt`,
  `Bin64/LICENSE-OpenXR.txt` and the 17 panel images under `Bin64/RaptureVR-ui/`. Any other path (the game EXE, other
  games' files, unknown images) is refused by `PlanBuilder.ValidateManifest`.
- Nothing outside the game folder is written by the installer. The mod itself applies the VR start settings to
  `Saved Games\Rapture\game.cfg` on its first start (`GameCfgPreset=1`) and keeps the original as
  `%LOCALAPPDATA%\RaptureVR\gamecfg\game.cfg.before-vr`; copying that file back restores the game's own settings.

## Package

`python scripts/package-rapture.py --mod <RaptureVR checkout> --tag <date>` stages the built RaptureVR files
(static-CRT proxy, OpenXR loader, VR profile, panel art, MIT + OpenXR notices), packs them with the `pack` tool (consumer
extractor audit) and writes `artifacts/rapture-<tag>/catalog.unsigned.json` on top of the newest signed catalog
(1.1.10 → 1.1.11). 2026-11-21: `rapture-vr-core-20261121.zip`, 22 files, 7 602 751 bytes, SHA-256 `F4C56D6C…9F11`.
Validated by signing with a throwaway key and `verify` ("Verified catalog 1.1.11"). Tests: 115/115 including
"Rapture x64 adapter, Bin64 targets, panel art, install/export/restore and guard".

## Evidence on the mod side (RaptureVR repo)

- L3 `e46-files1`: files only in Bin64 (as this installer does), original `game.cfg` (fullscreen, sys_spec 3), plain
  Steam start → the mod applied the VR preset and kept the original, window resized to the SteamVR-derived size,
  XR session up; afterwards `game.cfg` restored byte-exact. Gameplay projection not reached in that 60 s window (intro).
- L4 for this exact build: not yet.

## Maintainer steps left (outward-facing; need the owner's go-ahead)

1. Headset acceptance of the packaged build; then set the game `state` to `stable`.
2. Sign `catalog.unsigned.json` with the publisher key (`private/`), verify against `catalog/trusted-publisher.pem`.
3. Upload `rapture-vr-core-<tag>.zip` to the release named in `downloadUrl`; add it to the portable bundle as
   `packages/rapture/vr-core.zip` with the signed catalog.
4. The desktop app could not be built here: `artifacts/psvr2-client-runtime/psvr2_toolkit_capi_loader.dll` is missing
   locally (embedded resource). Core, Tools and Tests build.
