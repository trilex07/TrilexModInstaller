# Installer package, texture preparation and UI repair — 2026-11-10

## Confirmed failures and changes

- An old portable bundle can load a newer signed online catalog. Its adjacent `vr-core` ZIP then differs from that catalog. Previously this was fatal (`Bundled package changed`). The desktop installer now logs and ignores the mismatched bundled candidate and uses the normal exact-hash cache/download path. Offline mode still requires matching cached bytes. Package signature/hash enforcement is unchanged; mismatched bytes are never installed.
- The Darkness texture encoder evaluated whole-image float64 color distances. At 4096 pixels this alone could exceed 3 GiB. It now processes independent 64-row strips, preserving encoded endpoint/index bytes. Conversion runs below normal priority and emits a flushed receipt after every texture.
- Activity is outside the page scroll area; the long review has its own vertical scroll. Conversion shows completed/total texture count.
- Normal managed installation now has one **Install selected / Zainstaluj wybrane** action. It validates the checkbox selection, resolves downloads and installs in the same operation, for fresh installs and updates. The duplicate Download & install action is hidden. Explicit manual export retains its separate destination-confirmation action. Existing compatibility prompts and transaction backups remain.

## Verified offline

- Core: 107 passed, 0 failed; includes stale bundle → verified download, offline cache/missing cache and corrupt download rejection.
- Package producers: 15 passed. Native texture layout/strip tests: 6 passed.
- Optimized desktop publish and WPF smoke: PASS; 8 PL/EN layouts, review scrolling, pinned Activity, single managed-install action, existing 24 launch scenarios. No game launched.
- Real frozen worker: all 64 textures completed in 56.46 seconds; sampled worker-process peak working set 114,401,280 bytes (109.1 MiB). This is one local fixture result, not a remote performance guarantee.
- Encoded image bytes match the prior algorithm for all 64 textures. Reassembled native append streams match the accepted BC1/BC3 layout. The historical reference's final B/F cache bytes predate the native interleave correction and are not used as a final-cache identity assertion.
- Real embedded-worker `PrepareAsync`: PASS, 64 progress receipts, 6 generated targets; source fixture game files unchanged. Existing installation/restore tests also pass. Live game texture appearance was not retested here.

## Delivery boundary

Refresh installer 1.1.0 at the existing full-ZIP URL. Preserve catalog 1.1.7, every game ZIP, the asset worker, shotgun audio repair, PSVR2 clients and current Wonderlands headset package byte-for-byte. Include updated texture-worker source with its existing license materials. Assembly records source, installer, catalog and ZIP hashes and verifies every ZIP entry.

Users must redownload the complete ZIP and extract into a fresh folder to get the changed EXE. A newer game catalog does not update an old installer executable.

The player's reported SteamVR hang is not independently reproduced or proven to have this single cause. The excessive allocation and missing progress are confirmed and repaired; remote acceptance remains PENDING. If it recurs, preserve the installer log immediately after cancellation/failure and the last displayed texture count.
