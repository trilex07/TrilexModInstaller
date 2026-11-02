# Local reconstruction output mismatch

A screenshot with `Locally reconstructed output hash mismatch` is insufficient
to identify the failing file or establish Steam/GOG incompatibility.

`PackageExtractor.WriteReconstruction` checks the original source hash first.
Compressed sources also pass decoded length/hash verification before recipes
run. The reported output mismatch means a completed recipe produced bytes that
do not match its expected output hash. Do not disable integrity checks, accept
the resulting file, or recommend a store change on this evidence alone.

Both in-memory and streamed output verification now report the target-relative
path, declared source, codec, operation types, output length and expected/actual
SHA-256. No absolute user path or file content is added to these diagnostics.
Preimage validation, recipe behavior and transaction recovery are unchanged.

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
