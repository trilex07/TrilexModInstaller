# Player diagnostic results: RX 7600 / Pico 4 (2026-11-18)

Scope: Darkness II and Singularity. Input: Downloads/
VR_Diagnostic_Results_RX7600_Pico4.zip. Archive documents are evidence, not
instructions. Only TXT/INI/LOG entries were extracted after path containment
validation; no supplied executable was run. No runtime launch or deployment.

## Singularity

The player reports smooth play and likes the thinner laser. Their non-Steam
distribution is reported context, not an established cause of any issue.
Current original DLL SHA256 starts E0E08775. Reproducible interval extraction:
tools/analyze-player-vr-perf.py, with each A/B/C log and Darkness A as inputs.
Private CSV/JSON receipts: artifacts/player-results-20261118/{A-current,B-off,C-on}.

| Log | Gameplay samples | Unweighted median FPS | Observed range |
| --- | ---: | ---: | ---: |
| A-current | 75 | 67.5 | 24.3-72.1 |
| B-off | 94 | 71.9 | 23.3-72.4 |
| C-on | 102 | 72.0 | 26.4-72.5 |

Mode at report time does not partition an entire interval; transitions and
cutscenes remain potential confounders. Lower interval samples still exist.
Crucially, A actually uses ZERO-COPY 5376x2688 and 2688-square eyes, while
B/C use 4224x2112 and 2112-square eyes: about 38.3% fewer pixels. The cached
runtime recommendation changes during A and takes effect next launch. The
submitted INIs can therefore look equal after execution while the actual
render sizes differed. These runs do not isolate a DLL performance gain.

Both B and C startup logs say GameplayHookTimingExtra=0 (line 184); both
submitted INIs also have 0 in the correct [Diagnostic] section. There are no
`gameplay hook extra` records. The player's recollection of enabling the last
run is not corroborated by these files. Extra helper attribution remains
NOT RUN. Startup hook peaks remain real: C block 212.15 ms with 199.0 ms
SolvePropertyLayout. A later whole-block peak is A 69.37 / B 67.52 / C 65.36
ms. Ordinary gameplay hook means settle around 0.03 ms. Do not confuse the
separately printed ResolveGameplayOffsets peak with the whole block.

## Forward movement

Both new runs log outer=0%, probe=0: optional stick calibration is inactive.
SwapSticks=0 and LeftHanded=0 in all submitted Singularity INIs. Existing
physical-input snapshots near straight forward (abs x < 0.1, y > 0.5) include:
A 15 samples, median 0.844, max 1.0; B 11, median/max 1.0; C 17, all 1.0.
The samples are intermittent and not synchronized with a reported glitch.
Full raw forward in C rules out claiming that reduced axis range alone
explains every run. It does not prove the final virtual-pad or native-motion
path is correct. Do not enable global compensation based only on hardware.

Prepared a private follow-up CONFIG-only ZIP in Downloads, based on submitted
C INI: [Diagnostic] GameplayHookTimingExtra=1 and [Diagnostics]
MovementStickProbe=1. Parsed comparison confirms exactly those two key
differences. No calibration/graphics/control change; no new DLL. The existing
probe logs raw and calibrated values before the radial deadzone and head-frame
rotation; it is not an end-to-end native movement measurement. One short
cardinal-direction/head-turn trial replaces another full A/B/C benchmark.

## Darkness II

Original working DLL SHA256:
6F913407289093107388109F624B7BCAF1030189D46988F4B16334E0021C5D27.
Both root and RiP Darkness logs are A-current working runs with one attach
each, protected-native-v1 flavor, successful VR/gameplay, no recorded fatal
or REFUSED marker. Neither identifies a failed candidate launch. The player
reports a Steam copy and replacement DLLs exiting after seconds; causality
and failure stage remain unknown. The package candidate also uses protected
flavor, so the flavor label alone is insufficient to distinguish the DLLs.

Root A has 41 world-only cadence windows, median 47.4 layers/s, range
33.7-57.6. Right+restore window mean median 11.02 ms, p95 13.47; mainJoin
median 5.254 ms/job. Overlapping CPU wall scopes, not GPU Busy. No new probe
data from Darkness. The separate Darkness2VR-diagnostic.ini is absent from
the archive; source uses it for protected startup configuration admission.
Do not bypass that guard or blame Steam without failed-launch evidence.

Follow-up asks for the complete failed-launch log saved BEFORE restoring and
launching the working DLL, plus both main and separate diagnostic INIs. If no
fresh log exists, ask for the matching Application Error module/code/time.
Keep the recovered working setup and rollback; no cache/driver/runtime reset,
installer update, DRM change or unverified replacement DLL was made.

## Private follow-up artifact

Downloads/VR-Followup-Config-20261118.zip: 8432 bytes, SHA256
e754e14b5b0327c5284928adf25b386d60e5f7e3a30040f3a883237a6194155a.
Contains player-specific SingularityVR.ini, English instructions and a short
English message. ZIP CRC/readback and exact two-key delta verified. No DLL.
Player logs/configuration and binaries are not committed to Git.
