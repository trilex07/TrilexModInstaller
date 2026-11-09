# Wonderlands headset-accepted Steam refresh

User authorized main and installer publication after testing exact DLL
`EFC1685E762ED4C1F55AB674FD084CF28701A5468FD8EDFFFBF8F495B6D4D491`.
Behavior source `c967f44`; accepted source published to Wonderlands main
`a9a9c966b805755a588686634d221fbb2211293e`. Newer offline candidates are excluded.

Catalog `1.1.7`, module `1.1.0-20261110-headset`, package
`wonderlands-vr-core-20261110-headset.zip`, under the existing `installer-1.1.0`
release and unchanged full-installer download URL. No NVIDIA DLSS runtime is
redistributed; the portable package retains DLSSMode=0, as before. This is not a
fix for the reported DLSS Quality failure and does not overwrite the author's
installed DLSS setting. Existing user-modified settings follow the installer's
normal per-key merge policy.

Enabled reviewed options: ArmedRightArmIK, CenterActionSkillCamera,
MenuFigureLowerCm=12, KeepLootCardOnPanel, HideCrosshair, GunTargetHealthbar,
GazeTargetHealthbar, WorldTargetHealthbar and LootCardFocusHold. Other preset
keys, installer EXE, all other game packages and PSVR2 clients remain unchanged.

OPEN player reports carried into release notes:

- Physical weapon-optic green reticle appears in the left eye only.
- DLSS Quality renders gameplay in the left eye only.
- Enemy health/name intermittently returns to the center panel.

Epic support is not included. General headset acceptance is not exhaustive
acceptance of every skill/weapon/layout or stable 88-90 FPS combat performance.

Verification: downloaded current ZIP/catalog match public checksums; signed
catalog verified by the pinned publisher key; actual signed package managed
installation, manual export equality, verification and byte-exact uninstall
restoration PASS in a synthetic fixture. Fresh isolated accepted-source Release
build and17/17CTest PASS. Repacked ZIP CRC and every nested file compared exactly.
Only five old bundle files differ: catalog, Wonderlands archive, README,
release notes and bundle checksums. No Steam writes or game launch during this
release operation. Current published ZIP/catalog retained locally for rollback.

Reproduction: `scripts/refresh-wonderlands-accepted-20261110.py` prepares from
the checksum-pinned downloaded release, then uses the existing private signing
key and public verification tool before assembly. Local evidence and the release
payload are in `artifacts/installer-wonderlands-accepted-20261110/`; private key
and runtime DLLs are not committed.
