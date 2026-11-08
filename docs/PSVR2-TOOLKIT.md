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

These are direct downloads from BnuuySolutions, with byte length and SHA256 checked
before extraction. The experimental driver is the version currently recommended in
the upstream guide, verified November 8, 2026. No third-party fork is used. CAPIApps
(legacy IPC server and developer test utilities) are extracted into the installer's
local application data alongside the tools, and are not automatically launched.

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
The latter require a user headset test. Game runtime packages and the signed
catalog remain unchanged in the public installer refresh. The existing 1.1.0
download URL is retained; players must download the ZIP again for this feature.

Validation on November 8, 2026: 97/97 core regressions passed (including real
upstream ZIP hashes, extraction and driver installation in a synthetic app folder).
Self-contained Windows x64 publish passed with zero warnings/errors. The published
executable passed all eight WPF layout/language/DPI smoke cases with no binding
errors. No installed Sony driver or game files were modified, and no SteamVR or
physical headset calibration was performed.
