# Installer 1.1.0 — simpler installer, automatic update check

## Changes

- An unrecognized game EXE hash no longer blocks installation. Steam builds with a different hash are accepted automatically and recorded in `installer.log`. The warning dialog remains only when the Darkness II renderer signature scan does not find every signature.
- On startup the installer fetches the latest signed catalog from GitHub, detects all three games and shows the installed and latest mod version on each game tile.
- A banner offers the newer Trilex Mod Installer when the latest GitHub release tag is newer than the running application.
- One main action replaces Review/Verify/Repair/Check updates: Install, Update to vX, Repair installation, or Up to date. Installed files are verified automatically after the game is detected.
- Uninstall and Manual installation (folder + README) are separate buttons. Rare options moved to "More options".
- An interrupted installation is detected on startup and rolled back automatically; if the game is running, recovery is deferred with a message.
- Fixed Polish module labels that were double-encoded in the signed catalog (e.g. "Remaster dźwięków", "Śledzone dłonie i TMD", "Natywny nóż", "Rdzeń VR").

## Inputs and preservation

Catalog 1.1.0 is the signed 1.0.9 catalog with only the version and five Polish labels changed (`scripts/fix-catalog-encoding.py`). All module archives, hashes and download URLs are identical to 1.0.9, so packages continue to download from their original releases.

## Verification and limits

87 Core tests, including recovery detection after a killed installer process. Bilingual WPF render smoke. Live start against GitHub: catalog and release tag fetched, all three Steam games detected. No Steam deployment or game launch in this release task; no new PSVR2 acceptance claim.

## Reproduce

Sign `artifacts/installer-1.1.0/catalog.unsigned.json` with the publisher key, then run `scripts/assemble-1.1.0.ps1`.

## November 8: game-reinstall refresh

After a Steam reinstall, leftover or replaced mod files no longer block the installer. Download & install repairs a damaged installation instead of failing with "Modified installed file ... use Repair explicitly". Uninstall leaves files replaced outside the installer untouched and finishes; Update accepts files restored to the Steam original. Same version, catalog and download link; download the ZIP again. 88 Core tests, including leftover-older-mod-file repair after reinstall.
