# Trilex Singularity VR — first complete edition

## Polski

1. Wypakuj **cały ZIP**, zachowując `packages`, katalog podpisany i EXE w jednym
   folderze. Nie kopiuj instalatora do katalogu gry. Potrzebujesz własnego
   Singularity ze Steam, zgodnej wersji Windows x64 i aktywnego OpenXR/SteamVR.
2. Zamknij Singularity. Uruchom `TrilexModInstaller.exe`, wybierz język interfejsu
   i **Wykryj grę**. Możesz wskazać folder zawierający `Binaries` oraz `RvGame`.
   Nie wskazuj folderu zapisu. Instalator odmówi zmiany niezgodnej wersji EXE lub
   nieznanych plików innego moda; nie usuwa ich samodzielnie.
3. Wybierz **Pełna konfiguracja Trilex V7 (PL)** — komplet przetestowanej wersji
   Trilexa: VR, ręce/TMD, przeładowania, HUD/menu, polski tekst i dubbing, remaster
   audio oraz tekstury HD. Możesz zmienić dodatki. Polski dubbing filmów wymaga
   polskiego tekstu w tej wersji V7; EN/EN i PL/EN są dostępne.
4. Jeśli brakuje Visual C++ x86 lub x64, użyj odpowiedniego przycisku prowadzącego
   do oficjalnego pakietu Microsoft i wróć do sprawdzania. Gra wymaga x86,
   helper spustów PSVR2 wymaga x64. Przy pierwszej instalacji kodek audio i
   eksporter Centuriona są pobierane ze stałych adresów upstream i weryfikowane
   hashem. Nie potrzebujesz Pythona, .NET SDK ani repozytorium.
5. Kliknij **Podsumowanie instalacji**, sprawdź folder, dodatki i informacje o
   materiałach fanowskich, następnie **Pobierz i zainstaluj**. Paczki z ZIP są
   rozpoznawane lokalnie. Poczekaj na komunikat o poprawnej weryfikacji.
6. Uruchom SteamVR/PSVR2, a grę przez Steam. Nie uruchamiaj jej w trakcie zapisu
   plików. Po instalacji możesz zamknąć instalator.

**Naprawa / usunięcie:** użyj tego samego konta Windows i instalatora. „Sprawdź
pliki” porówna hashe; „Napraw” odtworzy wybrane moduły. „Przywróć / odinstaluj”
przywraca zachowane oryginały. Zachowaj `%LOCALAPPDATA%\Trilex\ModInstaller`:
zawiera kopie i dziennik odzyskiwania. Nie usuwaj go przed odinstalowaniem.
Jeśli operację przerwano, zamknij grę i wybierz odzyskanie przerwanej instalacji.

**Aktualizacja RC3 → RC4:** zamknij grę i stary instalator, wypakuj cały nowy ZIP
do osobnego folderu, uruchom nowy EXE i wybierz ten sam folder gry oraz pełny
preset. Nie usuwaj gry ani kopii z LocalAppData. RC4 uzupełnia katalog wyboru
broni, grafikę filmów startowych/ładowania, splash i brakujące tekstury 2X/8K.
Paczka jest większa, ponieważ zawiera teraz rzeczywiste tekstury. Odtworzenie
plików w testach offline nie potwierdza jeszcze obrazu w PSVR2.

To nieoficjalny, darmowy mod. Kod instalatora ma MIT; cudze materiały, gra i głosy
nie otrzymują przez to MIT. Szczegóły w `FIRST-EDITION.md` oraz
`DISTRIBUTION-NOTICE.md`. Lokalny RC bez Authenticode nie jest podpisaną wersją
produkcyjną; nie wyłączaj zabezpieczeń Windows. Podpis katalogu i podpis Windows
to różne mechanizmy. Nie ma automatycznej instalacji sterowników ani emulatora.

## English

1. Extract the **entire ZIP**, keeping EXE, signed catalog and `packages` together.
   Keep it outside your game directory. An owned compatible Steam Singularity,
   Windows x64 and working OpenXR/SteamVR setup are required.
2. Close Singularity. Run `TrilexModInstaller.exe`, select the installer language,
   then **Detect game** or select the folder containing `Binaries` and `RvGame`.
   Unknown game builds and conflicting foreign mod files are refused, not deleted.
3. Choose **Full Trilex V7 (Polish)** for the complete accepted configuration, or
   choose individual additions. V7's Polish movie voices require Polish game text.
   EN/EN and PL text/EN voice also work; the installer language is separate.
4. Install the official Microsoft x86/x64 VC++ prerequisites if requested (x86
   for the game, x64 for the PSVR2 trigger helper).
   On first use, pinned audio/exporter tools are obtained from their upstream
   sites and hash-verified. No Python, .NET SDK or source checkout is needed.
5. **Review installation**, check folder, modules and fan-content notice, then
   **Download & install**. Local bundled packages are recognized automatically.
   Wait for exact post-install verification before launching the game through Steam.
6. Enable your PSVR2/SteamVR environment and launch through Steam. Never start the
   game while files are being written.

Use **Verify files**, **Repair**, or **Restore / uninstall** under the same Windows
account. Keep `%LOCALAPPDATA%\Trilex\ModInstaller` until restoration is complete:
it holds original backups and the recovery journal. Recovery waits for game exit.
Do not delete backups or unknown conflicting files to force installation.

**RC3 → RC4:** close the game and old installer, extract the whole new ZIP to
another directory, run the new EXE, select the same game folder and full preset.
Do not reinstall the game or remove LocalAppData backups. RC4 adds the missing
weapon-selection catalog, startup/loading artwork, splash and 2X/8K textures.
The larger package now includes those actual texture files. Physical acceptance
of this corrected package is still pending.

This unofficial free fan mod does not grant rights over the game, recordings or
third-party assets. See the included distribution and publisher-decision notices.
The local unsigned RC is not an Authenticode-signed production release. Do not
disable Windows security. The signed module catalog is separate from Windows
executable signing. No drivers or game executable are included.

## What is tested

The build-specific evidence receipt records exact hashes and automated outcomes.
Offline file installation and rendering of the installer interface are not proof
of clean-PC or PSVR2 acceptance. The accepted V7 runtime is preserved unchanged.
Game EXE fingerprint: `BD459ACD87D4B82693928DEE4ED437C086D5928A8E0428F3AD54B6FC114C0F51`.
