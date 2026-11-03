# RC7 Singularity bugfix refresh

Version/tag stays `installer-1.0.0-rc.7`. Installer source is
`7e52a2788e91b6e156eb7a842a522afcfafffa8f`. This refresh does not redesign the
installer. Its EXE is rebuilt to embed the repaired local Valkyrie audio worker
and current signed catalog. Consent, source checks and recovery remain intact.

## Exact identities

- Complete ZIP: `B3BBD6502C385B47F2080B81B4AA4DFF0B93639E536A971170CA9934CC3D4FBD` (1,464,076,895 bytes).
- Installer EXE: `B32DA5400BB99B91CE691194E132BC211B7E4E0FA5C5047A830583ADD526C1CC`.
- Signed catalog: `547238B32B9FB58833255841BA550A268B03C6BE9AB242F54BB4E9DD8D413CD5`.
- Runtime source: `9488a26a508c1999edc2c4613fe39a9b0d326eb9`.
- x86 runtime DLL: `7AD356F832ED84E2F599733BE58553BD661381C6C2FD51B4ACDEE25116F38E86`.
- Locally reconstructed Valkyrie bank: `B8126A759169A89D92FA596D6FBE52E8D94F3BCBA0ACC24FD61967CAD87AFD7A`.

## Completeness audit

Compared every catalog copy/recipe destination with the installed accepted
content. Only DLL, Valkyrie bank and preset differ. Nine other module archives
are byte-identical. Existing HD weapons/textures, wrist/weapon HUD, detailed
wheel, menus, Polish localization/dubbing and Seeker HD remain included.

The DLL contains all intervening fixes since A16FC192: L1 gravity arbitration,
Valkyrie insertion, TMD/right-weapon attachment during movement/head/stick turns,
gaze-directed TMD with fallback, named/in-memory Polish Bink routing, context
popup pointer geometry, binocular captions and cinema bootstrap/frustum fit.
Preset adds only WorldScalePct=76, TmdGazeAim=1 and right-eye left extension=20
permille. Cinema capture diagnostics remain OFF. No test grants; Seeker physical
reload remains OFF. The Vorbis repair preserves encoded packets and allocation
lengths and is selected only by the exact new signed Valkyrie output identity.

## Verification scope

- Core regressions: 75 PASS / 0 FAIL.
- Producer regressions: 15 PASS / 0 FAIL.
- Published self-contained EXE: eight PL/EN layouts PASS, zero binding errors.
- Signed catalog and all 12 archive hashes/CRCs: PASS.
- Every complete-ZIP entry CRC/SHA: PASS.
- Actual packaged audio worker reproduces installed B812 bank exactly: PASS.
- Three changed modules: install, verify, reinstall, damage, repair, uninstall
  PASS; seven original banks restored byte-for-byte. Fixture does not load DLLs.
- Full all-media clean-install lifecycle not repeated; older RC7 coverage is
  historical. No Steam writes or game launch during packaging.
- User accepted weapon attachment, world scale and binocular subtitles.
  Right-eye 2% edge extension remains physical PENDING. No 90Hz claim.

Publication replaces the existing seven changed release assets. Nine unchanged
module assets must retain their previous GitHub digests. Remote verification
receipt is retained privately with the release artifact evidence.
