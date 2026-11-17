OpenXR startup diagnostic report (Darkness II / Singularity)

1. Extract all files together to a writable folder.
2. Double-click Collect-OpenXR-Report.cmd (administrator rights are not needed).
3. Send OpenXR-startup-report.json to the mod author.
If Windows policy blocks the script, send the error; no policy changes are needed.

This only reads both OpenXR registry views and referenced runtime/layer
manifests and PE headers. It does not change the registry, switch runtime,
disable layers, load their DLLs, start games, or upload files.
The report includes file paths, which can contain your Windows user name.
It checks file existence and architecture, not transitive DLL dependencies.
An existing DLL can still fail to load. Game-specific environment overrides
may differ from this console's environment. Do not delete registry entries
based solely on this report.
