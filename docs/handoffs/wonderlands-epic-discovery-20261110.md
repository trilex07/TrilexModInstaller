# Epic game-folder discovery

Private follow-up to signed payload routing `db3ce1f` and Toolkit recovery
`83c692f`. Wonderlands' adapter now searches both the existing Steam library
manifests and Epic's `%ProgramData%/Epic/EpicGamesLauncher/Data/Manifests/*.item`.
The selected actual game EXE then drives the signed package profile selection.
End users do not need a separate installer executable for Epic.

Epic records are location hints only. `InstallLocation` must be absolute. The
adapter verifies the actual `OakGame/Binaries/Win64/Wonderlands.exe` plus game
content directory, hashes that EXE and retains normal unknown-build consent.
It does not execute or trust the record's `LaunchExecutable`, copy a game EXE,
or infer build addresses from the store name. Duplicate roots are collapsed.
Malformed, inaccessible, oversized and missing records are ignored, with a
2048-file/256KiB-per-record bound. Manual folder selection remains available.

Verification: full Release Core suite 113 PASS / zero failures; Desktop Release
build zero warnings/errors. New actual filesystem fixtures cover valid and
duplicate records, unrelated/relative/wrong-type/oversized/malformed records,
missing manifest directory and unknown fingerprint preserving consent.

No full Epic installation exists locally, so actual Epic launcher startup and
mod gameplay remain NOT RUN. The complete real Steam/Epic package catalog is
not published; game candidates still need matched features, explicit frozen
package hashes and the runtime gates in the routing handoff.
