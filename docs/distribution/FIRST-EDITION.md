# Complete V7 first-edition publisher decision

On 2026-10-26 Trilex explicitly requested the complete tested configuration for
other users: “przygotuj całość by ktoś mógł to zainstalować tak jak ja mam teraz”.
This follows the explicit decision to share the free fan project with its
distribution notice. This document records that publisher decision; it does not
certify ownership of the game's assets or permissions from voice performers.

Accepted runtime: `38ca72f88112bfcbd26addb0d6aa4f76095aa15e`.
Exact file identities: `inventory/singularity-v7.json`. All 164 pins must be
accounted for, including the five explicitly excluded protected/inactive files.

The complete candidate contains code, authored interface resources, local
reconstruction recipes, Polish text and replacement dubbing instructions. A
compatible owned game is required. No game executable, full commercial cooked
package, Bink movie, Windows DirectInput binary or PolyOne glove is distributed.
Replacement voice data inside reconstruction instructions remains replacement
voice data: encoding is not claimed to remove third-party rights.

Reviewed MIT/BSD/Apache/CC0 components retain their actual notices. Native
index/texture/knife/Centurion reconstruction runs on owned originals, preserving
their source identities. Seven audio banks use local DSP; no remastered sound
recordings are included in that module. UE Viewer and FFmpeg are obtained from
their upstream sites with fixed SHA256, not embedded as redistributed executables.

Final replacement dubbing, inherited HUD artwork and inserted font data have
unresolved third-party permission provenance. Their `rightsVerified` remains
**false**. The separate signed `publisherReview` records approval to include them
as fan content and carries an explicit third-party-rights notice. The installer
shows that distinction in its review before installation. It is not legal
clearance, a waiver by a rights holder, or an MIT grant over those materials.
The existing distribution notice and rights-holder contact/removal policy apply.

The old default remains: a stable module with neither reviewed rights nor an
explicit complete publisher review is refused. Only the pinned signed catalog
can authorize consumer installation; arbitrary archives cannot authorize
themselves. Whole cooked-package copies remain categorically forbidden.

This changes the earlier all-permissions-confirmed publication policy in response
to the user's explicit fan-project publishing instructions. It does not change
Authenticode production requirements. Public unsigned executable publication
still needs the human decision recorded separately; no signing bypass is added.

## Build and assemble

Build using `scripts/build.ps1 -Publish`. Prepare complete packages using
`scripts/prepare-first-edition.py --inputs <private inputs JSON> --output
artifacts/<fresh output> --tag installer-1.0.0-rc.1`. Sign its catalog with the
existing ignored publisher key using Tools `sign`, then `verify` against
`catalog/trusted-publisher.pem`. Do not regenerate or distribute a private key.

The complete bundle layout is `TrilexModInstaller.exe`, `catalog.signed.json`,
`packages/<module-id>.zip`, notices, checksums and the PL/EN installation guide.
The application verifies the adjacent signed catalog and chosen packages. No
developer directory, external Python install or source checkout is needed.
First-time Centurion/audio processing requires upstream tool downloads; an
offline machine needs their previously verified cache.

Offline installation/recovery tests and the accepted game's PSVR2 result are
different evidence. Clean-PC installer acceptance remains pending until tested.
