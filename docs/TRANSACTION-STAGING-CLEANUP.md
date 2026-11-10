# Transaction staging cleanup

Status: CPU fixture verified; desktop release/package NOT RUN.

Completed installations retained all transaction packages and prepared copies.
A read-only live audit found15.966GB in21 disposable staging directories. A
separate user-authorized cleanup removed those directories while retaining and
hash-checking925 original/rollback/receipt files. This source change prevents
the same accumulation in future install, repair, uninstall and recovery flows.

Under the existing per-installation transaction lock, cleanup visits only
GUID-named transaction directories and only their packages, prepared and restore
subdirectories. It validates path ancestry and rejects descendant reparse points.
Original uninstall backups, journal files, receipts and rollback snapshots remain.
Any pending recovery journal prevents all staging cleanup. After recovery commits,
obsolete staging is eligible again. Cancellation before apply also cleans staging.
A locked staging file logs a cleanup warning without invalidating a committed
installation; subsequent recovery/installation retries cleanup.

## Validation

Four production transaction tests reproduce retained staging against the original
core:0PASS/4FAIL. The corrected core gives4PASS/0FAIL, covering exact original
restoration across install/repair/uninstall, cancellation before apply, game-start
interruption with deferred recovery, and Windows file-sharing lock refusal.

The complete synthetic core suite gives111PASS/0FAIL, including real child-process
termination and durable recovery, ownership, INI preservation and rollback tests.
No game, Steam, OpenXR session or GPU was used. Large optional real-game hand
reconstruction fixtures were not selected. Local evidence is in the ignored
.build/cleanup-red-fixtures, cleanup-green2-fixtures and cleanup-regression-fixtures
directories. The first attempted green run reused the baseline incremental output
because copying the candidate source retained its older timestamp. The source was
retimestamped and recompiled before the recorded4PASS and111PASS runs.

This does not purge downloaded package caches, original backups, rollback history,
or unrelated temporary directories. It is not a remedy for native game memory
exhaustion and has not been published or installed over the user's installer.
