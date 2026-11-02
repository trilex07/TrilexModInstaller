# Process guard: opaque active-game refusal (2026-11-02)

A remote screenshot reported `A game is active. Installation is deferred until it exits.` even after closing Steam. That screenshot contains no process identity; the remote blocking executable remains UNKNOWN.

The exact producer in `WindowsProcessGuard.RequireIdle` classifies any readable executable below `steamapps/common` as a potentially active game, except the existing SteamVR/fpsVR exceptions. This does not require Steam to be running. The installer itself was not exempt, so an installer unpacked below that directory could reject its own process.

Correction:
- Exempt exactly `Environment.ProcessId`, never a process-name wildcard.
- Preserve blocking of Singularity, other processes in the selected target, and the existing conservative other-Steam-app policy.
- Report blocker name, PID, executable path and classification. Keep the registry refusal separate.
- A failed executable-path query still cannot exempt an explicitly named Singularity process.
- Never terminate a process or alter Steam registry state.

Validation: 66 core/transaction regressions PASS; Release desktop build PASS, zero warnings/errors. Actual Windows `--check-idle` invocation from `.build/process-guard/steamapps/common/InstallerFixture/` with that same directory selected PASS. No game files written. This proves the self-block correction, not the cause on the remote user's computer.

Remote follow-up: first unpack the complete bundle into Downloads or another folder outside the game/Steam directories. If still blocked, supply `Open local logs` output (on the corrected build it identifies the process). Do not delete registry Running flags or disable the guard. Review an identified utility before adding any further exception.

Delivery: feature branch only. No release asset replaced by this change; previously downloaded RC6 remains unchanged.
