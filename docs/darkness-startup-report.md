# Darkness II VR startup report

In a build containing this change, select The Darkness II and its game folder,
launch normally, then close the game. In the installer, open **More options**
and select **Save VR startup report**. Save the JSON outside the game folder and
review it before sharing. The installer never sends it.

The report contains SHA-256 fingerprints and selected numeric INI settings, plus
structured startup observations from the beginning of the per-user runtime log.
It does not contain paths, usernames, arbitrary runtime names, raw log lines,
INI contents, saves, or other games' data. The log last-write timestamp is
included so the recipient can check freshness; the selected-folder fingerprints
and settings describe the files at export time and do not prove which process
produced that log. A missing, old or truncated log is not evidence that the
runtime stopped at its last reported stage.

For desktop-only startup, first state whether the actual 3D game menu appeared.
Use the report to distinguish native Present readiness, missing XR bootstrap,
a failing OpenXR call, and submission of a game layer. A successful submission
does not demonstrate that the headset displayed it.
