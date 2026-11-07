# The Darkness II VR — installer candidate

Based on the Singularity RC7 bug-fix refresh. This is an offline candidate.
Existing signed RC7 catalogs still show Darkness II as unpublished. No game
installation, production catalog signing, or upload was performed.

- Select The Darkness II VR. Detect Steam AppID 67370 automatically or browse to
  an installation with DarknessII.exe and Cache.Windows. A structurally valid
  x86 PE32 with an unknown fingerprint can proceed after a warning (default No).
  The approval is tied to the exact directory and EXE SHA-256. Launch uses
  DarknessII.exe directly. GOG/disc runtime compatibility has not been tested.
- VR Core contains the user-accepted 2026-11-05 renderer (source e641c9a5,
  DLL SHA-256 622D75E5B58496652106A804EF674AC037F636B169F7104C4FF81781D088A32D),
  both profiles including the runtime switches (simulator off), and x86 OpenXR.
- Menu and shrine repairs plus DLC marker and font repairs are selected by
  default. Optional Polish text selects the Polish variant of the same menu
  and shrine package. No Polish voices or private in-game language switching
  helper. Change subtitle language in the installer.
- If a supported optional cache preimage is missing or differs, the installer
  asks whether to continue without that module. The same applies when an
  optional download cannot be retrieved. A conflicting unmanaged runtime
  file can be backed up and replaced after consent to its exact fingerprint.
  Signed package bytes, executable structure and recipe source/output hashes
  remain mandatory. Restoring or deselecting a module restores originals.

Build the release tools with scripts/build.ps1. Run
`python scripts/prepare-darkness2.py --help` for the packaging inputs. Inputs
include the pinned runtime, clean cache originals, accepted menu/shrine cache,
English and Polish language blobs, and reviewed font/script repair pairs.
The script repacks only Languages.cl in the accepted menu cache for each
language. It creates local reconstruction recipes for all cache targets and an
unsigned merged catalog under a fresh artifacts directory. Preserve the
Singularity packages; Darkness II ZIPs are namespaced under packages/darkness2/.
Do not distribute the private intermediate files. Review redistribution and
catalog expiry before signing.

For a large reviewed package, upload the final ZIP as a publicly downloadable
file and use the direct URL in its signed module:
`https://drive.usercontent.google.com/download?id=FILE_ID&export=download&confirm=t`.
Test the actual download without signing in. The installer refuses HTML,
foreign redirects and bytes that do not match the signed hash and length.
The offline package cache and pinned GitHub Releases remain available.
The catalog itself still comes from pinned GitHub Releases or a signed file.

Offline evidence: 77 tests passed and WPF compiled. The four real candidate
ZIPs were exercised against private copies of the original files: English
installation, Polish update, English update, verification after each, then
byte-exact uninstall. No live Steam/GOG/disc, headset, or Drive test was run.

## Optional HD texture installation

The Darkness II options contain **HD textures (weapons and tentacles)**.
Selecting it and pressing Install downloads the fixed publisher Drive ZIP,
validates its bounded PNG-only structure, and runs the embedded converter.
The converter reads owned H/B/F.Texture cache/TOC pairs and generates native
planar DXT mip streams using the established Darkness2VR comic-texture encoder.
The native texture dimensions and mip layouts are preserved; the source PNGs
provide the replacement artwork. It does not simply copy PNGs into the game.

The downloaded ZIP has no fixed publisher hash or fixed byte length, as
explicitly requested. New PNG artwork can replace the Drive file without
updating the installer. Keep the current 64 unique filenames and the layout
weapons/*.png and tentacles/*.png. Changes to texture identities or formats
can require an updated converter. No downloaded code is executed.

All six cache outputs are prepared before game writes. The existing
transaction engine backs up originals and records the generated output
identities for repair, deselection, rollback and uninstall. Locally generated
hashes track exactly what was installed; they do not pin the remote ZIP.
Original texture caches larger than 2 GiB are supported with bounded copy
chunks. New installs can continue without textures after an explicit warning
if download/conversion fails; repair failures stop to retain the existing
installation. Cancellation stops the converter process tree.

Build the embedded converter using scripts/build-darkness-textures.ps1
-InstallDependencies. Its AssetTools parameter selects a Darkness2VR tools
folder with quickbms/quickbms.exe and darkness2.bms. The portable distribution
must include the worker license notices, Python module sources and matching
QuickBMS corresponding source under its GPL terms before public release.

Offline real-cache exercise:
dotnet run --project tests/Trilex.Installer.HdExercise -c Release -- artifacts/FRESH
OWNED_TEXTURE_FOLDER SOURCE_PNG_ZIP
This writes only to the fresh artifacts directory. Original texture folder
contains the six clean H/B/F.Texture.cache/.toc files. No game is launched.

Validation (offline, 2026-11-05): all 78 core regressions pass; WPF builds
without warnings. The embedded worker converted all 64 actual source PNGs.
A real-cache fixture installed and verified all six native texture cache/TOC
outputs including the >2 GiB F.Texture cache, then uninstalled and restored
the exact six original SHA-256 identities. No Steam or headset acceptance
is claimed for the installer integration.
