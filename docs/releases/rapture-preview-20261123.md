# Everybody's Gone to the Rapture VR — preview refresh (2026-11-23)

Status: **prepared locally, not signed with the publisher key, not uploaded.** Game state stays `preview`.

## Build

RaptureVR master `b1068cf` (E074b): Quality 115–120 % with contrast-adaptive sharpening, frame generation
(trilex-xr-framegen `cddf8ea`, game motion vectors from the eye depth), eye-tracked foveation canted per eye,
head walk direction by stick rotation, optional upscaler (Off by default), OpenXR application name `RaptureVR`
(what SteamVR shows). Same 22 files and targets as the 2026-11-21 preview.

- dinput8.dll `8B6421326873FB5E…`, openxr_loader.dll `66946EA6E9CC2649…`, RaptureVR.ini `7E11E9C7C90197A3…`
- `python scripts/package-rapture.py --mod D:/projekty/rapturevr --tag 20261123` →
  `artifacts/rapture-20261123/rapture-vr-core-20261123.zip`, 22 files, 7 854 362 bytes,
  SHA-256 `289AFE8FE417E9F188B582B4EF8005A3A3394A0F0F73D068041045A908BDF62A`; unsigned catalog 1.1.13 on published 1.1.12.

## Tester bundle (manual install, no installer needed)

`artifacts/rapture-20261123/tester/RaptureVR-test-20261123.zip` (7 811 995 bytes, SHA-256
`CEED67BC01E5869973D41A38999AA45A6D56864B364D6EF34797F0A63482F26B`): `Bin64/` with the same runtime files plus
`README.txt` (PL/EN: requirements, install, uninstall, controls, modes, reporting).

## Evidence

L1 ctest 24/24, L2/L3 per RaptureVR ENGINE_NOTES E071–E074; L4 for this exact build: not yet (owner's headset check).

## Maintainer steps left (need the owner's go-ahead)

Same as 2026-11-21: headset acceptance → `stable`; sign the catalog with the publisher key and verify; upload the ZIP to
the release named in `downloadUrl`; portable bundle `packages/rapture/vr-core.zip`.
