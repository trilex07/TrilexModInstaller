# Optional Drive packages and universal game launch

Branch: `fix/installer-cloud-integration-review`, following the Darkness review.
No Steam/game launch, installed-file changes or public release replacement.

## Drive sources

The publisher supplied these public files:

- Polish dubbing: `https://drive.google.com/file/d/1L1NqmuTTDgJQnHbLCWiOXmA_ZRbN67dF/view`
- HD textures: `https://drive.google.com/file/d/1EV4VdgSlh2AqxVaSf26F6027OwEgy6YI/view`

Prepared and signed a catalog from the accepted RC7 catalog using
`scripts/set-singularity-drive-packages.py`. Only the two module URLs changed;
archive hashes, sizes, files, native recipes and dependencies are unchanged.
The resulting signed catalog SHA-256 is
`75A8A068E48661456341541370AE6C2AEEAED9D50BCEBC972347D9F33418A3BB`.
Local output: `artifacts/drive-20261105/catalog.signed.json`.

Both files were downloaded anonymously using the actual `PackageDownloader`,
not a browser cache or a mocked HTTP response. Exact verification passed:

- Dubbing: 29,404,083 bytes;
  `86D5193630F2C16597770FCD54F1B5157C995B80EF501F2999100A97DCB9F68C`.
- Textures: 1,258,728,542 bytes;
  `A80691A201A1E313054760A7CE972BDC282BBDB39695CFDC746A2A0D2CA447E5`.

Reproduction command:
`dotnet run --project src/Trilex.Installer.Tools -c Release -- verify-downloads SIGNED_CATALOG PUBLIC_KEY singularity FRESH_CACHE polish-dubbing hd-textures`.
Use a fresh cache to verify network delivery; existing hash-verified cache files
are deliberately reused. Evidence: `artifacts/drive-20261105/verified-downloads/download-verification.json`.
Only selected modules are requested. Adjacent verified ZIPs and offline import
continue to work. Omit the two optional ZIPs from an online distribution if
on-demand downloading is wanted instead of shipping them inside the bundle.

Do not publish this Drive catalog alone for old RC7 installers: the new
Drive-aware EXE must accompany it. The existing public RC7 has not been changed.

## Launch behavior

The PL/EN button says **Uruchom grę / Run the game** for both games. It prepares
the selected game's exact EXE with the game root as working directory, with no
Steam URI, launcher argument or executable replacement. The game itself can
still invoke its storefront/DRM if that edition requires it.

The installation must be managed and match the selected game and directory.
A known fingerprint or exact recorded unrecognized-build consent is required.
The executable is hashed again at the actual button action; changes after
review are refused. No new compatibility claim for retail/GOG is implied.
The test only inspects `ProcessStartInfo`; it never starts a game.

## Verification

- Core: **81 PASS / 0 FAIL**, including launch identity, recorded consent,
  wrong-game/root/hash rejection, and executable replacement after review.
- Drive catalog producer: **2 PASS / 0 FAIL**.
- Tools and WPF Release build: **0 errors / 0 warnings**.
- Self-contained win-x64 publish: **PASS**.
- Published EXE with the new signed sidecar: **8 PL/EN UI rows PASS**, zero binding errors.
- Local EXE SHA-256:
  `B0B507E529103047F793F60E8ED7FECC61F2BB1819F5DB24C3BB9F9B24EA39CF`.
- Game/runtime/PSVR2: **NOT RUN**. This is an installer-only change.

Local EXE: `artifacts/drive-20261105/publish/TrilexModInstaller.exe`.
The earlier Darkness catalog/redistribution gates remain in force; this local
candidate does not publish Darkness or complete its physical acceptance.
