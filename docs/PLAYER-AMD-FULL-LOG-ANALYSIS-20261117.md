# RX 7600 / Xeon / Pico 4: complete-log analysis (2026-11-17)

Scope: Darkness II and Singularity only. Wonderlands explicitly deferred by
user: player has not retested its newer changes. No Wonderlands code changed.
Input files were read as evidence, not executed or treated as instructions.
No game launch, deployment, cache deletion, runtime change or installer update.

## Inputs and reproducible extraction

Downloads/view_matrix.log: 959106 bytes, SHA256
587cc3c01bbfb2913e0b4dd51fb615b2117bfc32cb834c2f9f2e0832a7f1d96b.
Downloads/d3d9 (1).log: 429992 bytes, SHA256
b77dd4c6b09caf14e45f69d523c72c9d6100eb22b71749119e604e91f671321f.
Downloads/OpenXR-startup-report.json: both VDXR DLL architectures registered
and present; this inventory does not establish their previous state.

Run tools/analyze-player-vr-perf.py with --singularity, --darkness and --out.
Private receipts/CSV/JSON are artifacts/player-amd-full-logs-20261117/.
The parser produced 46 Singularity interval records and 68 Darkness cadence
records. These are unweighted samples, not a matched before/after benchmark.
Mode at report time can differ from the mode earlier in a measurement window.
Raw player logs/configuration are not committed.

## Singularity: startup hitch and steady play are different questions

- Lines 543-544: first hook window mean 1.80 ms/frame, worst block 213.36 ms;
  SolvePropertyLayout total/worst 199.4/199.4 ms. The player's interpretation
  of that peak is correct. This occurs in VideoFlat startup, not steady play.
- Lines 657/670: 27.5 FPS and residual 'our work' 36.13 ms remain in VideoFlat,
  while gameplay hooks are only 0.02 ms. This is not 36 ms of measured mod CPU.
- GameplayStereo starts at line 3491. The first report is 30.6 FPS (transition,
  4577 peak draws / 1843 split). Subsequent 20 gameplay reports are 53.2-72.2
  FPS. All 21 gameplay samples have median 63 FPS. Later examples include
  72.0 FPS, and 54.7 FPS at 2394 draws (line 5313).
- Hook cost is typically ~0.03 ms, with one subsequent measured block peak
  67.11 ms (line 3789). Its listed four subscopes do NOT account for that
  peak: Solve 0/0, armed 3.0/0.0, latch 0.1/0.0, muzzle 8.1/0.7 ms.
- The current source's enclosing hook block also calls ReportEyeHeight and
  NativeRifleFx::Install without individual timing. The native rifle hook
  arms at line 3741, inside the 67-ms window. That is a testable lead, not
  proof it owns all 67 ms: CPU wall also includes scheduling interruptions.
- Zero-copy transfer ~0.05-0.06 ms, XR submit ~0.15-0.17 ms. Existing fence
  waits can be several ms; they are CPU waits for GPU completion, not a GPU
  Busy measurement. A later sample reports 7.14 ms while input actions cost
  only ~0.06 ms total per frame. No evidence for Pico input as the main cost.
- MISSED is cumulative across modes and counts application frame intervals
  >1.5 display periods. Early ~99% does not describe steady gameplay or a
  measured compositor drop rate. At line 7043 the mixed cumulative bucket
  is already down to 15.6%, still including the earlier startup.

The one-time solve peak is real and worth reducing separately. It is not a
credible explanation for sustained 25 FPS, which this gameplay segment does
not show. A previous working same-scene baseline is absent, so no regression
magnitude or driver vs mod attribution is established.

## Darkness II: sustained world throughput is the larger remaining lead

Instance/session, xrBeginSession and first world-layer submission succeed.
55 cadence windows whose world rate equals their layer rate show
33.9-56.0 world-bearing layers/s, median 43.5, at 72 Hz. This is world-bearing
XR submission cadence, not independently measured simulation/headset FPS.

Across all 134 available StereoPerf windows (including startup/cinema),
right+restore CPU wall median is 11.195 ms, maximum window mean 15.03 ms.
Gameplay examples at lines 4094-4192 show ~11-14 ms and GPUFrame spans ~22-25
ms. The spans contain submission gaps; GPU Busy and CPU bottleneck are not
separated by this log. Main-thread transfer join mean per job is ~5-7 ms in
these windows, whereas worker body wall is ~0.1 ms. These overlap the right
segment and must not be added as independent frame costs.

Current FinishStereoTransfer includes residual D3D9 producer-event drainage,
publishing the D3D11 packet, and Pending::Finish in that single join number.
The new default-OFF diagnostic splits those scopes; it changes no ordering,
query flags, thread ownership, copies, waits or rendering behavior.

NIS pair GPU dispatch median 0.312 ms, sample max 0.397 ms, cumulative peak
1.483 ms. Removing sharpening is not the first justified target for ~20-25
ms frames. SS=125 / source3345x3410 is 56.25% more pixels than 2676x2728, but
no prior same-scene/settings comparison shows it changed or caused regression.
No permanent quality cut, API change, cache purge or zero-copy disable proposed.

## Diagnostic candidates, not optimization fixes

Darkness source branch codex/darkness-player-perf-20261117 adds
[Diagnostics] StereoTransferTiming=0 (default). With 1, logs mean/peak
producerDrain, publish and workerFinish per successful job, at the existing
240-job reporting cadence. No new GPU waits. workerMean overlaps the sum.

Singularity source branch codex/singularity-player-perf-20261117 adds
[Diagnostic] GameplayHookTimingExtra=0 (default), timing the two missing
helpers only when enabled. [Render] TimeOurWork=1 separately times draw
processing and mesh matching in the existing binary. Both are tooling:
a successful build is not an FPS improvement or remote AMD runtime proof.

Keep prior optional Pico stick-range calibration at 0 for isolated trials.
No candidate has been installed, sent as a release, merged or put in installer.
The player's exact DLL hash and effective INI must be compared before replacing
working files; preserve the recovered setup and rollback. Run matching fixed
scene/profile captures with timing OFF/ON and repeat OFF to detect probe cost.
Do not combine a diagnostic with resolution, cache, driver or runtime changes.
