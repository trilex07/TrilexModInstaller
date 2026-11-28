# The Darkness II VR update for installer 1.1.5

The runtime from Darkness2VR commit `c7eb48a6` was tested in the OpenXR simulator. The DLL fixes the warehouse ladder dismount, weapon aim, off-axis tentacle grabs, and fog and shadow alignment in the Villa. The simulator recorded an assisted rebar hit and measured a separate no-target throw along the hand stroke. It also accepted one-hand firing pulses and both controller pulses on damage with camera shake suppressed.

Catalog 1.1.33 updates the Darkness II VR core over 1.1.32. Only the game runtime changes within the core archive. The accepted first-install settings and loader remain in the package. The updated installer preserves edited copies of both Darkness II INIs, including byte-exact settings when the selected language stays the same.

The actual signed packages passed fresh install, managed upgrade and uninstall over a private copy of the Steam executable. Both edited INIs had identical hashes after the update and uninstall. All eight installer layouts passed a smoke test. The test did not write to Steam or launch a physical headset.

- Runtime DLL SHA-256 `55BB00D09AD16D11752DF0178A17B10330FDF5135C9B03C3E9BCB4199106C833`
- Core archive SHA-256 `DFDEB01CC5051FA132CCE08A1186F448A7FF3E6A18A079A48CD8F602256837DA`
- Signed catalog SHA-256 `84382D28B07EF068B78D7318BE54038AEBD33928DEC0540FE8670393F7EF3A87`
- Portable bundle SHA-256 `31556AAF0458CFB900493126549E1C44CFAD94B841EEE969883C89A3B07F1910`

[Installer 1.1.5 with The Darkness II VR update](https://github.com/trilex07/TrilexModInstaller/releases/download/installer-1.1.5/Trilex-Mod-Installer-1.1.5-Darkness2-VR-20261128-win-x64.zip)
