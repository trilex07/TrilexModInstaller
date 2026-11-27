# Singularity VR on Oculus Touch, installer 1.1.5

[Download the refreshed Windows bundle (catalog 1.1.28)](https://github.com/trilex07/TrilexModInstaller/releases/download/installer-1.1.5/Trilex-Mod-Installer-1.1.5-Singularity-Quest2-Gloves-20261127-win-x64.zip).
Extract it to a new folder, run TrilexModInstaller.exe, and install Singularity VR with the VR core and portable settings. The optional texture and audio packs are independent of this update.

### Catalog 1.1.28

A cinematic could be mistaken for a gameplay hand mesh. The mod now waits for gameplay to identify the hands. It also keeps the native glove geometry and pose consistent across the early depth pass and later draws. At the helicopter in Workers’ District, mismatched draws produced black gloves. The corrected draws showed textured gloves in both eyes. The released DLL was checked in Steam with simulated Oculus Touch controllers, including a capture with both hands extended. The player’s report of a second pair on physical Quest 2 remains open.

A separate controlled depth-pass test reproduced the floating black helicopter. In four captures with the released DLL under normal conditions, the wreck’s depth and color projections matched across 3,041 model points in each eye. The trigger on the player’s system remains unknown.

The portable bundle includes the existing Touch alignment, 720p menu and rifle fixes. Upgrading the signed 1.1.27 bundle preserved custom headset dimensions. The installer also passed reinstall, damage detection, repair and uninstall tests.

The 32-bit mod reads the OpenXR pointing pose separately from the grip pose on Oculus Touch. When both unarmed hands are visible before acquiring the TMD, it aligns each wrist and knuckles with that controller. A Steam gameplay test at the burning helicopter in Workers' District verified horizontal hands and independent left-hand movement in both eye images. This used a simulated Quest 3 with Touch controllers. Physical Quest 2 acceptance, the reported second pair of hands and the floating helicopter silhouette remain open.

Catalog 1.1.26 also fixes the main menu and load-game list when the game starts at 1280 × 720. At that size the private menu texture matches the dimensions of the stereo backbuffer. The mod now identifies the focused menu's actual texture and keeps its contents together. The change was checked in both views of a Steam OpenXR simulator session, including selection of the original campaign checkpoint.

For the Quest 2 configuration reporting **2232 × 2292 per eye**, the matching Steam launch options are `-ResX=4464 -ResY=2292 -windowed`. With automatic resolution enabled, removing explicit resolution arguments lets the mod reuse the headset measurement on the following launch. The installer no longer distributes a PSVR2 measurement as the local resolution cache.

### Capture the floating silhouette

Load the campaign at the helicopter. Download the [frame collector](https://github.com/trilex07/TrilexModInstaller/releases/download/installer-1.1.5/collect-singularity-vr-frame.ps1) and run it from the download folder in PowerShell:

~~~powershell
.\collect-singularity-vr-frame.ps1
~~~

Return to the headset during the 15-second countdown and hold the controllers forward where the silhouette is visible. The script prints the path to a ZIP in `%LOCALAPPDATA%\SingularityVR` containing the two-eye bitmap, its draw trace and the session log. It replaces your Windows profile path in the log and keeps the archive on your computer. Attach that ZIP and, if available, the headset recording of the same moment. To collect a manual capture already completed, pass `-ExistingLog` followed by the path to that session's log, with the bitmap and trace in the same directory.

For manual capture, run the following in PowerShell at the wreck, then return to the headset.

~~~powershell
Start-Sleep -Seconds 15
New-Item -ItemType File -Force -Path "$env:LOCALAPPDATA\SingularityVR\shadow-pass.request" | Out-Null
~~~

After exiting the game, open `%LOCALAPPDATA%\SingularityVR\view_matrix.log` and search from the bottom for `shadow-pass trace finished`. That line names the draw trace. The following `capture: saved` line ending in `source=XR-backbuffer` names the matching two-eye bitmap. A later automatic screenshot can have a newer timestamp. Copy those two named files and the log before relaunching. If the capture failed or the next line refers to a different event, repeat the capture at the wreck. A headset recording from the same moment allows comparison with the image delivered over Air Link.

### Catalog 1.1.27

The retail core from `041fa024` activates the two native muzzle effects together. In consecutive Research Facility loads on the development build, the gameplay helper peaked at 68.52 ms before the change and 33.94 ms after it. The retail DLL activated both targets in 32.836 ms. Firing the AR9 Valkyrie produced the tracked muzzle and tracer receipts. The Steam/OpenXR simulator ran the original checkpoint with the player's save protected. The separate tracer setup and the remaining one-time activation cost are still present.

The fixture upgraded the signed 1.1.26 bundle to 1.1.27, checked local headset settings and DLL identity, then exercised reinstall, damage detection, repair and uninstall. The package changes only the Singularity core. Quest 2 verification of hand orientation and the black helicopter silhouette still requires a player capture.

| Artifact | SHA-256 |
|---|---|
| Retail DLL | `F14D396E75E814E20D378A8C9D1D7C8777375C7B12F575FB9B6857B16664AB12` |
| Singularity VR core ZIP | `FF26813FEF6FDCDE6D789EBE2A6DA5F633AD93924FFCB5A016660CCF2E644F7C` |
| Signed catalog | `8FDEB255146C2B24B199A603374D74D567CEBE5DF6A8C3358E56B445D3445A03` |
| Installer bundle | `D6FF2164FAAC07842AD197A08A94256F88253E24E89A17149153F45984CD80AD` |

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
