# One installer, separate Wonderlands build payloads

Private implementation on `fix/wonderlands-build-routing-20261110`, based on
Toolkit recovery commit `83c692f`. No public catalog, release, game installation,
registry, Steam, OpenXR or user settings changed.

## Behavior

The signed `GameManifest` may now contain `buildProfiles`. Each profile has an
identifier, at least three executable-section RVA/length/SHA256 probes, and a
complete alternate module set. Module IDs, required choices and dependency
relationships must match the default set. Alternate packages undergo the same
target, ownership, distribution review, archive and payload integrity rules.

`PlanBuilder` verifies the reviewed EXE has not changed, selects the matching
profile before resolving selected modules, and passes those ordinary signed
modules through the existing downloader, extractor and transactional installer.
No local unsigned payload substitution and no two modules owning one DLL.
Multiple complete matches or only partial matches refuse package selection.
No match retains the legacy default package; an unknown whole-file fingerprint
still needs the existing explicit review consent. A changed hash alone does not
prevent the code probes from selecting the matching Epic payload. Runtime
per-hook byte checks remain necessary and unchanged.

The current `GameLauncher` already uses Steam only for a root whose local Steam
appmanifest actually owns that installation, and directly launches other roots.
Epic automatic folder discovery is NOT added by this patch; users can select
the existing game folder. Store selection must never depend only on folder
names or on the launcher EXE instead of the actual game EXE.

## Verification

- Release Core suite: 112 PASS, zero failures, including actual alternate-package
  installation and exact uninstall, unknown whole-file hash with matching code,
  partial/multiple profiles and a wrong archive rejected before payload use.
- Desktop Release build: zero warnings and errors.
- Separate read-only harness using the actual owned EXEs and an ephemeral signed
  catalog: Steam `EE33D5FB...CD46D` selected Steam; Epic `F12727FD...F43B5`
  selected Epic. Three 32-byte executable probes each: skin matrices, HUD paint,
  trace-channel function. Both original file hashes stayed unchanged. No game
  process or native code was loaded. The package identities in that harness are
  synthetic; this is not an actual Epic mod installation.
- Private receipts: `.build/build-routing-real/{probes,actual-results}.json` and
  tests' `bin/Release/net10.0/fixtures/test-report.json`.

## Release and runtime gates still open

This is the installer routing implementation, not a published combined release.
Build a matched Steam/Epic feature pair, freeze each DLL/archive hash, and put
their real package metadata in the publisher-signed catalog only after review.
The supplied Epic EXE alone cannot prove live ABI, Cohtml compatibility or actual
startup/gameplay. Full Epic runtime remains NOT RUN. Do not install its DLL into
the Steam directory or describe it as verified Epic support.

Old installers reject unknown manifest properties by design. Publish the new
installer before a public catalog uses `buildProfiles`; older pinned catalogs
without profiles remain compatible with the new installer. New profiles need
publisher signature verification, not an unsigned local configuration file.
