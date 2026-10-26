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
