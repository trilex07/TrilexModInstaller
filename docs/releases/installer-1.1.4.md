# Trilex Mod Installer 1.1.4 — Everybody's Gone to the Rapture VR (preview)

Status: **built, signed, verified offline and staged; upload pending `gh auth login`.** Rapture game state: `preview`
(headset acceptance of RaptureVR b1068cf pending). Owner approval for signing and publishing: given in chat on 2026-11-23.

## What changed against the published 1.1.3 ZIP

Base: published `Trilex-Mod-Installer-1.1.3-win-x64.zip` `E51CF867…02D5C7` (matches the installer-1.1.3 SHA256SUMS.txt).
`scripts/assemble-installer-1.1.4.py` extracts it, replaces and adds files, and asserts the exact change set:

- changed: `TrilexModInstaller.exe` (1.1.4, `RaptureGameAdapter`), `catalog.signed.json` (1.1.13), `README.md`,
  `RELEASE-NOTES.md`, `SHA256SUMS.txt`;
- added: `packages/rapture/vr-core.zip` (= `rapture-vr-core-20261123.zip`), `docs/RAPTURE-VR-20261123.md`;
- every other file (Singularity, Darkness II, Wonderlands Steam/Epic packages, tools, licenses) is byte-identical.
  .NET runtime pack is still 10.0.10, so the bundled runtime notices are unchanged.

## Build

- `Directory.Build.props` 1.1.3 → 1.1.4. Core / Tools / Tests / Desktop Release builds: 0 errors.
- The Desktop embedded resources come from `psvr2-client-runtime-20261109.zip` (release installer-1.1.0), downloaded
  publicly: `E4645FAE…096CAC`, equal to that release's SHA256SUMS.txt; `psvr2_toolkit_capi_loader.dll` `30A456CC…BC82D8`
  (same as the 1.1.0 resource fixture), extracted to `artifacts/psvr2-client-runtime/`.
- Self-contained single-file publish: `TrilexModInstaller.exe` `9608F6B0D5E31B569C3A47D24A83775D1AA9187D99EF1387AF00960ADD0B157B`.

## Catalogs and how 1.1.3 apps are handled

- `catalog-1.1.13.signed.json` `79A4783686EE5B592297585A96C123F82CC9F7BB3B7B23648C4E94300F382258`: published 1.1.12 games
  unchanged + `rapture` (`preview`, module `vr-core` `stable`, downloadUrl `…/installer-1.1.4/rapture-vr-core-20261123.zip`,
  SHA-256 `289AFE8F…BDF62A`, 7 854 362 bytes). `Tools sign` with `private/publisher.pem`, `Tools verify` against
  `catalog/trusted-publisher.pem`: PASS. This catalog is inside the 1.1.4 ZIP.
- **Old-app finding (evidence):** 1.1.3 `CatalogService.Verify` validates every game; `Paths.AllowedTarget` in 1.1.3 does not
  know `Bin64/…`, so the whole catalog is refused. Tools built from tag `installer-1.1.3`:
  `verify catalog-1.1.13.signed.json` → `InvalidDataException: Target is not a mod installation path`;
  `verify` published 1.1.12 → `Verified catalog 1.1.12`. In the 1.1.3 app a refused online catalog skips the update prompt
  (the prompt code runs after the catalog refresh) and silently stays on its bundled/cached catalog.
- **Decision:** the release's online `catalog.signed.json` is the **unchanged published 1.1.12 envelope**
  (`C7CD3B86…BF274`, same bytes as installer-1.1.3). 1.1.3 apps verify it as before and, because releases/latest is now
  `installer-1.1.4`, show the "update to 1.1.4" prompt. The 1.1.4 app loads its bundled 1.1.13 sidecar and keeps it over the
  older online 1.1.12 ("Kept newer verified catalog"), so it shows Rapture. Rapture is available only in the 1.1.4 bundle,
  or by importing `catalog-1.1.13.signed.json` in a 1.1.4 app.
- Consequence for later releases: any future online catalog that contains Rapture will be refused by 1.1.3 apps (they stay
  on their own catalog, no prompt). Publish such a catalog only once 1.1.3 users have had time to update, or keep the
  same pattern (online catalog without Rapture, Rapture catalog inside the bundle).

## Validation levels

- Compile/test PASS: Release builds 0 errors; `Trilex.Installer.Tests` 115/115 incl. "Rapture x64 adapter, Bin64 targets,
  panel art, install/export/restore and guard"; `tests/test_package_producers.py` OK.
- Offline structural PASS: assembly audit above; ZIP `testzip` and per-entry byte comparison.
- Portable smoke PASS: the bundled exe, with no installed .NET runtime, `--render-smoke` 8/8 rows; the screenshot shows
  1.1.4, "Publisher signature verified · catalog 1.1.13" and the Rapture tile.
- Not done here: real Steam install of Rapture through the installer; physical PSVR2 acceptance of b1068cf (owner).

## Release assets (`artifacts/installer-1.1.4/release-assets`)

| Asset | SHA-256 | Bytes |
|---|---|---|
| Trilex-Mod-Installer-1.1.4-win-x64.zip | 5F9B80AFA4CBBA5E4FD8833155BD45C8E069B75F9B7A625DD94D9F3E0F0CBD63 | 332 435 653 |
| catalog.signed.json (1.1.12, online) | C7CD3B86CA5777A32248F61C99CE99B49AA511E3533D38D7B899896A07FBF274 | 589 593 |
| catalog-1.1.13.signed.json | 79A4783686EE5B592297585A96C123F82CC9F7BB3B7B23648C4E94300F382258 | 613 778 |
| rapture-vr-core-20261123.zip | 289AFE8FE417E9F188B582B4EF8005A3A3394A0F0F73D068041045A908BDF62A | 7 854 362 |
| RaptureVR-test-20261123.zip | CEED67BC01E5869973D41A38999AA45A6D56864B364D6EF34797F0A63482F26B | 7 811 995 |
| SHA256SUMS.txt | (lists the five above) | 478 |

Release notes (PL/EN): `artifacts/installer-1.1.4/RELEASE-BODY.md` (also `docs/RAPTURE-VR-20261123.md` in the ZIP).

## Publish (after `gh auth login`)

`scripts/publish-installer-1.1.4.ps1`: pushes the branch, creates release `installer-1.1.4` (latest) on the pushed commit,
uploads the six assets, downloads them back into a fresh folder and checks every hash against SHA256SUMS.txt, then runs
`Tools verify-downloads catalog-1.1.13.signed.json … rapture … vr-core`.

## Upload result

Pending.
