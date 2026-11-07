# Trilex Mod Installer

Free fan-made Windows installer by **Trilex / trilex07** for **Singularity VR**, **The Darkness II VR** and **Tiny Tina’s Wonderlands VR**.

**[Download installer 1.0.9](https://github.com/trilex07/TrilexModInstaller/releases/tag/installer-1.0.9)**

Extract the whole ZIP and run `TrilexModInstaller.exe`. Select a game, choose its installed folder and options, then review and install. No Python or .NET installation is required. The executable is not Authenticode signed.

![Installer interface](docs/screenshots/installer-en.png)

## Included features

- **Singularity VR:** accepted V7 runtime (`7AD356F8`), motion-controlled hands and TMD, physical reload resources, inner-wrist health/E99/item HUD, weapon-mounted ammo HUD, corrected muzzle VFX, HD weapon resources, native menus, Polish text/dubbing, local audio remaster, stereo engine-cutscene cinema, gaze weapon selection/NIS and damage haptics. Gaze requires a compatible headset/backend; Bink video remains mono.
- **The Darkness II VR:** VR runtime, native hand resources, menu changes, Polish text and optional HD textures. Existing voice modifications are preserved; this release does not supply a Darkness II dubbing pack.
- **Tiny Tina’s Wonderlands VR:** native x64 OpenXR runtime, stereo, tracked hands, controller aiming, support grip, wrist HUD, menu laser, rumble and optional PSVR2 Toolkit triggers/gaze. Requires DX11. Packages the current installed runtime/profile; this installer release has offline lifecycle checks, not new headset acceptance.
- **Manual installation:** select “Prepare files for manual installation”, choose your source game/options, review and choose an output folder. It creates ready-to-copy `files`, README-PL/EN, a manifest and checksums without modifying the game. Original-derived options require readable game sources. Repeat for each game you want to export.
- **Run the game:** launches the selected managed game's EXE directly. The button does not require Steam; the game's own launcher/DRM requirements still apply.
- English/Polish installer UI, signed catalogs, package hashes, per-game state, verified backups, repair, uninstall and interrupted-install recovery. User settings use three-way INI merging.

Singularity Polish dubbing and HD textures download from the configured Google Drive links only when selected. Both archives have signed size and SHA-256 pins. Darkness II HD textures download on selection and are converted locally against owned game caches; the bounded image-only source is mutable and the generated file hashes are recorded. No downloaded scripts are executed.

The complete installer bundle includes the other module archives and pinned UE Viewer, avoiding the earlier gildor.org download failure. The optional local audio codec may still need an internet download. Evidence JSON files are maintainer test records and are **not required** for installation. Separate release packages are for recovery/manual offline caching, not additional mandatory installs.

## Requirements and recovery

Windows x64, a legally owned compatible game, and the appropriate VC++ runtimes. The installer provides Microsoft links if needed. Choose the folder containing the game's EXE (Darkness II), Binaries and RvGame (Singularity), or OakGame (Wonderlands).

Unrecognized Singularity x86 executables can be tried after an explicit warning, default No. Consent binds the folder and EXE hash. Disc/GOG compatibility remains experimental; source, package and rollback integrity checks stay enabled. Send logs with the game edition/language if reconstruction fails. Do not replace the EXE or download evidence files.

Games must be closed during installation. Unrelated Steam apps, fpsVR and SteamVR do not count as the selected game. The installer never terminates applications. External file conflicts stop installation instead of silently replacing other mods.

Keep `%LOCALAPPDATA%/Trilex/ModInstaller/installations`: it contains original backups and transaction journals. Use **Restore / uninstall** or **Recover interrupted install** as appropriate. **Open local logs** opens local diagnostics; include those logs, game edition/language and the exact error in bug reports. No telemetry is sent.

## Verification

See [1.0.9 evidence and limits](docs/releases/installer-1.0.9.md). Offline, actual installation, gameplay and physical headset acceptance are separate evidence levels. No new FPS or exhaustive HMD compatibility claim is made.

## Build

Requires .NET SDK 10, Visual Studio C++ tools and pinned build dependencies:

```powershell
./scripts/build.ps1 -Publish
```

The default source bootstrap is separate from the reviewed release catalog. Release preparation uses `scripts/prepare-1.0.9.py`, publisher signing and `scripts/assemble-1.0.9.ps1`; private generation inputs and signing keys are never committed.

- `src/Trilex.Installer.Core`: catalogs, adapters, downloads, reconstruction and transactions.
- `src/Trilex.Installer.Desktop`: WPF app and process guard.
- `src/Trilex.Installer.Tools`: signing, verification and controlled installation fixtures.
- `tests`, `tools`, `scripts`: regression tests, fixed asset workers and packaging.
- `catalog/trusted-publisher.pem`: public catalog trust pin.

Code is MIT; branding, games, voices, artwork and external dependencies retain their separate ownership and terms. Fan publishing does not establish third-party permissions. See [distribution notice](docs/DISTRIBUTION-NOTICE.md), [publisher decision](docs/distribution/FIRST-EDITION.md) and [third-party notices](THIRD-PARTY-NOTICES.md). No commercial game EXE is included.
