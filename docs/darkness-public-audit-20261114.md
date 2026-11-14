# Local Darkness II audit candidate 1.1.1

Base: installer f2693f0, public signed catalog 1.1.7. Candidate catalog 1.1.8
updates only the Darkness vr-core archive, module version and URL. Its DLL is
the optimized x86 build from Darkness2VR branch
codex/darkness-public-audit-20261114 (base 7aa2359f); loader and INIs are unchanged.

The public DLL used hardcoded Polish labels independently of the installer
selection. The new DLL uses PolishText. Darkness defaults to English even with
a Polish installer UI or after using Singularity's Polish preset. Existing
language recipes and the Polish fonts still follow the Darkness checkbox.
Public in-game switching of archives remains unavailable without the private
language profiles.

The installer reports installed-file verification separately from known or unknown
EXE SHA and headset/runtime coverage. It prompts before provisioning optional
per-user PSVR2 clients; installing the Toolkit driver and calibration remain
separate choices. Manual exports retain the optional separate client kit.
A newer valid bundled catalog is retained if the online catalog is older.

Validation: 107 core tests (short temporary fixture root to stay within the
existing frozen worker's Win32 limits); eight EN/PL offscreen UI layouts without
binding errors; signed catalog verification, archive payload hashes, unchanged
packages and embedded PSVR2 client fixture. Candidate ZIP: SHA256
666E6BCB017562729720A3906DBCF0AE0FC1B263BA9BB3CC95F1F9FAABC17C69.
No Steam, live client install, release publication, or physical headset validation.
The external flat-start incident remains open; collect the XRStartup lines from
the affected run before treating it as solved.
