# Actual audio payloads in prepared recipes

The user's source clarification is recorded: voices and sounds originate from
the game. No separate redistribution permission was supplied. This is not a
reason to change any module's `rightsVerified` value.

The installer can reconstruct exact packages from an owned local copy, but that
does not mean all audio is generated from that local copy. SVRP opcode 1 embeds
literal replacement bytes in the distributed instructions.

Measured against the prepared V7 recipe manifests:

- Polish dubbing: 30,864,267 literal bytes, including 1,588 `OggS` page markers.
- Audio remaster: 6,420,019 literal bytes, including 100 `OggS` page markers.
- UI package: 5,315 literal bytes, no `OggS` page markers.

An `OggS` signature alone does not prove a full playable recording, authorship,
or a particular actor. It does prove that a blanket claim of instruction-only,
media-free recipes is unsupported. No payload is exported by this audit.

Reproduce without Steam access:

```
python scripts/audit-recipe-payloads.py artifacts/v7-owned-source-native-20261026b artifacts/audio-recipe-payload-audit.json
```

The audit reads only files listed by each module manifest. Decoded working cache
files are deliberately excluded. Tests cover exact byte accounting, truncation,
unknown opcodes and trailing data.

Public promotion remains blocked for these media packages. Two distinct paths
can close it: documented permission covering the actual replacement media, or
a reviewed local generation pipeline that does not distribute those recordings.
The latter is practical for deterministic original-audio DSP; it is not equivalent
to reconstructing a new Polish performance from original English audio.

Existing SIDE records also identify Pocket-TTS Polish 6L output in later campaign
deliveries. Do not label the entire final corpus XTTS or original studio Polish
dialogue without per-file evidence. Original Bink Polish-track selection is a
separate runtime feature and needs no redistributed movie/audio files.

A replacement metadata-only DSP path for the seven accepted sound banks is now
implemented; see LOCAL-AUDIO.md. The earlier literal-recipe finding remains valid
for the old remaster package and existing dubbing recipes.
