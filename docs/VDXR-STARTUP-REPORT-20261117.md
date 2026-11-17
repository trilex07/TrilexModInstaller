# Installer 1.1.3: reported VDXR flatscreen startup (2026-11-17)

Status: unresolved player report; runtime NOT RUN, player logs requested.
Player uses VDXR and says both games worked before the update. Treat this as a
regression report, not proof that VDXR cannot run these 32-bit games.

## Fresh public-asset audit

Anonymous downloads of the current installer-1.1.3 signed catalog and both
referenced VR-core archives were inspected on 2026-11-17. Catalog version 1.1.11;
ECDSA-P256 SHA256 P1363 signature verified with catalog/trusted-publisher.pem.
Archive sizes/digests, ZIP CRC and every copied member hash match the signed
catalog. Singularity's system-derived dinput8_orig.dll recipe was NOT reconstructed
in this audit. No install/export lifecycle or actual game launch was run.

Catalog SHA256:
1a8044f20b92776b2347a28617ed6e08b71a1ca6cfef4286fa72c604ebb6b85d
Darkness archive SHA256:
c70507c405bb3ba3dec422a6116114a615bb862eaa08132ba7edb41d8ecc171e
Darkness d3d9.dll:
bb8e9570c955366acdcde4d8dfb0ac8ac56538b28a6d303836dd2778ba1ae79a
Singularity d3d9.dll:
e0e08775939176c090bdad8fec47c6ba81b3e34a8b288a7a4a80edce76253220

Both game proxies/loaders are PE32 x86. SingularityPsvr2Bridge.exe is PE32+ x64
as intended for that optional bridge. Darkness profile has DisableOpenXR=0,
SubmitMode=4 and simulator disabled. Singularity portable settings StartInVr=1,
ForceDesktopVr=0. These checks do not prove which files/settings the affected
player actually installed or which runtime DLL their process loaded.

The Singularity package still points to the reviewed installer-1.1.0 asset;
the latest Darkness-controls refresh did not replace that package. No broken
archive, accidental x64 game proxy or disabled-XR package default was found.
This does not rule out installer path/state handling, preserved user configuration,
system-derived dependencies, runtime selection or runtime admission failures.

Existing examined source paths explicitly try OpenXR 1.0.34 first. Consequently
no speculative switch of SDK/API version, loader or global runtime is proposed
without the actual startup failure. Source inspection alone is not proof of the
player's installed binary or loaded runtime. Do not force SteamVR based solely
on the game being x86.

## Next evidence

- Darkness II: last ~80 lines of %LOCALAPPDATA%/Darkness2VR/d3d9.log.
- Singularity: Binaries/view_matrix.log, same failed launch.
- Check timestamps first. No new mod log suggests proxy admission/load failure;
  a new log then distinguishes instance/system/session/graphics admission from
  mod mode/INI state. Preserve exact error codes, loaded paths and runtime name.
- Compare installed proxy, loader and effective INI with the signed manifest and
  the player's previous working copy. Check the exact game's registry/env runtime
  selection without changing it. Preserve the working rollback files.

No public package or installer was changed. The optional Pico stick-range work
is separate and does not modify OpenXR loaders, instance/session creation or
runtime selection. Private audit metadata is artifacts/vdxr-startup-audit/report.json;
downloaded binaries and catalogs are ignored artifacts and never committed.
