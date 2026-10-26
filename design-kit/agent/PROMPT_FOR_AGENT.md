# TRILEX MOD INSTALLER — EXECUTION PROMPT FOR CODING AGENT

You are the lead product engineer, installer architect and UI developer for **Trilex Mod Installer** — a polished Windows desktop application for installing, updating, verifying and uninstalling independently developed VR game mods. Build real working software, not a decorative web-only mockup.

## Mission and repository boundary

Begin with **Singularity VR** from `https://github.com/trilex07/SingularityVR`. Architect the application so that **The Darkness II VR** and future games can be added later by registering a new game adapter and package manifest, without rewriting the installer. The Darkness II is **Coming Soon**, disabled until its mod is independently ready.

Work on a dedicated installer branch/repository (`TrilexModInstaller` if available or a clean new folder); do not rewrite Singularity's mod engine, touch live Steam installation, deploy experimental DLLs, or commit personal game files. Commit and push coherent milestones; provide instructions, build artifacts, and tests. Do not assume access to a user's private runtime assets.

## Visual language — MUST use ZIP assets

Inspect **all** files in this concept-art ZIP before implementing. They are visual references (raster mockups, not UI code or final interactive controls):
- `design/01_app_screen_concept.png`: window layout, game cards, detection, options and primary action.
- `branding/`: original Trilex symbol, PNG/ICO sizes.
- `icons/`: eight separate pictogram PNGs and source sheet.
- `design/BRAND_AND_UI_GUIDE.md`: color and typography direction.

Use dark charcoal/navy panels, subtle cyan emissive accents and rare amber emphasis, generous spacing, crisp typography, clear state indications, responsive window sizing, sharp DPI handling, proper keyboard navigation/accessibility. **Production UI label is `PSVR2`, NOT `Haptics`** (the concept screenshot predates the wording change). No faux status: never show “game detected” before actually detecting and validating a game. Do not accidentally use the image's fictional installation path as a default.

Recommended platform: **C#/.NET 10 WPF** (Windows desktop, self-contained distributable, no mandatory web runtime). Use native controls with an original visual theme, async download and progress reporting. Other Windows desktop stack is acceptable only if it demonstrably achieves better maintainability and performance. Publish a normal signed-ready .exe installer/build; produce a `zip` portable distribution if practical.

## User journey

1. Welcome/game library: **Singularity VR** enabled; **The Darkness II VR** and future titles shown as `Coming Soon` and non-installable.
2. Auto-detect installed Singularity via actual Steam library discovery (`libraryfolders.vdf` and app manifests), standard Windows location/registry hints where reliable, and optional other verified storefront adapters. Never hardcode a single Steam installation directory or fabricate app IDs. If not detected, offer **Browse** and validate the chosen root against required installed files and supported version hashes.
3. Language and voice **independently selectable**:
   - Game text/UI language: **Polski / English**.
   - Game voice/dubbing: **Polski / English**.
   - Installer interface language: **Polski / English**, stored separately.
   - Switching to English voice should normally preserve/use owned English assets already in the game; NEVER download or redistribute original commercial voice packs.
4. Module selection (with dependencies, statuses, size estimates and tooltips):
   - **VR Core** — required.
   - **Polish Text / English Text** — as appropriate.
   - **Polish Dubbing** — optional; ORIGINAL accepted localization or approved NEW voice-over release only, never confusing the two.
   - **Audio Remaster** — optional when complete and physically validated.
   - **HD Textures** — optional, only the reviewed compatible set.
   - **UI / HUD Remaster** — optional only where independently supported; VR-critical UI dependencies go into Core.
   - **PSVR2** — optional Sense controller enhancements / adaptive triggers and any verified PSVR2-specific integration. Do not disable essential OpenXR/SteamVR input when unchecked.
   - Future modules come from manifest, not hardcoded UI strings.
5. Review screen: selected features, voice/text choices, target folder, estimated transfer size, disk space and backup plan.
6. Transactional install: verify game closed; if ANY user game/session is active, never terminate it automatically; defer/write no files until safe. Download only selected compatible packages to a cache, validate integrity, preserve originals with hashes, install atomically with rollback on any error.
7. Final screen: exact installed versions, per-component PASS/FAIL, links to logs, buttons **Launch Game** (explicit user action), **Verify / Repair**, **Update**, **Restore Original / Uninstall**. Preserve user settings when upgrading.

## Online delivery — GitHub Releases

Do not use Git clone as an end-user delivery mechanism. Store source and metadata in Git; publish versioned, downloadable **mod-owned packages** as assets on GitHub Releases. Prefer a separate `TrilexModInstaller` release for the program and a versioned `TrilexModPacks`/game releases source (or an explicit namespace in one repo). Store a versioned `catalog.json` manifest with game ID, compatible native executable hashes/builds, downloadable components, dependencies, SHA-256 and exact destinations. Fetch over HTTPS, pin allowed repositories, use explicit release tags/immutable versions, fail closed on a changed manifest/asset hash, and verify both download and resulting installed file hashes. Add cryptographic manifest signing with a pinned public key for production; do not rely on a hash served from the same compromised location as a complete trust boundary. Cache downloads, support retries/range resume where feasible, cancellation, offline install from local cached packages, and informative errors. No arbitrary remote script execution or untrusted archive extraction.

GitHub Releases are good for V1; use different versioned asset names per package. Single release asset must be smaller than 2 GiB. For larger distribution, split into independently verified archives/chunks or use an alternative permitted binary host. Do not put large runtime binaries and assets in normal Git history.

### Legal/assets rule — CRITICAL

**Encoded/cooked Unreal game formats do not make original copyrighted game assets free to redistribute.** DO NOT include original or extracted commercial textures, voice files, sound effects, maps, meshes, fonts, original game executables, or repackaged cooked game packages in any public installer or release without express rights. This includes asset bundles which silently copy the game's original data.

Ship only code, config, UI/icon art and audio/textures with confirmed redistribution rights. Where modification of the player's original cooked game package is necessary, implement a local conversion/repacking pipeline using data read from the player's **own validated installation**, or review narrowly scoped patch/delta payloads for residual copyrighted content and legal permissions. Generate final game-format files **on the user's own PC** in a staging folder, using legitimate game-supplied originals and authorized mod inputs. Preserve package identities/cues, native audio formats and localization metadata as required by verified game tooling. Verify hashes and installed game versions before and after. Never claim that compression, obfuscation, `.xxx`, `.upk` or `.pak` extensions alone resolve licensing.

If a planned pack has unresolved provenance/permissions, DO NOT publish it; present it as `Coming Soon`/unavailable in the installer. Keep any private original-game fixtures excluded from repository, logs and ZIPs. New dubbing/audio remaster must not be offered as installable until the actual playable package is approved, SoundCue or native bank wiring is verified, and redistributable rights are confirmed.

## Singularity-specific packaging — INVENTORY EVERYTHING

Before implementing package emission, audit the current SingularityVR source, build outputs, last physically accepted runtime profile and deployment reports. Create a **complete inventory with source → target destination → ownership/rights → version hash → required/optional → release status**. Every accepted mod component must be accounted for, including:
- x86 `d3d9.dll` OpenXR/VR core and any necessary *mod-owned* renderer/texture bridge libraries;
- complete compatible INI sections (merge individual keys, never overwrite personal settings or blindly copy development diagnostics);
- menu/weapon wheel/HUD/localized atlases and mod-created UI assets;
- reviewed custom HD overrides, their original identity/hash gates and any local game-data reconstruction steps;
- PSVR2 Sense features/adaptive trigger prerequisites where ready;
- verified localization, approved dubbing replacements, and native audio bank/cue patching plans;
- shader/VFX helper binaries and any required redistributable runtime dependencies **only when distribution is permitted**;
- disable/restore handling, conflict/compatibility rules, verification tools and manifests.

Do NOT mark features physically accepted based only on code/build tests. In particular, existing TMD Age visual behavior was still failing the last reported PSVR2 test; Seeker and some final UI/audio remasters have unresolved/partial status. Package **last accepted features**, do not silently bundle experimental debug candidates or advertise unfinished packages. `VR Core` may include stable features despite optional unfinished features being hidden.

## Architecture requirements

Build three boundaries:
- `Launcher/UI`: game catalog, selection, localization, clear progress/error/reporting.
- `Package service`: release catalog, compatibility checks, dependency resolution, authenticated manifest verification, SHA256, download cache, safe archive handling, install plan.
- `Game adapter`: `IGameAdapter` with detection, version verification, file mapping, build-time/local transformations, config merge, running-process guard and post-install validation. Implement `SingularityGameAdapter` first. Add `Darkness2GameAdapter` later without changing generic installer logic.

Use manifest-driven module names and conditions; define per-game compatibility by validated executable/build hash, not game title alone. Prevent path traversal, zip slip, symlink escape, absolute destinations, unsafe overwrites, half-written DLLs or accidental edits to user saves. Store preflight manifest and backup in an app-specific state directory; use staging and atomic replace where practical; restore all touched files on failure. Upgrade/uninstall must know file ownership (mod-created vs original) and preserve the user's original directory, original files and any preexisting third-party mods. Warn on conflicts rather than overwriting an unrelated mod.

No automatic close of Steam or games; only user-initiated Launch. Elevate only when needed for the chosen install destination; do not request administrator rights for browsing UI. Privacy: no telemetry/uploaded game files by default. Provide human-readable install logs with paths redacted if shared.

## Development milestones and deliverables

**M1** Working WPF UI faithfully inspired by mockups, switchable PL/EN, real options model, disabled coming-soon games, responsive design. No pretend install success.

**M2** Complete game detection and validation + manifest models, a local fake-game fixture and offline sample mod package. Automated tests for multiple Steam libraries, nonstandard paths, wrong EXE, different binaries and manual Browse.

**M3** GitHub Releases catalog/download/updater, integrity checks, resume/cancel/cache and error handling. Tests with a locally served test catalog (no external download required during CI).

**M4** End-to-end transactional install, configuration merge, backup, rollback, repair, update and uninstall. Include process-busy protection and noninterference with another running game.

**M5** Singularity-specific acceptance inventory and packaging pipeline using only redistributable code/art and lawful local transforms. Demonstrate installing all **currently accepted** components into a CLEAN TEST FIXTURE; verify no leftover debug files or missing modules. Unfinished audio/Seeker/Age remain explicitly excluded or opt-in preview only when approved.

**M6** Release candidate: installer executable, source, checksums, publisher notes, README, reproducible package recipe, copyright/provenance audit, install/rollback report, automated test report, UI screenshots and example signed catalog.

## Required QA gates

- New Windows user profile + non-default Steam library.
- Installed game elsewhere / not installed / wrong version.
- PL text + EN voice; EN text + PL voice; PL/PL and EN/EN where permitted.
- Optional packs selected/deselected; disabled pack not downloaded.
- Missing/partial download; corrupt ZIP; wrong SHA; expired/incompatible manifest.
- Interrupted install halfway through: byte-exact rollback of every overwritten original.
- Repeat installation is idempotent; upgrade and uninstall are reversible.
- Do not modify a game while it or another Steam game is being actively played; never force-stop processes.
- Preview and app icon usable at 100%, 125%, 150%, 200% DPI.
- No redistribution of extracted/cooked original game assets or private EXE/saves/logs.

## Report progress

At every milestone, return: source commit/branch, screenshots or UI capture, build artifact SHA256, what actually works, what remains stubbed, tests, and exact GitHub release/manifest plan. Do not report a successful installer until an end-to-end fixture install, verify, re-run and rollback all pass. Keep Steam untouched unless the user explicitly authorizes an installer test against their real installation.

**Goal: One polished Trilex-branded installer that can eventually deliver the COMPLETE approved Singularity VR mod — code, UI, HD upgrades, localized options, optional audio and PSVR2 features — safely and repeatably, then expand to The Darkness II and other games via adapters.**
