# Singularity VR on Oculus Touch, installer 1.1.5

[Download the refreshed Windows bundle (catalog 1.1.26)](https://github.com/trilex07/TrilexModInstaller/releases/download/installer-1.1.5/Trilex-Mod-Installer-1.1.5-Singularity-Quest2-Menu-20261127-win-x64.zip).
Extract it to a new folder, run TrilexModInstaller.exe, and install Singularity VR with the VR core and portable settings. The optional texture and audio packs are independent of this update.

The 32-bit mod reads the OpenXR pointing pose separately from the grip pose on Oculus Touch. When both unarmed hands are visible before acquiring the TMD, it aligns each wrist and knuckles with that controller. A Steam gameplay test at the burning helicopter in Workers' District verified horizontal hands and independent left-hand movement in both eye images. This used a simulated Quest 3 with Touch controllers. Physical Quest 2 acceptance, the reported second pair of hands and the floating helicopter silhouette remain open.

Catalog 1.1.26 also fixes the main menu and load-game list when the game starts at 1280 × 720. At that size the private menu texture matches the dimensions of the stereo backbuffer. The mod now identifies the focused menu's actual texture and keeps its contents together. The change was checked in both views of a Steam OpenXR simulator session, including selection of the original campaign checkpoint.

For the Quest 2 configuration reporting **2232 × 2292 per eye**, the matching Steam launch options are `-ResX=4464 -ResY=2292 -windowed`. With automatic resolution enabled, removing explicit resolution arguments lets the mod reuse the headset measurement on the following launch. The installer no longer distributes a PSVR2 measurement as the local resolution cache.

### Capture the floating silhouette

At the helicopter, run the following in PowerShell. Return to the game and hold both controllers forward while looking at the silhouette. After the delay the mod captures one focused gameplay frame with both controllers tracked.

~~~powershell
Start-Sleep -Seconds 15
New-Item -ItemType File -Force -Path "$env:LOCALAPPDATA\SingularityVR\shadow-pass.request" | Out-Null
~~~

After exiting the game, the newest `capture_*.bmp`, matching `shadow-pass-*.txt`, and `view_matrix.log` in `%LOCALAPPDATA%\SingularityVR` show the two eyes before submission, the draws that produced the frame, and the active settings. Copy these before relaunching. A headset recording from the same moment allows comparison with the image delivered over Air Link.

### Catalog 1.1.26

The tested runtime is built from the retail-compatible source at `cbf69b1a`. An isolated upgrade from the signed 1.1.25 bundle verified both retained Touch settings, the new menu setting, the DLL hash, user resolution cache, idempotent reinstall, damage detection, repair and uninstall.

| Artifact | SHA-256 |
|---|---|
| Tested d3d9.dll | `2B6B0A8972992087E69E564E898211BFFB6C9F6358B53824ACE500EC987EE817` |
| Singularity VR core ZIP | `51DD342F7570051BAC966F772952B72207942DD7C4C6D9C83EFEE42D82BC3521` |
| Signed catalog | `F76EE7CE4EC6EBE05893B658C38FA7841C4D997766373BD6391EAAAF4BEBB82A` |
| Installer bundle | `9E7A3CAA12B9DA17EA628FEC63B360BE2F94E6EA9A098D241CB8F8E444BA81B4` |

### Catalog 1.1.25

The signed catalog is 1.1.25, based on 1.1.24. The bundle uses the executable from the November 25 Rapture bundle. The only changed entries are the signed catalog, Singularity VR core, both release information files and the internal checksums. Other packages retain their original contents.

| Artifact | SHA-256 |
|---|---|
| Tested d3d9.dll | `D265B8843C10D98BFB748D809C871FD03C0472B972B7D0F6F151727F7ADA5D9D` |
| Singularity VR core ZIP | `8871D17C049EA99ACB6AEBBCE07A07F20D9E5B5BA9EE5CE847D1C10EF709FA93` |
| Signed catalog 1.1.25 | `476B17BABAD2E3EE13C6D021A2DE534CFC96D3D52A8D69F070E762EEB4E210DB` |
| Installer bundle | `E8734187E13C6C665481F529A280AD98F247CAC0B247B73D7E593E4051EE2CD2` |

An isolated fixture used the signed November 25 bundle, set a custom headset resolution, then upgraded with the signed November 27 core and portable settings. Checks covered the two new INI values, preservation of the custom resolution and unrelated user setting, exact DLL hash, reinstall, damage detection, repair and uninstall. A fresh download of both public modules passed the installer's signature and package verification.
The extracted 1.1.5 Windows executable also passed its UI smoke test in all eight English and Polish size/DPI configurations.
