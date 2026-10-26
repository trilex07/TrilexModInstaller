# Ownership and notices

- Installer C#/scripts authored for Trilex: MIT, see LICENSE.
- Supplied Trilex logos, icons and mockup kit: user-provided design references. Separate branding ownership; not licensed under the source MIT grant. No commercial game screenshot/cover is used in the application. Keep design-kit attribution/provenance when reusing references.
- .NET/WPF runtime: Microsoft and .NET contributors, upstream MIT and included runtime notices. Self-contained distribution must retain generated notices.
- Singularity, The Darkness II, their game assets, voice recordings, textures and trademarks belong to their respective owners. Ownership of the game is required. No distribution permission is inferred from the mod or local runtime acceptance.
- SingularityVR runtime dependencies, including OpenXR and MinHook, require their own upstream license/notice audit before runtime packs are released. This repository does not bundle those game runtime DLLs.
- Voice-model or generated-audio licenses, voice likeness permissions and derivative texture rights are not established by this installer. Such modules are unavailable pending review.

No original extracted PSK/DDS/audio, cooked game package, accepted-profile payload or publisher private key belongs in this source repository or its installer build artifacts.

## Bundled standalone UE3 decoder

The embedded `Trilex.Ue3Decode.exe` helper is GPL-2.0-or-later, including miniLZO attribution by Markus F. X. J. Oberhumer and upstream decoder attribution to letsgosportsteam. It runs as a separate process; no decoder object code is linked into the MIT application or VR runtime. Full license and corresponding source, including the bounded wrapper and build script, accompany portable releases under `licenses/ue3decode-corresponding-source`. From that directory run `powershell -File scripts/build-decoder.ps1` with Visual Studio C++ tools installed. No proprietary assets are needed to rebuild it.

## Frozen local image worker

`Trilex.AssetWorker.exe` includes Python (PSF license), NumPy and bundled numerical-runtime notices, Pillow (HPND and bundled library notices), and the PyInstaller bootloader (GPL with its distribution exception). Portable releases retain full collected license trees under `licenses/asset-worker`. The DDS helper originates in SingularityVR, copyright 2026 letsgosportsteam, MIT; preserve the upstream MIT grant. The image worker runs only fixed local image operations and contains no commercial image data. Python is bundled; no player-side Python installation is required.

## Local Centurion exporter

UE Viewer, copyright 2022 Konstantin Nosov, is provided under its bundled MIT notice.
SDL2 2.0.10 is copyright 1997-2019 Sam Lantinga, zlib license. Portable bundles retain
both upstream notices under `licenses/ueviewer`. See `docs/distribution/UEVIEWER.md`
for exact archive pin and the remaining linked-codec audit gate. These tools do not
grant distribution rights to original game models or animations.

## Optional local audio codec
FFmpeg is obtained separately from the pinned Gyan upstream build only when the
local audio module is selected. It is GPLv3, not covered by this project's MIT
license. Its upstream LICENSE, README and source notice remain in the tool cache.
The native DSP/Vorbis allocation algorithms adapted from SingularityVR retain
that project's MIT attribution in third_party/SingularityVR-MIT.txt. Audio source
recordings and voice model weights are not bundled by this integration.
