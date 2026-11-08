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

## November 8: Singularity and Tiny Tina's Wonderlands runtime refresh (catalog 1.1.1)

- Singularity vr-core v7-20261108-gravity-fix: d3d9.dll E0E08775 (SingularityVR fa4c3151). Removes the black rectangles in gravity scenes (modulate decal draws, `[Diagnostic] DropDecalLocalTangent=1`) and holds the last tracked wrist correction to stop left-hand flicker (`[Input] TrackedGripWrist=1`). Diagnostic audit traces stay off. User confirmed both on PSVR2.
- Tiny Tina's Wonderlands vr-core 1.1.0-20261108: xinput1_3.dll 87641E55 (TinyTinaVR f638d1b). Unarmed arms follow the controller with elbow IK, shoulder anchor at the neck pivot, forearm twist, deferred graphics apply in VR (menu crash guard), presentation modes and per-weapon grip default. Profile equals the installed accepted INI except local simulator paths.
- All other modules unchanged. Verified: signed Wonderlands package install/export/restore exercise, Singularity vr-core fixture install/verify/repair/uninstall, UI smoke. Same installer version and download link; installed players see Update on the game tile.

## November 8: optional PSVR2 Toolkit and eye calibration

- After installing a game mod, the installer offers to download and install PSVR2 Toolkit. This is optional and available for all three games. More options → PSVR2 Toolkit can repeat setup independently.
- Downloads the upstream BnuuySolutions experimental-3 Windows driver and CAPIApps, with pinned SHA256 and size checks. Preserves the Sony driver and backs up affected files. Requires SteamVR and the PlayStation VR2 App to be closed.
- After installation, a second question offers eye tracking calibration. On acceptance, downloads upstream UnitySetup 1.1.0, starts SteamVR and launches UnitySetup. Put on the headset and select Eye Tracking / Gaze Calibration there.
- Polish and English prompts. Declining or a Toolkit setup error leaves the installed game mod intact. Existing game packages and signed catalog are unchanged.
- Verification: 97/97 core tests, including real upstream package hashes and synthetic driver install/recovery; self-contained Windows x64 build and eight WPF smoke cases passed. Physical PSVR2 installation/calibration awaits player testing.
- Same version and download link. Download the ZIP again to receive this installer change; refreshing the game catalog alone does not update the installer executable.

## November 8: Darkness II missing hands asset

- Fixed a missing installer component: Darkness II now automatically reconstructs the closed-wrist hands without forearms from the player's own game files. Previously the installer omitted this local B.Misc mesh modification, so players received full character arms.
- Matches the exact hand model installed in Trilex's game, including closed wrist caps across all five detail levels and original textures. Included in normal installation, repair and manual export, independently of optional VR fixes and HD textures.
- Uses normal verified backups and transactional restoration. No game models are downloaded or redistributed; the Darkness DLL, settings and existing runtime ZIPs are unchanged.
- Download the full installer ZIP again, then select Update for Darkness II. Old installer executables do not contain this asset reconstruction even after refreshing their catalog. Player headset tests remain pending.

## November 9: Steam-aware Run the game

Player report: Singularity under D:/SteamLibrary/steamapps/common/Singularity produced Steam Application load error 5:0000065434 from Run the game. Confirmed installer defect: all copies launched their EXE directly; Singularity also inherited the root directory instead of Binaries. The screenshot alone does not prove the remote Steam client's state or that this is the only cause of the error.

Run the game now verifies the reviewed EXE and managed receipt as before, then checks the adjacent Steam appmanifest: matching AppID, exact installed folder and steamapps/common layout are required. A matching Steam copy starts through Steam; manually browsed copies work as well. GOG/disc/manual folders without that matching manifest retain direct EXE launch with the executable's own directory. Tiny Tina keeps -dx11 on both routes. Unknown hash consent is preserved. No Steam DLLs, game EXE or appid files are injected or edited. Launch requests log route and target; startup exceptions use the existing visible error handler.

101 Core tests pass, including Steam/manual/unknown hash routing, bad and mismatched manifests, changed EXE rejection, and DX11 preservation. Release x64 build checked; no game was launched by these tests. The remote player's successful launch is PENDING. Temporary workaround for this report: launch Singularity from its Steam library rather than the older installer's Run the game button.
