# Trilex Mod Installer 1.0.9

Three games: Singularity VR, The Darkness II VR, and Tiny Tina’s Wonderlands VR.

- Adds Tiny Tina’s native x64 runtime, OpenXR loader and portable snapshot of the installed profile. Choose the game ROOT containing OakGame. The installer writes only the approved files under OakGame/Binaries/Win64; Run the game uses -dx11.
- Adds “Prepare files for manual installation” for every game and its selected modules, including locally reconstructed options. Choose your source game, options, review and output folder. Read README-EN.md or README-PL.md and copy the contents of files to the game root yourself. Source games and managed receipts are not changed by export. Manual installation has no automatic uninstall; make your own backups.
- Keeps unknown executable hashes warning-only with explicit review consent. Per-hook native checks and signed package/recipe bounds remain.
- Retains all 1.0.8 Singularity and Darkness packages and optional downloads unchanged, including the latest Darkness warning-only runtime.
- Tiny Tina profile simulator paths are removed and Simulator.Enabled remains 0. Optional PSVR2 Toolkit is not bundled.

Offline: installer/Core, actual signed Tiny Tina install/verify/manual-export/restore fixtures, Tiny Tina CTest and bilingual WPF checks. No new game launch or headset/FPS claim. Tiny Tina mod remains under development; see its project README for limitations. Evidence JSON is optional, not needed for installation.

Extract the entire ZIP. The installer EXE is not Authenticode signed. Windows x64 is required. No commercial game EXE is distributed.

Darkness II November 7 refresh: stabilized gaze targeting and tentacle throws; brief blinks retain the target, isolated gaze jumps are filtered. Download the refreshed full 1.0.9 ZIP. Other game packages and settings are preserved.
