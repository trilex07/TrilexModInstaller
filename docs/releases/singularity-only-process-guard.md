# Singularity-only installation guard

The previous guard refused installation when any Steam app registry entry said
Running, or any unrelated executable lived under steamapps/common. Those facts
do not prove Singularity is using the files being installed. They can also affect
retail users who have Steam or VR tools installed for unrelated reasons.

The installer now blocks only the exact Singularity process name or an executable
whose basename is Singularity.exe, case insensitive. Its own PID is excluded.
Inaccessible executable paths still block when the process name is Singularity.
No Steam registry Running state is consulted. Other games/tools and the drive or
folder used to extract the installer do not trigger this process refusal.
No processes are terminated by the installer. Transaction locks, original-file
verification, backups, atomic file operations and rollback remain unchanged.

This does not establish compatibility of an unverified retail executable. Its
fingerprint/reconstruction checks remain authoritative and are not bypassed.
The original player's exact failure message was not provided, so the broad guard
defect is proven in code, but it is not asserted as the sole cause of that report.

Verification:

- Core suite: 66 PASS / 0 FAIL, including Steam and retail paths on C:/D:,
  missing executable path, exact process identity and unrelated Steam tools.
- Self-contained Windows x64 published EXE built against the unchanged RC6 signed
  catalog and existing local packages.
- Actual Windows process enumeration through the published EXE --check-idle:
  unrelated helper under a fixture steamapps/common directory allowed;
  synthetic Singularity.exe helper refused with its exact PID. Helpers are
  controlled test programs, not the game. No game launched or modified.
- No real D: disk, Sam's retail copy or his machine was available for testing.

The guard-fix complete ZIP reuses RC6 packages/catalog and bundled UE Viewer;
only the installer executable, guard-fix notes and SHA256SUMS change.
It does not bundle the newer SingularityVR gameplay candidate.
