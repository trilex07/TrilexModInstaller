# Fixed local Centurion exporter

The installer EXE does not embed UE Viewer or SDL2. Starting with RC6, the
complete ZIP carries the unchanged official archive at `tools/umodel_win32.zip`
and imports it into its verified optional tool cache before any download. The
x86 UE Viewer and SDL2 2.0.10 helpers run
only against a hash-verified local copy of the owned pistol package, in an isolated
workspace, with fixed object names and `-notex`. No game process is started.
Final generated caches must match accepted V7 hashes before installation.

Build input: official `umodel_win32.zip`, SHA256
`2CEAD261B360DBCC3E703F091837B415F868250DD6A73A154F4232D09E226EF8`.
Set `TRILEX_UEVIEWER_ARCHIVE` to the original archive to prepare an offline cache.
Otherwise the application downloads the official URL and refuses any different archive. A changed
upstream archive requires review, never an automatic hash update.
The official server requires its own project-page HTTP referrer; direct hotlinks
return 404. The build sends that referrer and still verifies the unchanged SHA.

Upstream: https://www.gildor.org/en/projects/umodel
Source: https://github.com/gildor2/UEViewer
Archive LICENSE.txt/readme identify MIT, copyright 2022 Konstantin Nosov.
SDL2 binary version is 2.0.10; matching notice:
https://github.com/libsdl-org/SDL/blob/release-2.0.10/COPYING.txt

The cache and complete bundle retain the upstream LICENSE.txt/readme.txt and
SDL2 zlib notice. The official archive is redistributed unchanged, under its
included upstream notices; this does not relabel separate components as MIT. Original PSK/PSA output remains local and ignored.
Only the pinned archive and two independently pinned binary hashes are accepted;
the archive's unused x64 pair is not extracted. Missing offline tools fail before
game writes. No PATH executable or package-provided exporter is executed.

## DNS failure and offline retry (after RC5)

RC5 still downloads this tool even in the complete bundle. A DNS error for
www.gildor.org is independent of the game edition and the evidence report.
The next installer accepts the unmodified pinned upstream archive at
`tools/umodel_win32.zip` beside its EXE, before network access, including offline
mode. A corrupt archive is rejected; there is no unverified fallback.
RC6 assembly requires the verified archive and includes it and its notices.
The smaller developer-only EXE without the tools directory still needs an
existing verified cache, explicit archive override, or upstream access.
The existing `TRILEX_UEVIEWER_ARCHIVE` override also works in RC5.

`installer-rc5-evidence.json` is an optional test report, never an installation
input. Separate release module ZIPs are already in the complete bundle.
Retail-disc/Spanish builds remain unverified: executable and every source file
must match approved fingerprints; Steam launch is currently Steam-specific.
Do not bypass hashes to admit another edition.
