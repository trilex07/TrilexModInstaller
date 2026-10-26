# Trilex Mod Installer — agent handoff mockup kit

This ZIP contains a prototype UI screenshot, a conceptual Trilex logo, eight split-out installer icons and an actionable engineering prompt. It is a mockup/resource handoff, NOT a shipping mod package and contains NO original extracted game content.

Quick start for a coding agent:
1. Read `agent/PROMPT_FOR_AGENT.md` first.
2. Inspect `design/01_app_screen_concept.png`, `branding/` and `icons/`.
3. Read `design/BRAND_AND_UI_GUIDE.md`; it overrides the obsolete `Haptics` label in the screenshot with `PSVR2`.
4. Use the `schemas/` examples as design sketches (placeholders, not ready-to-publish release manifests).
5. Build/install only into a test fixture until a real deployment is explicitly authorized.

Main technical premise: **The installer downloads versioned mod-owned packages from GitHub Releases, not copies of the game.** Original game assets stay with the user's legitimately installed game; game-format patching/conversion occurs locally only where legally permitted.
