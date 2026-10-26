# Trilex Mod Installer — public source preview 0.1.0

Nieoficjalny, bezpłatny projekt fanowski Trilexa. Kod MIT; wymagane własne pliki gry.
To publiczny podgląd źródeł i lokalnych receptur audio, **nie gotowa paczka do
zainstalowania całego moda**. Opis po polsku i angielsku: docs/DISTRIBUTION-NOTICE.md.

Unofficial, free fan project by Trilex. MIT source; compatible owned game required.
This is a **source and local-audio recipe preview**, not an installable full-mod
release. The signed production installer workflow remains unchanged.

## Included

- Installer source at the exact tagged commit (GitHub source ZIP/tar.gz).
- Seven small JSON-only local audio recipes: Centurion, Kasimov, Valkyrie,
  shotgun, exploration music, boss music and ocean/wind ambience.
- Preview module manifest with exact owned-source/output identities (state stays preview).
- MIT license, distribution notice and SHA-256 sums for attached recipe files.

No game packages, recordings, dubbed performances, voice model weights,
proprietary extracted textures or executable installer are attached. Recipes are
intended for the reviewed source pipeline and exact source hashes; dropping this
ZIP into the game directory will not install the remaster. No public stable game
catalog or full-mod install capability is claimed.

## Evidence

- 55 core regressions and 9 producer regressions: PASS.
- Normal WPF and self-contained WPF smoke: 8 cases each, PASS.
- Seven sound banks reproduced with exact accepted hashes from owned originals.
- Combined offline lifecycle: 178 targets; 127 originals restored byte-exact.
- Hosted Windows CI for implementation/evidence commit b46482e: PASS.
- No Steam writes, no game launch, no physical PSVR2 installer acceptance.

The final dubbing's reference/performer permissions are not fully established;
it is excluded from this release. Software/model licenses are recorded separately
from reference/output provenance. See the distribution notice for details.

## Build

Use the tagged source and follow README.md. Development builds require Windows,
.NET 10 SDK, Visual Studio C++ tools and pinned Python build dependencies. Reviewed
original inputs remain local; they are not supplied by this release. The optional
fixed FFmpeg 9.0.2 upstream download retains its GPLv3 notices.

The tagged source retains production signature requirements and unavailable game
packs. No requirement to disable Windows security or accept an unverified binary.

## Optional developer-only offline audio verification

After building the tagged source, verify the attached SHA256SUMS.txt against each
asset, extract the recipe ZIP to `artifacts/audio-recipes`, then run:

```powershell
dotnet run --project src/Trilex.Installer.Tools -c Release --no-build -- prepare-audio-tools
dotnet run --project src/Trilex.Installer.Tools -c Release --no-build -- verify-recipes audio-remaster.module.preview.json artifacts/audio-recipes "D:\YourOwnedGame\Singularity" artifacts/audio-output
```

Use the actual owned installation root. The first command explicitly prepares the
pinned upstream codec; subsequent verification can use its verified cache offline.
The output directory must be fresh. The second command writes only inside the
repository's `artifacts/audio-output` and does not install or launch the game.
A source mismatch is a refusal, not a reason to disable identity checks. The
preview manifest is not a signed production catalog and must not be promoted by
changing its state locally.
