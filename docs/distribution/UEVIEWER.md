# Fixed local Centurion exporter

The installer embeds the reviewed x86 UE Viewer and SDL2 2.0.10 helpers. They run
only against a hash-verified local copy of the owned pistol package, in an isolated
workspace, with fixed object names and `-notex`. No game process is started.
Final generated caches must match accepted V7 hashes before installation.

Build input: official `umodel_win32.zip`, SHA256
`2CEAD261B360DBCC3E703F091837B415F868250DD6A73A154F4232D09E226EF8`.
Set `TRILEX_UEVIEWER_ARCHIVE` to a local copy for offline builds. Otherwise the
build downloads the official URL and refuses any different archive. A changed
upstream archive requires review, never an automatic hash update.
The official server requires its own project-page HTTP referrer; direct hotlinks
return 404. The build sends that referrer and still verifies the unchanged SHA.

Upstream: https://www.gildor.org/en/projects/umodel
Source: https://github.com/gildor2/UEViewer
Archive LICENSE.txt/readme identify MIT, copyright 2022 Konstantin Nosov.
SDL2 binary version is 2.0.10; matching notice:
https://github.com/libsdl-org/SDL/blob/release-2.0.10/COPYING.txt

Portable packages retain these notices. The full statically linked dependency
audit remains a public-release gate; the MIT top-level notice alone does not
establish the license of every embedded codec. Private reconstruction tests are
not distribution approval. Original PSK/PSA output remains local and ignored.
