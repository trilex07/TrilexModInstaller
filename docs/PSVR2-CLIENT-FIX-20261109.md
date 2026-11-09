# PSVR2 client distribution correction

Baseline installer `7141ce65`, signed catalog 1.1.4 SHA256
`EDEE069C047E5723A34327C42CE33F4A9A5284716D15529C607A03DB13CC5943`.
Confirmed source-to-package failures are detailed in `PSVR2-TOOLKIT.md`.
These are sufficient to break clean installations, but no remote player's
Toolkit version/logs have been received: do not attribute every report to them.

## Inputs

| Component | Identity |
|---|---|
| Toolkit loader | experimental-3 CAPIApps archive, SHA256 `30A456CC81C2B0D687FE987C2988EE90DCCB54AC24F52C4AAAF8F79AF7BC82D8` |
| Darkness x64 helper | `src/psvr2` unchanged at source `6ad62a2d`; last bridge/backend change `b609cece`; MSVC x64 `/O2`, SHA256 `9C2C3F69AD107DB5F2BE886D8F749DAC9AB1756A320EF8CF9056F87A84841697` |
| Singularity x64 helper | source `69acb592`, baseline `fa4c3151`; MSVC x64 `/O2` PE32+, SHA256 `6B5BCBF5ACE2C2A69DF20E9B65C87366BC32C1D63CBEBB617D8C1FED9162DA93` |
| Singularity renderer retained | SHA256 `E0E08775939176C090BDAD8FEC47C6BA81B3E34A8B288A7A4A80EDCE76253220` |
| Wonderlands renderer retained | SHA256 `504885E44AA1EE13D75C7958670D80FFD41E9389091FAA0C365FC083D0B260A2` |

The signed catalog is 1.1.5. All three core module versions receive a
`20261109-psvr2-clients` stamp so installed users see an update. Only the
Singularity helper is replaced inside a game archive. Other archive bytes,
renderers, textures, menus, input and language settings are preserved.

## Validation and limits

- 106 Core regressions PASS, including fresh Unicode per-user paths,
  idempotence, original preservation, late write failure rollback,
  active-game refusal, foreign target refusal and external-change recovery refusal.
- Singularity real optimized helper path fixtures PASS: explicit existing path,
  missing loader, Unicode AppData fallback and absent environment. No CAPI loaded.
- Desktop self-contained publish, embedded payload fixture, signed package
  lifecycle, WPF rendering and remote digest verification are recorded in the
  final release evidence. Check that evidence rather than assuming completion.
- Physical PSVR2 eye tracking, calibration, gaze axes and frame timing NOT RUN.
  No Steam deployment, game launch or driver installation on the developer PC.

For a failing remote installation request the game's startup log and Toolkit
version, after updating with the new full ZIP. Distinguish missing client,
missing CAPI export, inactive driver, failed init and invalid/no gaze samples.
Calibration alone establishes none of those producer-to-consumer stages.
