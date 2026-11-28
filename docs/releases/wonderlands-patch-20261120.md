# Wonderlands VR - November 20 update

Changes since the November 16 Wonderlands package:

- New Wonderlands-themed VR Settings and Controls screens, with categorized
  options, controller icons and descriptions reflecting the current configuration.
- Optional animated four-way D-pad wheel: point with either hand, or supported
  eye tracking, and confirm with the left trigger. The cursor is visible in the
  centre as well as over the four choices.
- Revised controls when the D-pad wheel is enabled: hold R3 for 0.5 seconds to
  open it; Triangle/Y toggles the held melee weapon and swing mode; right stick
  up attacks, and down changes weapons. Press Triangle/Y again to put melee away.
  L3+R3 still recentres. Menus keep their normal button actions.
- Optional snap turning: 15, 30, 45, 60 or 90 degrees, selectable in VR Settings.
- Optional double-L3 pause shortcut. A single L3 still starts sprint immediately;
  class abilities retain their normal timing.
- Optional left-hand interaction ray, using the normal use button (X/Square).
- Movement-stick range calibration for controllers that struggle to reach full
  forward speed, including affected Pico setups.
- Body display options: Full body, Wrists only or Weapons only. Reduced body
  modes remain beta; this update makes no measured performance-gain claim.
- Fixed the broken stereo presentation after the dark-hub mirror transition.
- Moved the level-up notification lower and hid technical ambient-ghost name keys.
- Explicit flat launches keep the mod passive; optional DLSS/Frame Generation
  paths check the actual NVIDIA adapter and capabilities before enabling.

## Using the new controls

In VR Settings > Controls, enable D-pad wheel (Hand or Eyes/Hand), then restart
the game. The revised R3/Triangle/stick layout belongs to that optional wheel
mode. Hold R3, then release it and point at a highlighted sector. Hold the left
trigger to send that D-pad direction; releasing closes the wheel. B cancels.
Melee toggle clears on menus, UI, mode/focus changes or lost tracking.

New optional features remain off by default. Existing customized settings retain
the installer's normal merge behavior. DLSS and Frame Generation remain optional;
Frame Generation is beta. No anti-aliasing or sharpening quality was reduced.

## Installer and validation

Update the Wonderlands Steam runtime in the current 1.1.3 installer bundle.
Automatic Epic detection and the Epic version checkbox remain available; the
previous player-confirmed Epic compatibility package is preserved. These new
runtime changes apply to Steam; they are not advertised for that older Epic DLL.
The installer executable, Epic payload and all other game payloads are preserved.

The previous wheel/UI candidate was accepted by the user in the headset. The
November 20 binding revision passed host input/UI tests and a Release build;
physical testing of the revised bindings remains pending. The Controls image
is exported directly by the production UI renderer, rather than captured in a
live gameplay session. The shared simulator was reserved by another mod test.

Source: d7c48ec (codex/wonderlands-dpad-wheel-20261119).
