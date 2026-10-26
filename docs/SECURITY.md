# Safety and trust boundaries

The installer runs asInvoker and never automatically elevates, kills a game, edits the game EXE, uploads telemetry or executes package scripts. Installer state and backups belong to the Windows user. Secure that directory; receipt/journal tampering by an already compromised local account is outside the signing boundary. Do not run untrusted manifests with an elevated developer tool.

1. Validate game path/fingerprint; refuse links/reparse paths.
2. Resolve only stable, approved modules from a pinned signed catalog, including dependency and language conflicts.
3. Verify exact download size and SHA-256. Keep incomplete `.part` separately. Offline imports must match the signed catalog.
4. Stage allowlisted content and verified local transformations. Check free space on both game and installer-state volumes, accounting conservatively for backups and temporary copies.
5. Acquire exclusive root-specific transaction lock; recover any previous incomplete journal first.
6. Back up original and immediate preimage bytes, flush the durable journal, recheck idle game and file preimages before each atomic replacement. Stream large payloads instead of retaining the campaign in RAM.
7. Verify postimages, commit receipt then journal. On error/cancel, restore exact preimages. A real child-process hard-exit regression proves recovery after process termination; it does not simulate every disk/power-controller failure.
8. If a game starts during a transaction, defer recovery until it exits. Recovery refuses externally edited postimages. Preserve its journal and backups; never delete them to silence an error.

Updates and uninstall affect owned paths only. Repair is explicit and preserves the first original backup. Three-way INI restore retains changed user settings, including across upgrades. Foreign DLLs are never silently replaced. An original backup's hash is verified before restoration. Keep the state folder until uninstall is complete.

Steam process guard reads registered running apps and process paths; SteamVR itself is treated as a runtime tool rather than a game. It cannot prove every protected/non-Steam process's identity. Multi-user simultaneous installers, network shares, junction-based Steam libraries and unsupported executable builds are not certified. Reparse paths deliberately fail closed. Running-game checks are repeated, but no user-mode guard can eliminate every race with a separately started application; atomic writes, journaling and conflict refusal bound recovery.

Known RC limits: no clean-machine VM acceptance yet; Windows input/screen-reader/manual DPI switching remains to validate; no production Authenticode certificate; no public stable game packs; per-user rather than machine-wide lock; journal/staging retention consumes disk until manually archived **after** verified uninstall/recovery. Manifest signatures establish publisher authorization, not creative ownership or physical VR correctness. Expired catalogs refuse installation. Release anti-rollback beyond signature/expiry is not certified. An authenticated expired bootstrap may populate the recovery UI, but Review/Execute refuse installation until a fresh signed catalog is loaded. The current bootstrap expires on 2027-04-26.

Dependencies are limited to framework libraries; no external NuGet app packages. Self-contained publishing embeds the official .NET runtime. See Microsoft's [single-file deployment guidance](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview). Do not disable HTTPS validation, Windows warnings or endpoint protection to install this RC.
