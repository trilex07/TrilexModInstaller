# Trilex Mod Installer — visual direction

## Included artwork

- `01_app_screen_concept.png` — illustration/mockup for the desktop layout, NOT the implementation.
- `branding/Trilex_logo_master.png`, `Trilex_logo_512.png`, `Trilex_logo_256.png`, `Trilex_logo_128.png`, `Trilex_logo_64.png`, `Trilex_mod_installer.ico` — original conceptual Trilex brand symbol. App icons require small-size cleanup before release.
- `icons/00_icon_sheet.png` — eight original raster concepts.
- `icons/01_game_library.png` etc. — individual crops. Use in UI as reference or replace with vector/icon assets at production quality.

## Visual language

- Primary surfaces: near-black navy `#070E16`, secondary `#0E1A27`, cards `#142231`.
- Text: `#EAF6FF` (primary), `#9FB5C9` (muted).
- Cyan accent: `#17BFFF`, bright focus `#53DCFF`.
- Amber accent: `#F9A928` for very rare highlights.
- Success `#47CB8B`; error `#E66A73`.
- Avoid huge glowing borders on every element. Use subtler strokes in actual implementation, meaningful focus and selection only.
- Window target reference 1500 x 940 logical composition, fully resizable; maintain keyboard focus and sensible minimum size.

## Screens

1. Welcome & game library: Singularity selectable; Darkness II disabled coming-soon.
2. Installation discovery: detected path, validation, Browse override.
3. Options: Game Text PL/EN, Dubbing PL/EN independent, optional packs.
4. Review/Install: exact plan, download sizes, progress, stage-specific status, cancel.
5. Finished/Manage: verify, repair, update, restore/uninstall, launch.

**Copy correction:** The concept screenshot contains `Haptics`. Final production option must be named **`PSVR2`**, with tooltip describing adaptive triggers and Sense controller enhancements. This pack intentionally retains the original concept artwork so agents can compare it; the corrected copy above is authoritative.

## Legal/design note

The imagined sci-fi game cards here are original design mockups, NOT official Singularity/Darkness artwork or licenses. Replace with properly licensed assets or self-authored title/illustrations in a public release. Retain attribution for any third-party assets added later.
