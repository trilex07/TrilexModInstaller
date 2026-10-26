# Delivery tracking — 2026-10-26

## Implemented

- Dedicated source repository/feature branch, supplied branding/icon, EN/PL WPF interface.
- Read-only Steam detection and compatible fingerprint checks; independent text/voice choices.
- Pinned signed catalogs, latest release discovery, verified cache/import, bounded redirects/resume/retry.
- Real transactional install, update, repair, uninstall, exact backups, journal and recovery.
- Local owned-original reconstruction, ZIP/content/provenance gates and offline package/signing tools.
- Complete accepted V7 metadata inventory: **164 file pins + 381 INI keys**.
- Self-contained win-x64 publishing and CI/draft-release workflows.

## Evidence

Core fixture suite: 37/37 PASS at initial delivery; actual child process terminated midway and recovered. WPF: 8 offscreen PL/EN layouts at 1024/1200/1440 logical widths and 96/144/192 DPI, binding diagnostics checked. See checked-in evidence JSON and screenshots; build script regenerates under ignored `artifacts/`.

These are offline fixture and own-app rendering results, not a real Steam installation, physical PSVR2 confirmation, real monitor DPI/input certification, complete Windows VM test or verification of GitHub Actions runs. Computer Use access to the app window timed out; no interactive PASS is claimed.

The published single-file EXE also passed all 8 offscreen cases with `DOTNET_ROOT` pointing to a nonexistent runtime and multilevel lookup disabled. Both workspace-directed native extraction and the normal extraction location were exercised. This proves startup of the self-contained bundle on this machine, not clean-VM compatibility. The build now repeats this bundle smoke automatically. The first sandbox-only attempt failed before app startup; normal extraction outside that restriction passed without an application code change.

GitHub Actions independently passed for source `144e9a6d76098bcda71566117f3c64f371f7415a`: [run 40346750583](https://github.com/trilex07/TrilexModInstaller/actions/runs/40346750583). Later commits require their own CI result; this receipt does not certify them implicitly.

## Release blockers

1. **Public game packages**: all seven catalog modules remain unavailable. Reproducible local generation/reconstruction and per-asset rights review remain necessary. The installer app can transact real signed packs; a complete distributable accepted V7 pack is not yet authorized/prepared.
2. **Production publisher**: secure the local generated signing key, establish production custody and provide an Authenticode certificate. Pipeline requires these secrets; it will fail closed if absent.
3. **Clean Windows + actual installer interaction**: verify keyboard/folder picker/scrolling, 100–200% real DPI and screen-reader behavior on a clean profile. Offscreen renders do not certify those interactions.
4. **Release integration**: after package gates, exercise actual published CDN downloads, all four text/voice combinations, managed install/update/repair/uninstall on a separate owned game copy. Only MAIN with explicit authorization may proceed to Steam. Never adopt the developer's untracked live installation implicitly.

The accepted Singularity V7 runtime remains frozen. No installed DLL/INI/game asset or running game was changed for this installer task.

## Short acceptance procedure

Open the built RC without starting a game. Switch UI EN/PL, resize to minimum, scroll both panels and check keyboard focus and independent text/voice selectors. Darkness II stays Coming Soon. Read-only Detect/Browse may validate an owned folder, but Review must explain unavailable packs and cannot write. Import a publisher-signed approved test catalog and test package against a disposable fixture to exercise Review → Install → Verify → Repair → Restore. Stop if any unexpected folder or privileged action is requested. Never use a synthetic EXE fixture to launch Steam.
