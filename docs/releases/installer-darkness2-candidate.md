# The Darkness II VR — installer candidate

Base: Singularity RC7 bug-fix refresh, 2f9d4ec. This is an offline candidate.
Existing signed RC7 catalogs still show Darkness II as unpublished. No game
installation, catalog signing with the production key, or upload was performed.

- Select The Darkness II VR, then detect Steam AppID 67370 or browse to the game.
  The signed catalog must match the exact DarknessII.exe SHA-256. The RC7
  Singularity-only disc/GOG exception does not apply.
- VR Core installs d3d9.dll, Darkness2VR.ini and the x86 OpenXR loader.
- Polish subtitles and game text is optional and defaults off. Its data recipes
  reconstruct only the H.Misc cache/TOC pair from exact owned originals. The
  packer changes only the live Languages.cl entry; the rest of the cache bytes
  and all other entries remain intact. No Polish voices or voice packs.
- Original files are backed up. Deselecting Polish text or uninstalling restores
  the original managed files. Close the game before changes. Files already
  changed by an unmanaged install may fail exact preimage checks.
- Polish glyphs are a separate in-game acceptance item. The candidate does not
  install DLC font repairs, private language-switch profiles or HD textures.

Build the release tools with scripts/build.ps1. Run
`python scripts/prepare-darkness2.py --help` for the packaging inputs. Use the
exact reviewed x86 runtime, a private clean copy of H.Misc.cache/H.Misc.toc,
the reviewed Polish Languages.cl, and Darkness2VR's tools/polish_pack.py cache
tool. The script writes private intermediate files and an unsigned merged
catalog under a fresh artifacts directory. Preserve the accepted Singularity
catalog and packages; the Darkness II bundle uses packages/darkness2/. Never
ship the intermediate files. The catalog's validity period is inherited from
the input; review it before signing.

For large reviewed packages, upload the final ZIP as a publicly downloadable
file. In the signed module use the direct URL form
`https://drive.usercontent.google.com/download?id=FILE_ID&export=download&confirm=t`.
The normal Drive sharing link is useful for sharing the file ID. Test each
actual download without a Google account before signing. The installer refuses
HTML, foreign redirects and bytes that do not match the signed SHA-256 and
length. Google Drive can throttle downloads; use the verified offline import
or the pinned GitHub Releases transport if a file cannot be downloaded.
Replacing a ZIP requires a newly reviewed and signed catalog entry.
Catalogs themselves still come from pinned GitHub Releases or signed offline
files. A large texture pack still requires its own supported install recipe
and distribution review; no texture module is enabled by this change.

Offline evidence: the 2026-11-04 runtime/text inputs produced a 1,191,930 byte
core ZIP and a 442,243 byte text recipe ZIP. Both reconstructed cache files
matched exact output hashes. The 76-test suite passed, including game
detection, install/uninstall in a fixture, and mocked Drive transport.
Live Drive delivery, Steam and headset behavior remain unverified.
