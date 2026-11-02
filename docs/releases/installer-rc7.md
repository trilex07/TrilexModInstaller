# Installer RC7 — current accepted runtime and Singularity-only process guard

This release combines the installer process fix with the accepted SingularityVR
runtime. It is not the earlier installer-only guard-fix ZIP.

RC7 diagnostic refresh (2026-11-02): the installer now names the failed locally
reconstructed output, source/operation, length and expected/actual SHA-256 in
the error and local log. This is not a confirmed GOG compatibility fix. Send
`installer.log` from **Open local logs**, installer version, game version and
language if installation fails. Re-download the complete ZIP for this update;
the signed catalog and all mod packages are unchanged.

- x86 renderer: A16FC1927B215A1A306DBE56CE24B1CB03A042A541EC7C8C4A4CBA1BF6A4A75A.
- Runtime build source: 6c38abea084023e8e5f7702385e21b37e71adc9f, integrated
  into SingularityVR main by 92fe119c187076defb2f5038e1678ec15c0da137.
- x64 gaze/trigger bridge: C4D002A3F09258F311EFF9CFB4FF20ABAD4CEB9CF3967E25A80212DFAF335271.
- Stereo cinema for engine-rendered cutscenes, gaze weapon-wheel selection,
  gaze-centred NIS sharpening and damage haptics are enabled in the preset.
- Scripted mutant-tackle aim assistance is enabled. The user accepted hitting
  and passing that scene in build A16FC192. This is not a fresh clean-install
  headset acceptance of every feature or every pending HUD/recenter case.
- Test-only Kasimov/Centurion grants are OFF. Owned native inventory remains
  authoritative. Seeker physical reload remains disabled.
- Existing HD weapons/textures, menus, weapon HUD, Polish text/dubbing and local
  audio recipes remain byte-identical to RC6. Native Bink films remain mono;
  stereo cinema applies to engine-rendered scenes. Unverified/pending work is
  not silently substituted into this release.
- Only running Singularity blocks installation; other Steam apps and VR tools
  are allowed. No process is terminated. EXE compatibility, reconstruction
  hashes, backups and transactional rollback remain enforced.

## Installing/updating

Download the complete ZIP and extract the entire directory to any writable
folder, including another drive. Keep packages, catalog, tools and EXE together.
Close Singularity, select its existing game directory, then install. Preserve
LocalAppData backups; do not uninstall/reinstall the game to update RC6.

Separate package downloads duplicate what is bundled. The evidence JSON is an
optional report and is not required to install. Retail-disc compatibility remains
unverified; send the exact error/log if that version is rejected. The installer
does not bypass unknown executable or original-file checks.

PSVR2 gaze and adaptive triggers require the compatible PSVR2 Toolkit backend
installed/configured on the recipient's PC. No driver is bundled or installed.
Other headsets retain ordinary controller selection/sharpening fallback. No
performance or 90Hz guarantee is inferred from packaging or automated tests.

The full distribution remains unsigned by Authenticode; the pinned publisher
catalog signature is separate. Existing content rights declarations are retained.

## Release gate

Before publication: signed catalog verification, actual archive/setting checks,
core suite, offline clean install and RC6 upgrade/repair/uninstall lifecycle,
complete-ZIP integrity and hashes. No Steam write or game launch is needed for
these checks. See installer-rc7-evidence.json for actual results.
