# Recovered VDXR startup and performance report (2026-11-17)

## Evidence scope

The player reports both x86 games recovered after clearing shader caches,
resetting resolution/INI settings and checking D3D9Ex/zero-copy. Several
variables changed together; none is established as the cause of the earlier
xrCreateInstance -32. Do not attribute recovery to a specific code fix.

Fresh JSON lists separate existing VDXR manifests/libraries for Registry32
(x86) and Registry64 (x64). The compatibility layer is registered only in the
64-bit view, with an existing x64 library. No XR overrides were set in the
collector process. This proves current inventory, not previous state or the
game process environment. Both game logs now show successful VDXR use:

- Darkness lines 148-150: instance, system and session success; lines 355-356:
  first game-layer xrEndFrame result=0. The previously failed startup gate is
  now passed in this supplied run.
- Singularity line 232: VirtualDesktopXR; lines 374-375: zero-copy 5640x2876;
  line 388: first headset frame; heartbeats show running=1, haveEyes=1.

## The supplied text is truncated, not a complete gameplay capture

Singularity ends after line 513 with a remaining-910-KB marker. Darkness
ends after line 633 with a remaining-380-KB marker. The alleged 199.4-ms
SolvePropertyLayout and 36.13-ms 'our work' records are not in these fragments.
No Wonderlands log or INI was attached.

The Singularity excerpt actually measures video: GameplayStereo -> VideoFlat,
one Bink fullscreen draw, zero world draws, 24.8 FPS in one window and 26.8
FPS in the cumulative controller bucket. Do not present this as a verified
25-FPS gameplay scene. Darkness reports ~69.4/72 layers per second at 72 Hz,
but world-bearing=0 and CinematicTheater video=1. Neither excerpt establishes
steady-state gameplay performance or a before/after regression magnitude.

## Counter semantics checked in our existing Singularity source

spikes/view_matrix/d3d9.cpp at 6f4acd6f0560c478b066140951af49d645d92e56:

- MISSED counts application frame intervals >1.5 times the runtime display
  period; it is not a queried compositor dropped-frame percentage.
- 'our work' is frame time minus measured buckets. It contains native game
  time and other unmeasured costs; it is not directly measured mod CPU time.
- 'gameplay hook breakdown' prints total milliseconds this window / WORST
  single call. The complete line is required to classify 199.4 correctly.
- SolvePropertyLayout returns immediately once g_upChildren is solved. It
  retries at most once per two seconds while unresolved. This run logs a
  validated layout at line 355. A genuine expensive solving call is a hitch
  lead, but does not by itself explain persistent ~40-ms frames after solving.
- [Render] TimeOurWork=0 in this player excerpt. Its opt-in value 1 directly
  times StereoPair and mesh matchers. The diagnostic adds per-draw QPC work;
  return it to 0 after one controlled capture. Gameplay hook timing already
  exists independently; enabling this is not required to see its peak.

## Wonderlands next evidence

Smooth benchmark vs stuttering gameplay is a useful within-machine contrast,
not proof of a GPU/driver or gameplay-hook cause. Existing code has a hands
refresh every two seconds and gameplay-specific pawn/weapon/HUD paths worth
measuring, alongside normal game streaming/asset work. No TTW runtime data
has selected a culprit. Existing [Diagnostics] HandRefreshTiming is gated by
Simulator.Enabled in this source: do not tell the player it will work on a
normal headset profile, and do not enable simulator mode on their machine.

Darkness effective source size is 3345x3410 with SS=125. Compared with its
2676x2728 base request this is 1.5625 times the pixels. This is not evidence
that the update increased their previous settings, nor a recommendation to
reduce visual quality as a permanent fix.

## Requested next capture (English text suitable for the player)

Please attach the original logs in a ZIP, rather than pasting them: the text
received here cuts off before the reported gameplay timing lines.

1. Singularity: the full view_matrix_3.log/view_matrix.log, plus its effective
   mod INI. Include at least 60 seconds in the slow gameplay scene after
   loading, not just the startup videos. If collecting a new profiling run,
   optionally set [Render] TimeOurWork=1 for that run, then restore 0.
2. Darkness II: full d3d9.log and Darkness2VR.ini after playing the slow scene.
3. Wonderlands: wvr.log and WonderlandsVR.ini from a benchmark run and a
   separate slow-gameplay run. Save/copy each log after closing the game,
   before the next launch overwrites it. wvr.log is normally next to the mod
   in OakGame/Binaries/Win64, with a fallback under LOCALAPPDATA/WonderlandsVR.
   Note the map/save, actions at the hitch, whether it happens while standing
   still, approximate interval, and Virtual Desktop refresh/quality settings.

Keep the working runtime and current shader caches unchanged during these
captures. No new game build, package, global setting, quality reduction or
cache purge is proposed from the incomplete evidence. Local game tests NOT RUN.
