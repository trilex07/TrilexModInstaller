# Installer 1.1.3: reported VDXR flatscreen startup (2026-11-17)

Status: later player logs confirm successful VDXR instance/session and frame
submission in both games. Earlier -32 cause and installer relationship remain
unproven. Performance follow-up is docs/PLAYER-PERFORMANCE-FOLLOWUP-20261117.md.
Local runtime NOT RUN.
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

## Player logs received (2026-11-17)

Darkness II reaches the successful native Present boundary, loads its x86
OpenXR loader, and attempts xrCreateInstance. Lines 146-148 show:
processBits=32 loaderLoaded=1; xrCreateInstance failed (-32); swapchain formats
result=-12 count=0. The first failure is XR_ERROR_FILE_ACCESS_ERROR (-32),
not XR_ERROR_API_VERSION_UNSUPPORTED (-4). Later invalid-handle (-12) and
missing eye pairs are downstream symptoms. The proxy hooked successfully;
the log does not support a missing proxy or disabled VR configuration.

Singularity's supplied run header says 2026-11-16 06:05:41. Line 224 reports
no OpenXR instance; heartbeat session=0/running=0 confirms no session. This
binary logs no numeric xrCreateInstance result. Do not assign Darkness's -32
to Singularity, or assume the two logs represent identical installation states.

The exact error mapping was checked against our OpenXR header and upstream:
https://github.com/KhronosGroup/OpenXR-SDK/blob/main/include/openxr/openxr.h

Upstream loader code returns -32 when opening a runtime or API-layer library
fails. Possible causes include a missing DLL/dependency, inaccessible file or
incompatible architecture. The player log identifies neither the file nor
which component returned the error. The OpenXR loader itself is present.
Examined mod sources already try API 1.0.34 before their current header version;
blindly changing API versions does not address the evidenced -32 failure.

Primary implementation references inspected:
- https://github.com/KhronosGroup/OpenXR-SDK-Source/blob/main/src/loader/runtime_interface.cpp
  TryLoadingSingleRuntime, library open failure.
- https://github.com/KhronosGroup/OpenXR-SDK-Source/blob/main/src/loader/api_layer_interface.cpp
  LoadApiLayers, library open failure.

Next: run tools/openxr-startup-report/Collect-OpenXR-Report.cmd on the player's
PC. This read-only inventory records both registry views, runtime/layer JSON
paths, referenced library existence, PE architecture and selected XR process
environment variables. It does not load libraries, detect transitive dependencies
or establish the game's exact environment. No elevation, registry edit, forced
SteamVR switch or speculative installer update is needed. A subsequent loader
debug trace may still be needed to identify the failed LoadLibrary and Windows
error. No player logs or binaries are committed.

Collector validation: parsed successfully and ran under Windows PowerShell 5.1
on the developer PC, producing valid JSON with both registry views. This is
tool validation, not evidence about the player's runtime configuration.
