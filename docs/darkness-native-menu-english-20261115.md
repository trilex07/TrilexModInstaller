# Darkness II native menu language repair

The public 1.1.8 catalog correctly selects `darkness-menu` for English and
`polish-text` only when requested. Both pinned native menu outputs had Polish
labels. This is separate from the language of the runtime VR settings panel.

`scripts/prepare-darkness2.py` now validates the native Continue, Options and
Back strings for both locales before constructing either language pack.
`scripts/refresh-darkness-native-menu.py` can repair the published staging input
without rebuilding other modules. It requires the original archive and
catalog, an owned fixture that reproduces both exact package output hashes,
and an original English Languages.cl. It replaces the appended Languages.cl
and only its size/padding fields in the TOC. Source SHA-256 requirements,
unrelated ranges and other modules are preserved.

Verification: the existing catalog's English recipe failed the language check;
the replacement, decoded by the installer, passed. The Polish recipe still
passed its Polish check. The test fixture required its own preimage SHA pins
for the C# decoder invocation; the staged catalog retains the original pins.
The installer suite passed 106 checks including EN/PL module selection, INI
migration and source binding; an unrelated frozen image worker failed to create
its temporary directory. No live installation or game launch.

Staging: `C:/Users/Trilex/Darkness2VR-controls-20261115/build/installer-candidate`.
The candidate also contains the accompanying VR Controls DLL archive. The
unsigned catalog preserves the old versions and URLs for review. Bump versions,
assign the actual new release URLs, and sign only after selecting the tested
archives for publication. Keep private fixture caches out of a release.
