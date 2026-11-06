# Installer 1.0.8: scope and acceptance

## Source and packaged content

Release branch: `release/installer-1.0.8`. The release tag and `installer-1.0.8-evidence.json` identify the exact final source commit and binary hashes.

Singularity's 12 module definitions are preserved from the reviewed Drive-migration catalog. Two optional archive URLs use the supplied Drive files, retaining exact sizes and SHA-256 pins. The accepted x86 runtime remains `7AD356F832ED84E2F599733BE58553BD661381C6C2FD51B4ACDEE25116F38E86`.

Darkness II imports only four reviewed packages from SIDE's `darkness2-full-candidate`, not its older Singularity catalog. The initial runtime was `622D75E5B58496652106A804EF674AC037F636B169F7104C4FF81781D088A32D`. The November 6 refresh replaces only its DLL with `A9D487B1C5D3B73930A9514C28C9B2CC643D35C1E6A997BBFA65C077E462CE72`, using the frozen `menu-state-cache-20261105` package. Other core payloads and all other modules remain unchanged. The game is selectable and its displayed module versions are 1.0.8, with no preview marker. HD conversion is a separate optional local transaction using the corrected embedded converter.

## What the audit found

- Both Singularity Drive archives downloaded anonymously and passed exact signed length/hash verification. All other selected module ZIPs are bundled and hash-checked.
- The local Singularity deployment already had the accepted runtime, but its installer receipt recorded an older manual deployment. Controlled adoption accepts only bytes equal to the new signed target. Unknown external changes still stop the operation.
- The installed Singularity copy was missing the gaze-notices file. Installation includes it.
- Darkness II had unmanaged hand/font/menu patches and HD caches. These are not clean recipe sources. The local migration keeps verified snapshots and uses hash-verified originals from existing private fixtures. This is a local migration, not a public bypass of recipe integrity checks.
- No Darkness II dubbing package is present in SIDE's installer handoff. Existing local voice caches remain untouched. The installer does not promise a new Darkness II voice pack.
- Matching QuickBMS source, worker sources and collected dependency notices accompany the portable release. The pinned UE Viewer remains bundled.

## Verification gates

### Darkness II HD converter correction

The initial 1.0.8 converter regressed the native block-layout repair from Darkness2VR `ef56b259` (physically accepted in `c8015216`). It appended planar DXT streams while declaring raw, uncompressed TOC entries. This caused rainbow/static artifacts on tentacles. The PNG archive was correct; replacing it was not necessary.

The corrected worker unplanes B/F streams once at the raw-cache publication boundary, preserving H descriptors, BC3 alpha, image dimensions, mip order and native paths. Four regression cases cover BC1/BC3 blocks, full 16KiB chunks and tails, unchanged headers and rejected malformed formats/alignment. The read-only `verify_native_layout.py` audit compared all 190 generated payloads (64 H, 64 B, 62 F) byte-for-byte with the accepted pre-install backup: all equal. Independent Pillow BC decoding passed all 126 B/F top mips against the source PNGs; maximum mean RGB error was 7.838/255 for B and 3.253/255 for F. Original bad planar decoding averaged 61.551/255 and 51.238/255 respectively. These are offline pixel and file checks, not a new headset PASS.

Users who selected Darkness II HD in the initial 1.0.8 must use the refreshed installer and reinstall with HD selected. Managed original backups remain authoritative; the corrected transaction regenerates the six cache files. VR DLLs, configuration, audio and other games are outside this fix. The release evidence records the hotfix source separately from the original 1.0.8 tag.

- Core regression harness: 81 PASS, 0 FAIL.
- Asset/package producer tests: 15 PASS. Drive catalog producer tests: 2 PASS.
- Published self-contained WPF app: eight PL/EN layout/binding cases PASS, zero binding errors. Darkness II module/options screens are captured at the same layouts; displayed modules must be stable and must not contain preview in their versions.
- Darkness II clean-copy lifecycle: EN install, PL update, EN update, verify at each stage, uninstall and byte-exact restoration of nine original files PASS.
- Actual Drive texture ZIP: bounded image-only validation PASS. Embedded converter produced six targets; install/verify, deselect restoration and byte-exact uninstall PASS on copied real caches, including the greater-than-2GB F.Texture cache.
- Both selected real Steam installations are backed up and checked independently after deployment. Exact final results and file counts are in the release evidence. Games are not launched as part of this release preparation.
- New gameplay/HMD validation: NOT RUN. No new FPS, GPU-time or exhaustive edition-compatibility PASS is claimed.

## User installation

### November 6 FPS package refresh (same version and download URL)

Runtime source change: Darkness2VR `26c8dca8855e34a9dcb2d561db6e8887d568e89c`; frozen package source `1e819c0926b090bb5cd87da75844e9c3f6b35669`; deployment handoff `6c14d94f87ab3df61293f10606b4528220bece59`. The render path repeatedly walked the native pause graph outside an input-poll cache. The fix caches that boolean per frame/context with a 50 ms expiry, retaining input-poll semantics. The prior agent's WPR trace identified the per-draw ReadProcessMemory cost. Its stationary crane simulator A/B measured baseline 32.21/32.23 FPS versus candidate 89.15/89.43 FPS; those are scene-specific simulator results, not a headset or universal FPS claim. Headset acceptance remains pending. No new native hooks, settings or texture substitutions are introduced by this refresh.

The installer application version and UI are unchanged. Its signed catalog and embedded bootstrap are refreshed with the new archive/DLL hashes. The original release tag remains intact; release evidence identifies the refreshed installer source and binaries. Existing downloaded ZIPs are not updated automatically: download and extract the refreshed complete ZIP. The prior native texture-layout correction remains included.

### Player report: cannot review installation

The screenshot contains a Discord message, not the installer's error. It does not establish an executable, prerequisite or reconstruction failure. Do not claim this refresh fixes an unidentified install refusal or bypass integrity gates.

Send the player:

> Please extract the complete installer ZIP into a new folder and run TrilexModInstaller.exe. Select The Darkness II and choose the main game folder containing DarknessII.exe, for example E:\\SteamLibrary\\steamapps\\common\\Darkness II. Do not select Cache.DLC\\PCPatchManifests\\Cache.Windows or manually place the separate installer packages there. Click Check folder, then Review installation. If it fails, send a screenshot showing the game selection, folder and Activity error, plus installer.log. Click Open local logs, or press Win+R and open %LOCALAPPDATA%\\Trilex\\ModInstaller. The installer.log file is there. If the game starts but VR fails, also send %LOCALAPPDATA%\\Darkness2VR\\d3d9.log. Please mention your game language and installer ZIP version. Do not delete the installations folder: it contains your backups.

Extract the entire `Trilex-Mod-Installer-1.0.8-win-x64.zip`. Select the game, browse to its folder, choose language/modules, review and install. Singularity dubbing/HD and Darkness II HD download only when selected. Optional local sound processing may need its pinned codec download. Evidence JSON files are not required.

Keep `%LOCALAPPDATA%/Trilex/ModInstaller/installations` for backup/recovery. Logs are available through Open local logs. Report game edition, language, installer version and exact error. Disc/GOG Singularity remains a warned experimental attempt, never a claim of verified compatibility. The EXE is unsigned.
