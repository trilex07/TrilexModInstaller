using System.IO.Compression;

namespace Trilex.Installer.Core;

// Upstream Windows packages, pinned to the digests published by GitHub.
public sealed record ToolkitPackage(string Url, string Sha256, long Bytes);
public sealed record ToolkitJournal(string Backup, Dictionary<string, string?> Previous, Dictionary<string, string> Expected);
public static class Psvr2Toolkit
{
    public const string Version = "1.0.0-experimental-3";
    public const string CalibrationExe = "PSVR2Toolkit.UnitySetup.exe";
    public static readonly ToolkitPackage Driver = new(
        "https://github.com/BnuuySolutions/PSVR2Toolkit/releases/download/v1.0.0-experimental-3/PSVR2TK-win64-Windows.zip",
        "e3eca6151c422bd7896bbfd08025b48ceac90e7b20a7f3362b89613ac2dd9bd0", 895585);
    public static readonly ToolkitPackage Calibration = new(
        "https://github.com/BnuuySolutions/PSVR2Toolkit.UnitySetup/releases/download/v1.1.0/PSVR2Toolkit.UnitySetup-Windows.zip",
        "9fb240659188ae1c6549082592a8bd4943dd7ead689fe87278f4600647d62c7b", 47789591);
    public static readonly ToolkitPackage CapiApps = new(
        "https://github.com/BnuuySolutions/PSVR2Toolkit/releases/download/v1.0.0-experimental-3/CAPIApps-Windows.zip",
        "79df77c32355946db0c5d02a4ca045998b912e746c3f60098b372bf2b41ed933", 1363067);
    public static readonly string[] DriverFiles = ["driver_playstation_vr2.dll", "libcrossipc.dll", "libusb-1.0.dll", "psvr2_toolkit_capi.dll"];

    public static IEnumerable<string> Discover(IEnumerable<string> steamRoots)
    {
        var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in steamRoots.Where(Directory.Exists))
        {
            libraries.Add(Path.GetFullPath(root));
            var file = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(file)) continue;
            if (Vdf.Parse(File.ReadAllText(file)).GetValueOrDefault("libraryfolders") is Dictionary<string, object> entries)
                foreach (var (key, value) in entries)
                    if (int.TryParse(key, out _))
                    {
                        if (value is Dictionary<string, object> child && child.GetValueOrDefault("path") is string path) libraries.Add(path);
                        else if (value is string old) libraries.Add(old);
                    }
        }
        foreach (var library in libraries)
        {
            var manifest = Path.Combine(library, "steamapps", "appmanifest_2580190.acf");
            if (!File.Exists(manifest)) continue;
            if (Vdf.Parse(File.ReadAllText(manifest)).GetValueOrDefault("AppState") is not Dictionary<string, object> app ||
                app.GetValueOrDefault("appid") as string != "2580190" || app.GetValueOrDefault("installdir") is not string dir) continue;
            var root = Paths.Safe(Path.Combine(library, "steamapps", "common"), dir);
            if (File.Exists(Paths.Safe(root, "SteamVR_Plug-In/bin/win64/driver_playstation_vr2.dll"))) yield return root;
        }
    }

    public static async Task<string> DownloadAsync(HttpClient http, ToolkitPackage package, string cache, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            try { return await DownloadOnce(http, package, cache, ct); }
            catch (Exception e) when (attempt < 2 && !ct.IsCancellationRequested && (e is HttpRequestException or TaskCanceledException))
            { await Task.Delay(TimeSpan.FromSeconds(attempt + 1), ct); }
        }
    }

    private static async Task<string> DownloadOnce(HttpClient http, ToolkitPackage package, string cache, CancellationToken ct)
    {
        Directory.CreateDirectory(cache);
        var target = Paths.Safe(cache, package.Sha256 + ".zip");
        if (File.Exists(target) && new FileInfo(target).Length == package.Bytes && Paths.Same(Paths.Hash(target), package.Sha256)) return target;
        var temp = target + "." + Guid.NewGuid().ToString("N") + ".part";
        try
        {
            using var response = await http.GetAsync(package.Url, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
            await using (var input = await response.Content.ReadAsStreamAsync(ct))
            await using (var output = new FileStream(temp, FileMode.CreateNew))
            {
                var buffer = new byte[81920]; long total = 0; int count;
                while ((count = await input.ReadAsync(buffer, ct)) > 0)
                {
                    total += count;
                    if (total > package.Bytes) throw new InvalidDataException("PSVR2 Toolkit download is larger than expected.");
                    await output.WriteAsync(buffer.AsMemory(0, count), ct);
                }
            }
            if (new FileInfo(temp).Length != package.Bytes || !Paths.Same(Paths.Hash(temp), package.Sha256))
                throw new InvalidDataException("PSVR2 Toolkit download failed SHA256/size verification.");
            File.Move(temp, target, true);
            return target;
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    public static void Extract(string archive, string destination)
    {
        using var zip = ZipFile.OpenRead(archive);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        foreach (var entry in zip.Entries)
        {
            var name = entry.FullName.Replace('\\', '/');
            if (name.EndsWith('/')) continue;
            var target = Paths.Safe(destination, name);
            if (!names.Add(target) || (total += entry.Length) > 512L * 1024 * 1024) throw new InvalidDataException("Invalid PSVR2 Toolkit archive.");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, false);
        }
    }

    // All previous files stay in a sibling backup. The journal survives process interruption.
    public static void InstallDriver(string appRoot, string stagedDriver, Action requireIdle)
    {
        requireIdle();
        var bin = Paths.Safe(appRoot, "SteamVR_Plug-In/bin/win64");
        var journal = Paths.Safe(bin, "trilex-toolkit-pending.json");
        using var installationLock = new FileStream(Paths.Safe(bin, "trilex-toolkit.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        if (File.Exists(journal)) Restore(bin, journal, requireIdle);
        foreach (var name in DriverFiles)
            if (!File.Exists(Paths.Safe(stagedDriver, name))) throw new InvalidDataException("Missing Toolkit file: " + name);
        var original = Paths.Safe(bin, "driver_playstation_vr2_orig.dll");
        var driver = Paths.Safe(bin, DriverFiles[0]);
        if (!File.Exists(driver)) throw new IOException("Missing Sony PSVR2 driver.");
        // A previous wrapper without its required Sony original is ambiguous: never rename it as Sony.
        if (!File.Exists(original) && (File.Exists(Paths.Safe(bin, "psvr2_toolkit_capi.dll")) || File.Exists(Paths.Safe(bin, "libcrossipc.dll")) || Paths.Same(Paths.Hash(driver), Paths.Hash(Paths.Safe(stagedDriver, DriverFiles[0])))))
            throw new IOException("PSVR2 Toolkit driver found without driver_playstation_vr2_orig.dll. Restore the PlayStation VR2 App through Steam first.");
        if (File.Exists(original) && Paths.Same(Paths.Hash(original), Paths.Hash(Paths.Safe(stagedDriver, DriverFiles[0]))))
            throw new IOException("The Sony original is a Toolkit wrapper. Restore the PlayStation VR2 App through Steam first.");
        if (File.Exists(original) && DriverFiles.All(name => File.Exists(Paths.Safe(bin, name)) && Paths.Same(Paths.Hash(Paths.Safe(bin, name)), Paths.Hash(Paths.Safe(stagedDriver, name))))) return;
        var backup = Paths.Safe(bin, "trilex-toolkit-backup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(backup);
        var previous = DriverFiles.Append("driver_playstation_vr2_orig.dll")
            .ToDictionary(name => name, name => File.Exists(Paths.Safe(bin, name)) ? Paths.Hash(Paths.Safe(bin, name)) : null);
        foreach (var (name, hash) in previous)
            if (hash is not null) Paths.AtomicCopy(Paths.Safe(bin, name), Paths.Safe(backup, name));
        foreach (var (name, hash) in previous)
            if (hash is not null && !Paths.Same(Paths.Hash(Paths.Safe(backup, name)), hash)) throw new IOException("Toolkit backup verification failed: " + name);
        var expected = DriverFiles.ToDictionary(name => name, name => Paths.Hash(Paths.Safe(stagedDriver, name)));
        expected["driver_playstation_vr2_orig.dll"] = previous["driver_playstation_vr2_orig.dll"] ?? previous[DriverFiles[0]]!;
        Json.Write(journal, new ToolkitJournal(Path.GetFileName(backup), previous, expected));
        try
        {
            requireIdle();
            if (!File.Exists(original)) Paths.AtomicCopy(driver, original);
            foreach (var name in DriverFiles) Paths.AtomicCopy(Paths.Safe(stagedDriver, name), Paths.Safe(bin, name));
            foreach (var name in DriverFiles)
                if (!Paths.Same(Paths.Hash(Paths.Safe(bin, name)), Paths.Hash(Paths.Safe(stagedDriver, name)))) throw new IOException("Toolkit verification failed: " + name);
            if (!Paths.Same(Paths.Hash(original), expected["driver_playstation_vr2_orig.dll"])) throw new IOException("Sony original verification failed.");
            File.Delete(journal);
        }
        catch
        {
            Restore(bin, journal, requireIdle);
            throw;
        }
    }

    private static void Restore(string bin, string journal, Action requireIdle)
    {
        requireIdle();
        var state = Json.Read<ToolkitJournal>(journal);
        var names = DriverFiles.Append("driver_playstation_vr2_orig.dll").ToHashSet(StringComparer.Ordinal);
        if (!names.SetEquals(state.Previous.Keys) || !names.SetEquals(state.Expected.Keys) ||
            !System.Text.RegularExpressions.Regex.IsMatch(state.Backup, "^trilex-toolkit-backup-[a-f0-9]{32}$"))
            throw new InvalidDataException("Invalid Toolkit recovery journal.");
        var backup = Paths.Safe(bin, state.Backup);
        // Refuse recovery over changes made by Steam or another installer after interruption.
        foreach (var name in names)
        {
            var target = Paths.Safe(bin, name);
            var oldHash = state.Previous[name];
            if (oldHash is not null && !Paths.Same(Paths.Hash(Paths.Safe(backup, name)), oldHash))
                throw new IOException("Toolkit recovery backup changed: " + name);
            if (File.Exists(target))
            {
                var hash = Paths.Hash(target);
                if (!Paths.Same(hash, oldHash ?? "") && !Paths.Same(hash, state.Expected[name]))
                    throw new IOException("Toolkit recovery refused an externally changed file: " + target);
            }
        }
        foreach (var name in names)
        {
            var target = Paths.Safe(bin, name);
            if (state.Previous[name] is not string hash) { if (File.Exists(target)) File.Delete(target); }
            else
            {
                Paths.AtomicCopy(Paths.Safe(backup, name), target);
                if (!Paths.Same(Paths.Hash(target), hash)) throw new IOException("Toolkit rollback verification failed: " + target);
            }
        }
        File.Delete(journal);
    }
}
