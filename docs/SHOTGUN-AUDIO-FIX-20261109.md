# Singularity shotgun header refresh - 2026-11-09

Published under existing installer-1.1.0 URL; signed catalog 1.1.6. Redownload
full ZIP into a fresh directory, then Update/Reinstall Singularity with Audio
Remaster selected. Merely opening an old installer does not update its embedded
worker. Installer application version remains 1.1.0; module version is
v7-20261109-shotgun-header.

Only shotgun recipe's encoded/logical/target hashes and audio-module archive
metadata changed in catalog. Compatible padding is accepted for the exact
reviewed shotgun profile plus existing Valkyrie profile; legacy signed recipes
remain reproducible and unrecognized hashes/profiles refuse. No renderer,
settings, PSVR2 payloads or other game archives changed.

Original bank 57E8E5A37B667A4CB0A4EC4C2095DDDA3782063E92A7AA559CF314D4B84F33A4
contains shotgunFire35 rejected by shipped native decoder (-133). Repaired bank
88548AA9F8F20044418B9CE121FDF3C74923A6BBC0DB6D39A2F2AEE340AE383F opens
all54 waves; all92 compressed packets and290980 decoded PCM bytes unchanged.
Bank defect proven; player's crash cause and gameplay clearance UNCONFIRMED.

Validation: 15 producer tests, 106 core tests, Release desktop publish, frozen
worker and actual C# package consumer exact reconstruction from owned original,
install/reinstall/damage-detection/repair/uninstall/exact original restore PASS.
Initial core run used overly long default fixture path and failed worker temp
extraction (105/106); documented short TRILEX_TEST_ROOT run passes106/106.
No game launch, Steam writes or physical acceptance.

Publication artifacts: artifacts/installer-1.1.0-shotgun-20261109/.
Remote SHA256 digest verification of all five uploads and every entry in release
SHA256SUMS PASS. Predecessor checksum for untouched legacy wonderlands-vr-core.zip
was stale; independently downloaded archive matches remote digest93D5D57E...
and checksum metadata corrected. No Wonderlands payload update.

Installer ZIP SHA256860F447E43EFC30E2171F3C72A71B1C32CF03AFBA3AE7C298EF6F9698E285E25
EXE FD50EB1098E61D90E9888602DB31EA0505D9EFD0A09648ADE654449E3A8CC9BB
Signed catalog3287C83259920A282D03469ECF3E528D34E3A57011B7CFC9A1FF701FE0A3892C

Player check: receiving shotgun, first shot, several shots and reload. If still
crashing, preserve %LOCALAPPDATA%/SingularityVR/view_matrix.log and relevant
Windows event/dump; do not infer the cause from directory screenshot alone.
Disc/GOG compatibility work remains separate from this published audio refresh.
