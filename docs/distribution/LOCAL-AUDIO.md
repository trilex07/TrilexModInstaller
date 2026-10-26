# Local sound remaster and dubbing provenance

The optional sound-remaster recipe contains seven small JSON operation records,
not recordings, Ogg payloads, neural weights or game packages. A fixed embedded
worker reads SHA-pinned files from the user's owned installation, extracts the
identified native wave, applies fixed EQ/dynamics/true-peak processing and writes
an exact expected native bank. Package metadata cannot supply executable code,
filters, tool URLs or commands. Source and output hashes are mandatory.

Coverage: Centurion, Kasimov, Valkyrie, shotgun, exploration music, combat music,
and ocean/wind ambience. The previously withheld TMD impulse replacement stays
excluded. This does not establish that the wind asset is the game's thunder cue.

## Optional codec

Selecting this module prepares FFmpeg 9.0.2 essentials from Gyan's fixed HTTPS
upstream archive. Both archive and executable hashes are pinned in
`LocalAudioGenerator.cs`. The approximately 109 MB download is disclosed in the
review screen. It is not bundled or labeled MIT. LICENSE/README/source notice are
retained alongside the cached tool. Offline mode requires an already verified
cache. `TRILEX_AUDIO_CODEC` permits an explicitly imported approved binary only;
an arbitrary executable is rejected. No general PATH lookup is used.

CLI cache preparation: `dotnet run --project src/Trilex.Installer.Tools -c Release -- prepare-audio-tools`.
This explicit command permits the fixed upstream download; installation offline never does.

## Reproduction

Run `scripts/prepare-local-audio.py --help` with private accepted-source and
mastering-manifest fixtures, then `verify-recipes` through Installer.Tools. The
producer archives only allowlisted metadata. Original/audio fixtures must stay
ignored. Two approved FFmpeg builds independently reproduced all seven accepted
bank SHA256 values; final C# embedded-worker validation is recorded separately.
This is offline byte identity, not listening approval or PSVR2 validation.

## Dubbing is separate

The local audit did NOT establish that most final voices are MIT licensed.
`cast_legal_20261014/CAST_MANIFEST.json` is a proposed cast, not a per-take
provenance record. `synthesize_551_delivery.py` supplies original English game
recordings as reference prompts. The final 551 manifest records output identity
but does not independently approve reference/performer rights. Profile names
alone cannot classify a final take as redistributable.

The currently identified Pocket-TTS Polish 6L model card declares CC-BY-4.0:
https://huggingface.co/shefowl/pocket-tts-polish-6l
XTTS documentation identifies a different Coqui Public Model License:
https://docs.coqui.ai/en/latest/models/xtts.html
Neither a software license nor local execution automatically resolves reference
recording, voice or output permissions. No automatic cloning of unresolved game
actor references is enabled, and the existing dubbing module remains unavailable
for public promotion. Existing private fixtures are not removed or altered.

To admit a take, bind its exact output SHA to engine/model revision, reference SHA,
reference source and applicable permission/license evidence, with attribution where
required. Approved original/consented or appropriately licensed reference voices
can use a separately reviewed local-generation path. Missing evidence stays
UNVERIFIED; it is not converted into approval by a disclaimer or a free release.
