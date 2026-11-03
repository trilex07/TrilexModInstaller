# Installer RC7 â€” current accepted runtime and Singularity-only process guard

This release combines the installer process fix with the accepted SingularityVR
runtime. It is not the earlier installer-only guard-fix ZIP.

Disc/GOG/unrecognized EXE follow-up (2026-11-03): a structurally valid Singularity
folder with an unrecognized x86 PE32 executable may now be reviewed and installed
after an explicit PL/EN warning (default **No**). Consent is bound to that folder
and exact executable SHA-256, logged, and stored in the installation receipt.
Changing the executable after review still refuses installation. This permits
testing; disc/GOG runtime compatibility is **NOT VERIFIED**. Back up saves first.
Package signatures/hashes, reconstruction source checks, backups and recovery
are unchanged. Different source assets can still stop installation with an exact
error; send the log instead of replacing the EXE or bypassing source checks.
For non-Steam editions use the game's normal shortcut; **Launch through Steam**
is not a GOG/retail launcher and is not enabled by this consent.

RC7 diagnostic refresh (2026-11-02): the installer now names the failed locally
reconstructed output, source/operation, length and expected/actual SHA-256 in
the error and local log. This is not a confirmed GOG compatibility fix. Send
`installer.log` from **Open local logs**, installer version, game version and
language if installation fails. Re-download the complete ZIP for this update;
at that diagnostic-only revision the catalog/packages were unchanged. This later
mod refresh updates the signed catalog, vr-core, audio-remaster and portable-settings.

RC7 explicit exception follow-up: if a fully generated local file has a different
output hash, the installer offers a per-file warning with **No** selected by
default. **Yes** continues at the user's risk with that particular generated
file; compatibility is unverified and crashes or missing content are possible.
Expected/actual hashes are recorded and the result is shown as **installed with
exceptions**. Originals, transactional rollback and uninstall remain protected;
uninstall cannot undo save changes or prevent crashes. Original-source and
downloaded-package checks cannot be bypassed. Approval is required again for
regeneration. This is not a confirmed Steam/GOG compatibility fix.

## Singularity bug-fix refresh — 2026-11-03

Still **installer-1.0.0-rc.7**: re-download the complete ZIP and extract it into a
fresh folder. Do not mix an older catalog/packages with this refresh. This is a
mod-content bug-fix update, not a new installer version or UI feature release.
The EXE is rebuilt only because its embedded local-audio worker must generate
the repaired Valkyrie bank. Existing consent, backup and recovery rules remain.

- x86 renderer: 7AD356F832ED84E2F599733BE58553BD661381C6C2FD51B4ACDEE25116F38E86.
- Runtime source: 9488a26a (full SHA in evidence), including all intervening
  runtime fixes since the previous RC7 A16FC192 build.
- L1 TMD gravity with grip arbitration; optional gaze-directed TMD aim with the
  compatible PSVR2 Toolkit provider and existing tracked-hand fallback.
- Valkyrie physical insertion correction, plus TMD/right-weapon attachment
  corrections during movement, stick turns and head rotation.
- Polish Bink routing fixes for named and in-memory movie streams; native films
  remain mono. Unknown movie streams retain native selection.
- Context popup laser/cursor geometry correction; engine-cinema caption
  duplication in both eyes and mono-to-stereo bootstrap/frustum-fit correction.
- World scale 80 -> 76 (5% smaller), accepted by the user.
- Right-eye cinema LEFT-edge extension 2%, right edge unchanged. This is an
  explicitly requested reversible calibration; final headset border symmetry
  is still PENDING, not a proven compositor repair. Diagnostics/captures OFF.
- Repaired Valkyrie Vorbis allocation header, generated locally from owned game
  data. Final bank B8126A759169A89D92FA596D6FBE52E8D94F3BCBA0ACC24FD61967CAD87AFD7A.
  Encoded audio and native lengths/offsets are preserved; no XAudio DLL swap.
- Existing Seeker physical reload remains disabled. No test-only weapon grants.

- x64 gaze/trigger bridge: C4D002A3F09258F311EFF9CFB4FF20ABAD4CEB9CF3967E25A80212DFAF335271.
- Stereo cinema for engine-rendered cutscenes, gaze weapon-wheel selection,
  gaze-centred NIS sharpening and damage haptics are enabled in the preset.
- Scripted mutant-tackle aim assistance is enabled. The user accepted hitting
  and passing that scene in build A16FC192. This is not a fresh clean-install
  headset acceptance of every feature or every pending HUD/recenter case.
- Test-only Kasimov/Centurion grants are OFF. Owned native inventory remains
  authoritative. Seeker physical reload remains disabled.
- Existing HD weapons/textures, menus, weapon HUD and Polish text/dubbing remain
  unchanged; only the Valkyrie local-audio recipe is refreshed. Native Bink films remain mono;
  stereo cinema applies to engine-rendered scenes. The cinema calibration limitation is disclosed above.
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

This refresh verifies the signed catalog, every archive and changed setting,
core/producer suites, exact local audio reproduction, and scoped runtime/settings
transaction lifecycles. Earlier full RC7 lifecycle evidence remains historical;
a new complete all-media clean-install/headset pass is not claimed. Complete-ZIP
integrity and hashes are checked before publication. No Steam write or game launch is needed for
these checks. See installer-rc7-evidence.json for actual results.
