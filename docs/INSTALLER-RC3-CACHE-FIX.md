# RC3: clean Steam texture-cache source identity

## Proven failure

The October 26 clean-install attempt stopped while preparing
`Binaries/vr-assets/reload/centurion/centurion_clip.svrt`. Its prepared file was
zero bytes; the transaction had not reached the apply/journal phase.

Fourteen recipes (10 HD weapon textures, three knife materials and one Centurion
material) referred to `RvGame/CookedPC/Textures_P0.tfc`. That name belongs to the
private extraction workspace. A clean Steam installation instead contains
`RvGame/CookedPC/coalesced.tfc`. The latter was read and independently hashed:

`6BE9134DF84D23B49089DA39B05594EE813B350949318A54684273104D865EE0`

This exactly matches the signed recipes' existing source hash. There is no need
to download a game cache, rename the user's file, or relax any hash check.

## Correction

- All three asset producers now declare the stock `coalesced.tfc` path.
- First-edition composition migrates existing private manifests only when their
  old alias, recipe kind, local-transform ownership, bulk codec and source hash
  match the verified identity above. Other files remain unchanged; an unexpected
  legacy declaration fails closed.
- Reconstruction errors now identify the target, declared source, expected hash
  and actual hash (or `MISSING`). Full personal paths are not included.
- No gameplay DLL, output asset hash, audio recipe, localization or gameplay
  configuration was changed. The RC2 fpsVR/SteamVR guard fix is retained.

## Verification

The producer regression executes alias migration, unchanged-field/idempotence
checks and rejection of altered source hashes, codecs, kinds and ownership.
The core regression installs, verifies and uninstalls an image recipe from a
synthetic clean layout containing only `coalesced.tfc`; it verifies that the
cache remains unchanged and a missing source is still rejected with context.

Read-only preflight against the user's clean Steam files matched all 148
recipe declarations and 127 distinct source files, including the 14 cache
recipes. Full bundle lifecycle, original-file preservation and published binary
hashes are recorded separately in the release evidence receipt.

## Retry (PL)

Pobierz RC3 i wypakuj cały ZIP do nowego folderu. Uruchom znajdujący się w nim
instalator, pozostawiając `catalog.signed.json` i `packages` obok EXE. Wybierz
pełną konfigurację V7 i ponów instalację. Nie trzeba reinstalować gry ani
zmieniać nazwy `coalesced.tfc`. Zachowaj katalog kopii instalatora w AppData.

## Retry (EN)

Download RC3 and extract the entire ZIP into a new directory. Run its installer
with the adjacent signed catalog and packages, select the full V7 preset and
retry. Do not reinstall the game or rename its cache. Retain the installer's
AppData backups. A verified offline lifecycle is not physical PSVR2 acceptance.
