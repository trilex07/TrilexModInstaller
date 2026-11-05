# Darkness II review and Singularity optional Drive delivery

Reviewed `codex/darkness2-installer` through `e6c09ef34aafb74fd6064780de389ac5a5b12a4c`
against `release/rc7-singularity-bugfix-refresh` (`2f9d4ec`).
Review branch: `fix/installer-cloud-integration-review`. Steam/game untouched.

## Findings and corrections

- The Darkness checkbox reaches the existing transaction installer. The fixed
  Drive source is PNG artwork, not downloaded executable code. The converter
  requires 64 native weapon/tentacle targets and retains native mip layouts.
  Cache outputs have generated hashes and original backups; source ZIP contents
  intentionally have no fixed publisher hash. Signed runtime/module downloads
  retain their hash and length checks. This is not headset visual acceptance.
- Texture cache paths were allowed globally but missing from the per-game
  target classifier. A Singularity manifest could therefore name these Darkness
  paths, while a Darkness manifest containing them was rejected. Classify all
  six texture paths as Darkness-only. Regression covers both directions.
- Local conversion accepted ZIPs without the download validator. Run the same
  bounded PNG-only/path/link validation before copying original caches, including
  offline callers. Extraction now observes cancellation. A receipt from another
  game is refused before reading its original backups.
- The worker build collected only the QuickBMS text file. It now also collects
  the Python, NumPy, Pillow and PyInstaller license trees and the BMS script.
  Public distribution still needs matching QuickBMS corresponding source, as
  already required in the SIDE handoff. This review does not publish a bundle.

## Verification in this review

- Core: **80 PASS / 0 FAIL**, including the SIDE regressions and two review tests.
  `artifacts/cloud-review-tests/test-report.json`.
- Package producers: **15 PASS / 0 FAIL**.
- Drive catalog preparation: **2 PASS / 0 FAIL**.
- Texture worker rebuilt from pinned dependencies; full dependency license
  collection passed. No new real-cache conversion exercise was run here; the
  earlier SIDE real-cache/64-PNG result is a handoff claim, not re-established.
- WPF Release build: **0 errors / 0 warnings**.
- UI smoke: **8 PL/EN rows PASS**, no binding errors;
  `artifacts/cloud-review-ui/ui-report.json`. Existing smoke fixtures do not
  validate a released Darkness catalog or a physical game installation.
- Steam/runtime/PSVR2: **NOT RUN**. No installed files or release assets changed.

Darkness remains unpublished in the current signed catalog. Source integration
and a checkbox alone do not make the public release install Darkness. Final
catalog, package distribution and actual runtime installation remain separate
release gates.

## Singularity Drive migration

Upload these existing archives unchanged, not their extracted contents:

- `polish-dubbing.zip` (29,404,083 bytes), from
  `https://github.com/trilex07/TrilexModInstaller/releases/download/installer-1.0.0-rc.7/polish-dubbing.zip`
- `hd-textures.zip` (1,258,728,542 bytes), from
  `https://github.com/trilex07/TrilexModInstaller/releases/download/installer-1.0.0-rc.7/hd-textures.zip`

After receiving public file links, run `scripts/set-singularity-drive-packages.py`
with `--catalog`, `--dubbing-url`, `--textures-url`, and a fresh `--output`.
This produces **unsigned** catalog input, changing only those two optional
module URLs. It retains existing archive/source/output identities, dependencies
and rollback behavior. Sign with the normal publisher tooling and verify actual
anonymous downloads before release. Never distribute the unsigned input as a
signed catalog or mutate the signature envelope in place.

Only selected modules enter the install plan and downloader. Adjacent verified
packages/offline cache still work. To distribute an online/slim bundle, omit
these two optional ZIPs from that bundle; otherwise verified adjacent files are
used without a Drive download. Keep the complete offline ZIP as a separate
option if desired. New Drive URLs and anonymous download acceptance are
**PENDING user upload**; no placeholder links have been published.
