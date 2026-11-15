# Wonderlands patch and Epic selection - installer 1.1.3

Catalog 1.1.10, November 16, 2026. The preceding Wonderlands public package was
November 10 EFC1685E; installer 1.1.1 on November 14 retained that package.

- Steam: current 780C91D6 native runtime; physical Medium/Long scope placement,
  selected-eye optical capture, optional centre dot in VR settings and tracked
  muzzle correction for beam weapons. The current DX12/DLSS/FG implementation
  is included; FG remains beta and OFF by default, as does ScopeDot.
- Epic: automatically detect launcher manifests and signed executable probes;
  expose an Epic Games version checkbox for manual selection. Include the exact
  535502EE small overlay reported working by players, without substituting the
  Steam DLL. This older Epic runtime does not include the newest Steam optics
  and beam changes. Launch normally through Epic Games.
- Package signed NVIDIA SR/FG runtime files plus complete license/notices.
  NVIDIA driver and supported hardware remain required. Simulator/private paths
  are disabled. User-modified settings retain the normal three-way INI merge.
- All Darkness II and Singularity packages and settings are byte-identical to
  installer 1.1.1. No unpublished Darkness II 1.1.2 candidate is included.

Verification: Steam runtime Release and CTest 40/40; simulator Medium/Long scope
and dot plus two-eye beam firing, zero hook refusals/pin mismatches; 114 installer
regressions; eight PL/EN WPF layouts with Epic checkbox binding checks; actual
signed Steam and Epic package install/manual export equality/verify/uninstall
and byte-exact original restoration in isolated synthetic fixtures. No game
was launched for installer testing. Latest patch headset testing remains pending.
Owned Steam simulator shutdown still returned C0000409; not fixed in this patch.
Player Epic reports are not our own full Epic/headset acceptance.

Download/extract the full ZIP into a fresh folder, close the game, select
Wonderlands and use Install selected. Automatic detection checks Steam and Epic;
for manual Epic setup select its game root and enable Epic Games version.
Do not copy or replace the game's EXE. Existing old installers cannot interpret
alternate build profiles; use the new full installer for Epic.

Sources:
https://github.com/trilex07/TinyTinaWonderlandsVR/blob/main/docs/RELEASE-20261116.md
https://github.com/trilex07/TinyTinaWonderlandsVR/tree/codex/wonderlands-epic-player-packet-20261116

The installer skips locally reserved 1.1.2 and uses 1.1.3 to avoid colliding with
that separate unpublished Darkness II candidate. Catalog also advances past its
reserved local revision. This release changes Wonderlands and installer routing.
