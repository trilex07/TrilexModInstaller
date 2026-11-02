# Installer RC6 — bundled exporter / DNS recovery

Installer build source: `583d2fd3cbf70c37e62148371f87d551b19cedd8`.
The accepted gameplay DLL remains `147C3297211CFA6CA6B964E29FBBD79538B7EE69D7BC155861B66F67B24BDC96`.

## Confirmed cause

The reported RC5 failure was `HttpRequestException: No such host is known.
(www.gildor.org:443)`. Even the complete RC5 ZIP needed a first-use exporter
download. This happened before the game-file transaction; it does not establish
retail-disc incompatibility or a missing evidence report.

## Fix

- RC6's complete ZIP contains the unchanged official UE Viewer archive at
  `tools/umodel_win32.zip`, SHA256
  `2CEAD261B360DBCC3E703F091837B415F868250DD6A73A154F4232D09E226EF8`.
- The installer checks its verified cache, explicit archive override, adjacent
  archive, then the existing online fallback. Local import works in offline mode.
- Existing exact archive, ZIP-entry allowlist and independent x86 EXE/SDL2 hashes
  remain mandatory. A corrupt adjacent archive fails without downloading another.
- The complete bundle includes upstream LICENSE.txt/readme.txt and SDL2's zlib
  notice. No game assets are extracted into the public source repository.
- Packaging refuses a missing or wrong archive before building the bundle.
- PL/EN review text explains the bundled tool; download errors explain recovery.

All twelve module archives and game-file declarations match RC5 exactly. No new
gameplay runtime, save, ammo, audio, texture or INI changes are introduced.

## Validation

The release evidence JSON records the actual signed-bundle lifecycle, published
EXE smoke results and exact ZIP/EXE/catalog hashes. Focused tests cover simulated
DNS failure, no-network import into a fresh tool cache, corrupt archive refusal
and all bundled file hashes. Source audit excludes private keys and game payloads.

These installer checks do not claim physical PSVR2, retail-disc compatibility or
performance acceptance. FFmpeg still uses its existing first-use download/cache.

## Download and update

Extract the entire complete RC6 ZIP into a new directory. Keep EXE, `tools`,
`packages` and signed catalog together. Select the same game folder to update;
retain the LocalAppData installation backups. No game reinstallation is required
for an otherwise healthy RC5 installation.

The separate module ZIPs duplicate the included packages.
`installer-rc6-evidence.json` is an optional report, never an installer input.
The EXE remains unsigned; the catalog is signed by the existing pinned key.

Reproduce with `scripts/assemble-first-edition.ps1`, the frozen accepted RC5
inputs, a fresh RC6 output, and `-Tag installer-1.0.0-rc.6`. Provide the original
archive through `TRILEX_UEVIEWER_ARCHIVE` if it is not already in the build cache.
