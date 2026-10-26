# Catalog and package contract

All JSON uses camelCase, UTF-8 and strict unknown-property rejection. See `Models.cs` for the executable schema and `catalog/catalog.json` for a complete current example. Optional fields have explicit defaults; schemaVersion is 1. Catalogs expire. Module states are stable, preview or unavailable. Installation accepts **stable** modules only, including dependencies; preview/unavailable data is informational. Required modules cannot be unchecked.

## Signed catalog

The envelope contains `algorithm: ECDSA-P256-SHA256-P1363`, base64 `payload` (the exact catalog bytes) and base64 `signature` (64-byte P1363 ECDSA signature over SHA-256 of those bytes). The application embeds `catalog/trusted-publisher.pem`; changing that trust pin requires rebuilding the app. Reformatting the payload requires signing again.

The currently embedded publisher key was generated locally for this project; its private half is ignored under `private/`. Back it up securely. It is not a Windows Authenticode certificate. Production must establish publisher key custody, certificate ownership and recovery before distribution. Never regenerate the key silently, ship private PEM/PFX, paste secrets into Git, or add arbitrary user-provided trust keys to the consumer UI.

Check updates reads GitHub's latest public non-prerelease release metadata only to locate `catalog.signed.json`. It then accepts only an immutable `/trilex07/TrilexModInstaller/releases/download/<tag>/<asset>` URL and verifies the pinned signature. GitHub metadata is not an authorization source. The verified envelope persists locally for offline use. Release redirects are bounded and limited to official GitHub asset CDN hosts. Private repository downloads are not implemented; the repository is public.

## Package identity and rights

Every stable module requires ID, labels, version, dependencies, immutable download URL, archive SHA-256, exact download bytes, expanded-byte ceiling and complete file list. Distribution review is either verified rights plus `rightsEvidence`, or a separate signed `publisherReview` with exact basis `fan-mod-publisher-approved`, evidence and notice. The latter does not set `rightsVerified` true; it records the publisher's explicit fan-project decision and is disclosed by the review UI. Missing/incomplete/unknown reviews still fail. See FIRST-EDITION.md. `localizedReasons` supplies optional PL/EN unavailable explanations.

Each file has a relative game `path`, archive `entry`, final `sha256`, `kind` and `ownership`. Paths are restricted to Binaries/RvGame; EXE writes, absolute paths, `..`, Windows devices, ADS and reparse ancestors are refused. All ZIP files must be enumerated, have no duplicate case-folded names or symlink entries, and fit the signed size ceiling. Nothing runs from the package.

- **copy**: newly authored or licensed bytes. Existing foreign files require an exact known `beforeSha256`, otherwise collision fails closed. Final bytes must match `sha256`.
- **ini**: `iniValues` maps `Section/Key` to strings. No whole user INI overwrite. Compatible modules may merge distinct keys; conflicting values fail. Original per-key values and whole-file backups are retained. User changes survive update/uninstall. Untouched files restore byte-exact originals, including encoding.
- **recipe**: ownership `local-transform`; mandatory exact `beforeSha256`; ordered `recipe` ranges `{source: original|payload, offset, length, entry?}`. `original` refers to the user's backed-up owned file. `payload` refers only to allowlisted mod data. Output is rebuilt into private disk staging and must match final `sha256`. Range length <=256 MiB, total output <=2 GiB; split packages as needed. No network scripts, arbitrary commands or executable patching.

Whole cooked `.xxx` copy operations are categorically refused. A recipe is not a license loophole: inserted voice data remains voice data, with third-party rights reserved and unverified provenance disclosed where applicable. Source-game compatibility and before hashes must be known; do not derive recipes from an unverified modded Steam directory.

`useCurrentSystemVersion=true` is permitted only for the complete local
`windows-directinput8-x86` copy to `Binaries/dinput8_orig.dll`. The compiled planner
binds it to the recipient's Windows SystemX86 file after PE32/x86 and Microsoft
version-identity checks; records its actual hash/length in the plan and receipt;
and the transaction verifies that exact hash again. It cannot redirect to another
path, DLL, codec or package payload. Game/runtime hashes remain pinned.

## Maintainer commands

```powershell
dotnet run --project src/Trilex.Installer.Tools -- keygen private/publisher.pem catalog/trusted-publisher.pem
dotnet run --project src/Trilex.Installer.Tools -- pack private/module.json private/stage artifacts/packages/module.zip
# Use generated module.zip.module.json for final sizes and archive hash.
dotnet run --project src/Trilex.Installer.Tools -- sign catalog/catalog.json private/publisher.pem artifacts/catalog.signed.json
dotnet run --project src/Trilex.Installer.Tools -- verify artifacts/catalog.signed.json catalog/trusted-publisher.pem
```

Keygen refuses overwrite. Pack accepts only manifest-listed files, fixes ZIP timestamps, then runs the same consumer extractor/content/hash audit. Upload only reviewed packages to an explicit immutable release tag. Sign the completed catalog after hashes/URLs/size are final, verify against the embedded public key, then attach `catalog.signed.json`. Manual production workflow requires signatures and creates a draft; promotion/publication is a maintainer action after acceptance.

## Accepted V7 inventory

Regenerate **metadata only** with:

```powershell
python scripts/inventory-v7.py ../SingularityVR-final-completion
```

`inventory/singularity-v7.json` accounts for 164 accepted pins and 381 INI keys, using the accepted profile and first-release baseline. It includes protected EXE/original dependencies as verify-only, not payload. All distribution gates remain explicit. The V7 manifest's 134 assets / 131 changed assets is a different count from the 164 runtime pins; none are silently assumed interchangeable. Native SoundCue/campaign packages must be reconstructed locally; native hand/knife/SVTG geometry and derived textures need their own generation/provenance step. Existing PSVR2 success does not settle redistribution rights.

Current outstanding package work: per-asset provenance; reproducible local generation recipes for original-derived runtime meshes and modified native packages; external runtime notices; approved voice/sound permissions; reviewed optional INI presets. Do not merely flip all modules to stable. Preserve the original `RvGame.xxx` and withheld impulse audio behavior.
