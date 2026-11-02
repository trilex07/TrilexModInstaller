# Local reconstruction output mismatch

A screenshot with `Locally reconstructed output hash mismatch` is insufficient
to identify the failing file or establish Steam/GOG incompatibility.

`PackageExtractor.WriteReconstruction` checks the original source hash first.
Compressed sources also pass decoded length/hash verification before recipes
run. The reported output mismatch means a completed recipe produced bytes that
do not match its expected output hash. Do not recommend a store change on this
evidence alone. Default installation still rejects this output.

Both in-memory and streamed output verification now report the target-relative
path, declared source, codec, operation types, output length and expected/actual
SHA-256. No absolute user path or file content is added to these diagnostics.
Preimage validation, recipe behavior and transaction recovery are unchanged.

## Explicit user exception

The desktop installer offers a per-file **Yes / No** warning when a completed
local recipe produces a mismatched output. **No is the default.** Yes authorizes
only that generated file, not a global or permanent checksum bypass. There is
no automatic approval for later reinstalls or repairs requiring regeneration.

The warning explains unverified compatibility, missing content/visual errors,
possible crashes, original backups and the limits of restore (no save rollback
or crash prevention). All archive/signature, original/decoded-source, executable,
path, ownership, size and active-game checks remain enforced. No failed source
or failed recipe execution is eligible for this output-only exception.

An accepted exception records expected and actual SHA-256, file, source,
operation, size and codec in `installed.json` and the local log. The installed
hash is the actual accepted hash. Post-write verification and restore still
require exact byte matches. The UI labels completion and later verification as
**with exceptions**, not publisher-verified output. External edits remain
protected from silent overwriting during uninstall. Keep installer backup data.

## Collect from affected users

- Installer version / complete ZIP release (e.g. RC6 or RC7).
- `installer.log`, available through **Open local logs**, or at
  `%LOCALAPPDATA%\Trilex\ModInstaller\installer.log` by default.
- GOG game version, game language and selected optional installer modules.
- Whether other mods or older Trilex installations are present.

Do not request full game packages or saves initially. Older installer logs may
contain only the generic message; the diagnostic build is needed in that case
to name the failed output. Never treat this diagnostic improvement as a GOG
compatibility fix. GOG runtime/install acceptance remains unverified.

## Regression

The core suite executes both `Reconstruct` and `ReconstructTo` with a valid
source but intentionally incorrect expected output hash. Both must reject the
output and identify target, source, operation, length and both hashes. A bad
source must still be rejected before reconstruction.

Transactional fixtures additionally cover acceptance/refusal, recorded actual
hashes, repair requiring renewed approval, byte-exact uninstall, a later refusal
before writes, cancellation and rollback after the approved output was written.
No Steam files or actual game process are used by these fixtures.
