# Darkness II closed-wrist hands

Players received full arms because the installer never included the local geometry
modification. `darkness-assets.zip` reconstructs only the DLC H.Script cache/TOC;
the accepted first-person hands instead modify `Cache.Windows/B.Misc.cache` and
its TOC entry for `/D2/Characters/Camera/FPJackieArms_skel.fbx`.

Read-only inspection of the installed game on November 8, 2026 identified the live
B.Misc TOC entry at byte 341864, resource offset 1030291302, stored/unpacked size
206112. Its body SHA256 is
`ce0b8c981cc45af2d918603d482c69e3721ff68365c1594c28d3438a07acfe46`.
The stock compressed resource remains at byte 91129648, length 176296, SHA256
`70590e21d645601e7168cd31d56f54c14df6dea35ce56f97cc53186c231b44a6`.
Reconstructing that stock resource and applying the existing closed-wrist algorithm
produced **the identical live model hash**. This establishes the missing asset,
rather than inferring it from a DLL/config difference. The installed DLL is outside
this change; it and the runtime packages are preserved.

The transform code comes from Darkness2VR's existing
`.build-unarmed-on-installed/tools/hands_only_asset.py` and
`closed_wrist_mesh.py`. Only the preparation/geometry code is included here;
the standalone Steam installation/rollback commands are not used. No model, texture,
vertex buffer or compressed game resource is included in the public distribution.

All new installer Darkness II install/repair/manual export operations now generate a
local `hands-only` module automatically. It is independent of the optional VR fixes,
language and HD textures. The embedded worker extracts only the pinned stock H/B
resource through the already bundled QuickBMS, clips both wrists with closed caps
in all five LODs, preserves original textures, skin palette and cooked buffer sizes,
and requires the result to match the accepted local model hash above.

The worker reads owned game files or verified installer original backups. It writes
only its private workspace. The generated B.Misc cache recipe copies the complete
original prefix and appends the new body; only its resource TOC record is redirected.
If the original already contains the accepted hands, preparation produces a no-op.
Foreign replacement resource data or a mismatched stock compressed source is refused.
The regular transaction handles backup, verification, repair, failed-write rollback,
manual export and uninstall. Original local modifications are restored on uninstall.

The complete installer ZIP must be downloaded again. The signed catalog's Darkness
core version marker is advanced so existing installations show Update, but an older
installer executable does not contain the new reconstruction worker. Steam/gameplay
tests were not run for this packaging change; player headset acceptance remains a
separate validation level from the byte-identical asset and file lifecycle tests.

Validation: 99/99 core tests (including the embedded worker on the private clean
FP arms fixture; install, repair, repeat preparation, manual export, injected-failure
rollback and byte-exact uninstall) and 24 Python producer/layout tests passed.
The real installed game's already-correct model produces an empty append and
unchanged cache/TOC hashes. The installed game was only read during this work.
