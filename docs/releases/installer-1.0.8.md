# Installer 1.0.8: scope and acceptance

## Source and packaged content

Release branch: `release/installer-1.0.8`. The release tag and `installer-1.0.8-evidence.json` identify the exact final source commit and binary hashes.

Singularity's 12 module definitions are preserved from the reviewed Drive-migration catalog. Two optional archive URLs use the supplied Drive files, retaining exact sizes and SHA-256 pins. The accepted x86 runtime remains `7AD356F832ED84E2F599733BE58553BD661381C6C2FD51B4ACDEE25116F38E86`.

Darkness II imports only four reviewed packages from SIDE's `darkness2-full-candidate`, not its older Singularity catalog. All archive hashes match the handoff. The runtime is `622D75E5B58496652106A804EF674AC037F636B169F7104C4FF81781D088A32D`. The game is selectable and its displayed module versions are 1.0.8, with no preview marker. HD conversion is a separate optional local transaction using the existing embedded converter.

## What the audit found

- Both Singularity Drive archives downloaded anonymously and passed exact signed length/hash verification. All other selected module ZIPs are bundled and hash-checked.
- The local Singularity deployment already had the accepted runtime, but its installer receipt recorded an older manual deployment. Controlled adoption accepts only bytes equal to the new signed target. Unknown external changes still stop the operation.
- The installed Singularity copy was missing the gaze-notices file. Installation includes it.
- Darkness II had unmanaged hand/font/menu patches and HD caches. These are not clean recipe sources. The local migration keeps verified snapshots and uses hash-verified originals from existing private fixtures. This is a local migration, not a public bypass of recipe integrity checks.
- No Darkness II dubbing package is present in SIDE's installer handoff. Existing local voice caches remain untouched. The installer does not promise a new Darkness II voice pack.
- Matching QuickBMS source, worker sources and collected dependency notices accompany the portable release. The pinned UE Viewer remains bundled.

## Verification gates

- Core regression harness: 81 PASS, 0 FAIL.
- Asset/package producer tests: 15 PASS. Drive catalog producer tests: 2 PASS.
- Published self-contained WPF app: eight PL/EN layout/binding cases PASS, zero binding errors. Darkness II module/options screens are captured at the same layouts; displayed modules must be stable and must not contain preview in their versions.
- Darkness II clean-copy lifecycle: EN install, PL update, EN update, verify at each stage, uninstall and byte-exact restoration of nine original files PASS.
- Actual Drive texture ZIP: bounded image-only validation PASS. Embedded converter produced six targets; install/verify, deselect restoration and byte-exact uninstall PASS on copied real caches, including the greater-than-2GB F.Texture cache.
- Both selected real Steam installations are backed up and checked independently after deployment. Exact final results and file counts are in the release evidence. Games are not launched as part of this release preparation.
- New gameplay/HMD validation: NOT RUN. No new FPS, GPU-time or exhaustive edition-compatibility PASS is claimed.

## User installation

Extract the entire `Trilex-Mod-Installer-1.0.8-win-x64.zip`. Select the game, browse to its folder, choose language/modules, review and install. Singularity dubbing/HD and Darkness II HD download only when selected. Optional local sound processing may need its pinned codec download. Evidence JSON files are not required.

Keep `%LOCALAPPDATA%/Trilex/ModInstaller/installations` for backup/recovery. Logs are available through Open local logs. Report game edition, language, installer version and exact error. Disc/GOG Singularity remains a warned experimental attempt, never a claim of verified compatibility. The EXE is unsigned.
