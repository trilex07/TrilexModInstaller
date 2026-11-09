# Run game busy-state correction

Confirmed call chain: `MainWindow.Launch_Click` invokes `vm.Run`, which sets
`Busy=true`. `Launch` previously checked `CanLaunch`, whose `CanManage` dependency
returns false while busy. Every normal button launch therefore refused with
`Verify a managed installation first`, even with a valid receipt and accepted
executable. This is unrelated to the player's unknown executable SHA.

The action now validates the actual receipt, game/root identity and executable
consent through `GameLauncher`, independently of UI availability. The UI remains
disabled during operations. The final on-disk executable hash and Steam manifest
routing checks remain enforced.

WPF regression exercises the actual `Run -> Launch` path for all three games,
in PL/EN across eight layout cases: valid unknown executable with consent launches
exactly once into an injected recorder; no receipt and changed executable refuse.
No game is started. Core: 106 PASS. WPF: eight layouts, 24 launch scenarios PASS.

Refresh the existing installer 1.1.0 ZIP only. Preserve catalog 1.1.6, all game
packages, the shotgun audio worker, PSVR2 clients and configuration byte-for-byte.
The player must redownload/extract the complete ZIP and use the new EXE. They may
also launch an already installed game through Steam; reinstalling the game is
not required by this button fix. Remote player launch acceptance remains pending.
