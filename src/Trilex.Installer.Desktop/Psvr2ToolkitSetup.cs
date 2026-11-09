using System.Diagnostics;
using System.Windows;
using Microsoft.Win32;
using Trilex.Installer.Core;

namespace Trilex.Installer.Desktop;

internal static class Psvr2ToolkitSetup
{
    private static bool Running(string name)
    {
        var processes = Process.GetProcessesByName(name);
        try { return processes.Length > 0; }
        finally { foreach (var process in processes) process.Dispose(); }
    }
    private static void RequireIdle(string root)
    {
        foreach (var name in new[] { "vrserver", "vrmonitor", "vrcompositor", "vrstartup", "PSVR2Toolkit.UnitySetup" })
            if (Running(name)) throw new IOException("Close SteamVR / PSVR2 Toolkit UnitySetup and try again. Zamknij SteamVR / PSVR2 Toolkit UnitySetup i spróbuj ponownie.");
        // Game process policy is intentionally scoped to games; inspect this app's
        // own executable names instead (also works for elevated Sony processes).
        var appProcesses = Directory.EnumerateFiles(root, "*.exe", SearchOption.AllDirectories)
            .Select(Path.GetFileNameWithoutExtension).Where(name => !string.IsNullOrEmpty(name)).Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var name in appProcesses)
            if (Running(name!)) throw new IOException("Close the PlayStation VR2 App and try again. Zamknij aplikację PlayStation VR2 i spróbuj ponownie. (" + name + ")");
    }
    public static async Task Offer(HttpClient http, string home, bool polish, bool offline, Action<string> status, CancellationToken ct)
    {
        string T(string pl, string en) => polish ? pl : en;
        bool Ask(string text) => MessageBox.Show(Application.Current.MainWindow, text, "PSVR2 Toolkit", MessageBoxButton.YesNo,
            MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;
        if (!Ask(T(
            "Czy używasz PSVR2 i chcesz pobrać oraz zainstalować bezpłatny PSVR2 Toolkit?\n\nWersja: " + Psvr2Toolkit.Version + " (eksperymentalna, zalecana przez autorów). Dodaje m.in. eye tracking. Zamknij SteamVR i aplikację PlayStation VR2 przed instalacją. Oryginalny sterownik Sony zostanie zachowany. Toolkit jest niezależny od moda gry.\n\nŹródło: github.com/BnuuySolutions/PSVR2Toolkit",
            "Do you use PSVR2 and want to download and install the free PSVR2 Toolkit?\n\nVersion: " + Psvr2Toolkit.Version + " (experimental, recommended by its authors). Adds eye tracking and other features. Close SteamVR and the PlayStation VR2 App before installation. The original Sony driver will be preserved. Toolkit is separate from the game mod.\n\nSource: github.com/BnuuySolutions/PSVR2Toolkit"))) return;
        if (offline) throw new IOException(T("Wyłącz tryb offline, aby pobrać PSVR2 Toolkit.", "Turn off offline mode to download PSVR2 Toolkit."));
        var roots = Psvr2Toolkit.Discover(SteamRoots.Discover()).ToList();
        string root;
        if (roots.Count == 1) root = roots[0];
        else
        {
            var dialog = new OpenFolderDialog { Title = T("Wskaż folder PlayStation VR2 App (nie folder gry)", "Select the PlayStation VR2 App folder (not the game folder)") };
            if (dialog.ShowDialog(Application.Current.MainWindow) != true) return;
            root = dialog.FolderName;
        }
        if (!File.Exists(Paths.Safe(root, "SteamVR_Plug-In/bin/win64/driver_playstation_vr2.dll")))
            throw new IOException(T("Najpierw zainstaluj aplikację PlayStation VR2 ze Steam. Wybrany folder nie zawiera jej sterownika.", "Install the PlayStation VR2 App from Steam first. The selected folder does not contain its driver."));
        // Give a user running VR a retry path without repeating the game installation.
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            try { RequireIdle(root); break; }
            catch (IOException e) { if (!Ask(e.Message + T("\n\nSpróbować ponownie?", "\n\nTry again?"))) return; }
        }
        status(T("Pobieranie i weryfikacja PSVR2 Toolkit…", "Downloading and verifying PSVR2 Toolkit…"));
        var cache = Path.Combine(home, "psvr2-toolkit", "cache");
        var archive = await Psvr2Toolkit.DownloadAsync(http, Psvr2Toolkit.Driver, cache, ct);
        var stage = Paths.Safe(Path.Combine(home, "psvr2-toolkit"), "driver-" + Guid.NewGuid().ToString("N"));
        await Task.Run(() =>
        {
            Psvr2Toolkit.Extract(archive, stage);
            ct.ThrowIfCancellationRequested();
            Psvr2Toolkit.InstallDriver(root, stage, () => RequireIdle(root));
        }, ct);
        status(T("PSVR2 Toolkit zainstalowany i zweryfikowany.", "PSVR2 Toolkit installed and verified."));
        if (!Ask(T("PSVR2 Toolkit jest zainstalowany. Czy chcesz teraz skalibrować eye tracking?\n\nPobiorę UnitySetup i uruchomię SteamVR. Włącz i załóż PSVR2, a w UnitySetup wybierz kalibrację oczu (Eye Tracking / Gaze Calibration).",
            "PSVR2 Toolkit is installed. Calibrate eye tracking now?\n\nI will download UnitySetup and start SteamVR. Turn on and wear your PSVR2, then select Eye Tracking / Gaze Calibration in UnitySetup."))) return;
        status(T("Pobieranie i weryfikacja narzędzia kalibracji…", "Downloading and verifying the calibration tool…"));
        var setup = await Psvr2Toolkit.DownloadAsync(http, Psvr2Toolkit.Calibration, cache, ct);
        var tools = Paths.Safe(Path.Combine(home, "psvr2-toolkit"), "unitysetup-1.1.0-" + Guid.NewGuid().ToString("N"));
        await Task.Run(() => Psvr2Toolkit.Extract(setup, tools), ct);
        var exe = Paths.Safe(tools, Psvr2Toolkit.CalibrationExe);
        if (!File.Exists(exe)) throw new InvalidDataException("Missing PSVR2Toolkit.UnitySetup.exe");
        ct.ThrowIfCancellationRequested();
        if (!Running("vrserver"))
        {
            Process.Start(new ProcessStartInfo("steam://rungameid/250820") { UseShellExecute = true });
            status(T("Oczekiwanie na uruchomienie SteamVR…", "Waiting for SteamVR to start…"));
            var timeout = Stopwatch.StartNew();
            while (!Running("vrserver"))
            {
                if (timeout.Elapsed > TimeSpan.FromSeconds(60)) throw new IOException(T("SteamVR nie uruchomił się. Uruchom go ręcznie i ponów konfigurację PSVR2 Toolkit.", "SteamVR did not start. Start it manually and retry PSVR2 Toolkit setup."));
                await Task.Delay(500, ct);
            }
        }
        ct.ThrowIfCancellationRequested();
        Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, WorkingDirectory = tools });
        status(T("Uruchomiono UnitySetup. W goglach wybierz kalibrację oczu. Wynik kalibracji sprawdź w UnitySetup.",
            "UnitySetup launched. Select eye tracking calibration in the headset. Check the calibration result in UnitySetup."));
    }
}
