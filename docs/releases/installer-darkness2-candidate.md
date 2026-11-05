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
  and shrine package. No Polish voices, private in-game language switching
  helper, or HD texture pack. Change subtitle language in the installer.
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

## Mutable 2K texture source download

The Darkness II card offers a separate **Download 2K texture source ZIP**
action. Only this hardcoded public Drive file can be downloaded without a
pinned hash or fixed size. Upload new versions to the same Drive file ID.
Keep the ZIP layout as weapons/*.png and tentacles/*.png. Download and
expanded-size limits apply; invalid files do not replace a saved ZIP.
This action saves source PNGs to a user-selected location and does not
install or extract them into the game. Installable modules still require
exact signed hashes and lengths.
