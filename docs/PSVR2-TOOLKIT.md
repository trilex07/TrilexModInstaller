# Optional PSVR2 Toolkit setup

After a successful managed game installation (including Download & install repairing
a damaged installation), the desktop installer asks whether to install PSVR2 Toolkit.
Declining leaves the game installed. Manual exports and the separate Repair action do
not trigger this offer. More options → PSVR2 Toolkit can run it again independently.
Prompts are available in Polish and English.

The implementation follows the upstream [installation guide](https://github.com/BnuuySolutions/PSVR2Toolkit/wiki/Installation).
It discovers the PlayStation VR2 App (Steam app 2580190) across Steam libraries or
asks for that app's folder. SteamVR, UnitySetup and processes in the app folder must
be closed. It never closes them automatically or modifies firmware.

Pinned upstream packages:

| Package | Version | SHA256 |
| --- | --- | --- |
| PSVR2TK-win64-Windows.zip | v1.0.0-experimental-3 | e3eca6151c422bd7896bbfd08025b48ceac90e7b20a7f3362b89613ac2dd9bd0 |
| CAPIApps-Windows.zip | v1.0.0-experimental-3 | 79df77c32355946db0c5d02a4ca045998b912e746c3f60098b372bf2b41ed933 |
| PSVR2Toolkit.UnitySetup-Windows.zip | v1.1.0 | 9fb240659188ae1c6549082592a8bd4943dd7ead689fe87278f4600647d62c7b |

The driver and calibration application are direct downloads from BnuuySolutions,
with byte length and SHA256 checked before extraction. The experimental driver is
the version recommended in the upstream guide, checked November 8, 2026. The CAPI
loader is now bundled from the pinned CAPIApps archive and installed at the actual
client locations described below. No legacy IPC server is needed or launched by
our direct-CAPI clients. No third-party Toolkit fork is used.

The Sony driver is preserved as `driver_playstation_vr2_orig.dll`. Existing originals
are retained. Each operation backs up all affected files to a unique sibling
`trilex-toolkit-backup-*` directory and verifies the backup hashes before changes.
A journal supports verified rollback and recovery on the next attempt; recovery
refuses externally changed files. Backups are retained for manual recovery. If the
PlayStation VR2 App folder requires administrator access, setup reports the access
failure; it does not silently elevate. The game mod remains installed.

After driver installation a second question offers eye calibration. On acceptance,
the installer downloads UnitySetup into its local application data, starts SteamVR
if necessary, waits up to 60 seconds for vrserver, and launches
`PSVR2Toolkit.UnitySetup.exe` with its own working directory. The user selects eye
tracking calibration in UnitySetup and performs it in the headset. Upstream v1.1.0
has no documented direct calibration command-line switch; no switch is invented.
Launching the tool is not treated as proof of successful eye calibration.

Validation must distinguish synthetic file transaction/download tests and WPF
build/layout checks from an actual Sony driver installation and PSVR2 calibration.
The latter require a user headset test. The existing 1.1.0 download URL is retained;
players must download the ZIP again to obtain the new embedded client dependencies.

Validation on November 8, 2026: 97/97 core regressions passed (including real
upstream ZIP hashes, extraction and driver installation in a synthetic app folder).
Self-contained Windows x64 publish passed with zero warnings/errors. The published
executable passed all eight WPF layout/language/DPI smoke cases with no binding
errors. No installed Sony driver or game files were modified, and no SteamVR or
physical headset calibration was performed.

## November 9: corrected game client deployment (catalog 1.1.5)

Confirmed failures in the public bundle:

- CAPIApps was extracted to a random installer directory while Wonderlands and
  Singularity used a fixed C-drive Steam path. Alternate libraries and clean
  installations could have a calibrated driver but no loadable client.
- Darkness II's DLL required `%LOCALAPPDATA%/Darkness2VR/runtime/psvr2/`, but the
  public installer supplied neither its x64 helper nor the loader there. The
  developer PC already had manually provisioned files, concealing this omission.

Every managed install/repair now verifies embedded, hash-pinned x64 clients and
provisions them transactionally, even if the optional Toolkit driver offer is
declined. This installs no SteamVR/Sony driver and changes no calibration:

- `%LOCALAPPDATA%/Trilex/PSVR2Toolkit/capi/psvr2_toolkit_capi_loader.dll`
- `%LOCALAPPDATA%/Darkness2VR/runtime/psvr2/psvr2_toolkit_capi_loader.dll`
- `%LOCALAPPDATA%/Darkness2VR/runtime/psvr2/Darkness2Psvr2Bridge.exe`
- Shared MIT notices at `%LOCALAPPDATA%/Trilex/PSVR2Toolkit/`.

Wonderlands gets the resolved per-user loader path through its existing
`[Haptics] ToolkitLoader` setting, before normal managed INI hashes are recorded.
Singularity's x64 helper now falls back to that location if its requested loader
is missing; existing explicit loader paths retain priority. Its x86 renderer,
Darkness II renderer and Wonderlands renderer remain byte-identical to 1.1.4.
No gaze direction, smoothing, trigger or gameplay setting is changed.

Clients use an exclusive lock, verified backups and a persistent recovery journal.
All three games and helpers must be closed before writes. Recovery refuses external
changes. Clients are intentionally shared and retained on individual game uninstall.
Manual exports carry `psvr2-clients/README.txt` and the same pinned files separately
from the game-root payload; the user must install these for the target Windows
account and resolve Wonderlands' loader path on that account.

To update: redownload the complete 1.1.0 ZIP, extract it into a new folder, close
games, then use Update/Reinstall for each installed mod. Users with older Toolkit
drivers can use More options -> PSVR2 Toolkit to install the pinned matching driver
and optionally calibrate. Installing clients alone does not prove driver activity,
valid gaze samples or headset acceptance.

Source evidence and binary provenance: `docs/PSVR2-CLIENT-FIX-20261109.md`.
