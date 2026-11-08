using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using Trilex.Installer.Core;

internal static class Psvr2ToolkitTests
{
    public static async Task Run(Func<string, Func<Task>, Task> test, string testRoot)
    {
        void Check(bool value) { if (!value) throw new Exception("Toolkit assertion failed"); }
        string Fresh() { var path = Path.Combine(testRoot, "toolkit-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(path); return path; }
        (string App, string Bin, string Stage) Fixture()
        {
            var app = Fresh(); var bin = Paths.Safe(app, "SteamVR_Plug-In/bin/win64"); Directory.CreateDirectory(bin);
            var stage = Fresh();
            File.WriteAllText(Paths.Safe(bin, Psvr2Toolkit.DriverFiles[0]), "SONY ORIGINAL");
            foreach (var name in Psvr2Toolkit.DriverFiles) File.WriteAllText(Paths.Safe(stage, name), "TOOLKIT " + name);
            return (app, bin, stage);
        }
        void Refuse(Action action) { try { action(); } catch (Exception e) when (e is IOException or InvalidDataException) { return; } throw new Exception("Expected Toolkit refusal"); }
        await test("Toolkit install/reinstall preserves Sony original and backup hashes", () =>
        {
            var f = Fixture();
            Psvr2Toolkit.InstallDriver(f.App, f.Stage, () => { });
            Psvr2Toolkit.InstallDriver(f.App, f.Stage, () => { });
            Check(File.ReadAllText(Paths.Safe(f.Bin, "driver_playstation_vr2_orig.dll")) == "SONY ORIGINAL");
            Check(Directory.GetDirectories(f.Bin, "trilex-toolkit-backup-*").Length == 1);
            foreach (var name in Psvr2Toolkit.DriverFiles) Check(Paths.Hash(Paths.Safe(f.Bin, name)) == Paths.Hash(Paths.Safe(f.Stage, name)));
            return Task.CompletedTask;
        });
        await test("Toolkit refuses active VR before modifying files", () =>
        {
            var f = Fixture(); Refuse(() => Psvr2Toolkit.InstallDriver(f.App, f.Stage, () => throw new IOException("SteamVR running")));
            Check(File.ReadAllText(Paths.Safe(f.Bin, Psvr2Toolkit.DriverFiles[0])) == "SONY ORIGINAL");
            Check(!File.Exists(Paths.Safe(f.Bin, "driver_playstation_vr2_orig.dll")));
            return Task.CompletedTask;
        });
        await test("Toolkit rolls back and verifies partial driver installation", () =>
        {
            var f = Fixture();
            // Cause a late write failure after the wrapper and Sony backup have been installed.
            using (var locked = new FileStream(Paths.Safe(f.Stage, Psvr2Toolkit.DriverFiles[^1]), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var calls = 0;
                Refuse(() => Psvr2Toolkit.InstallDriver(f.App, f.Stage, () =>
                {
                    if (++calls == 2) File.Delete(Paths.Safe(f.Stage, Psvr2Toolkit.DriverFiles[1]));
                }));
            }
            Check(File.ReadAllText(Paths.Safe(f.Bin, Psvr2Toolkit.DriverFiles[0])) == "SONY ORIGINAL");
            Check(!File.Exists(Paths.Safe(f.Bin, "driver_playstation_vr2_orig.dll")));
            Check(!File.Exists(Paths.Safe(f.Bin, "trilex-toolkit-pending.json")));
            return Task.CompletedTask;
        });
        await test("Toolkit recovers interrupted transaction before reinstall", () =>
        {
            var f = Fixture();
            var backupName = "trilex-toolkit-backup-" + Guid.NewGuid().ToString("N");
            var backup = Paths.Safe(f.Bin, backupName); Directory.CreateDirectory(backup);
            var previous = Psvr2Toolkit.DriverFiles.Append("driver_playstation_vr2_orig.dll").ToDictionary(n => n, n => n == Psvr2Toolkit.DriverFiles[0] ? Paths.Hash(Paths.Safe(f.Bin, n)) : null);
            Paths.AtomicCopy(Paths.Safe(f.Bin, Psvr2Toolkit.DriverFiles[0]), Paths.Safe(backup, Psvr2Toolkit.DriverFiles[0]));
            var expected = Psvr2Toolkit.DriverFiles.ToDictionary(n => n, n => Paths.Hash(Paths.Safe(f.Stage, n)));
            expected["driver_playstation_vr2_orig.dll"] = previous[Psvr2Toolkit.DriverFiles[0]]!;
            Paths.AtomicCopy(Paths.Safe(f.Stage, Psvr2Toolkit.DriverFiles[0]), Paths.Safe(f.Bin, Psvr2Toolkit.DriverFiles[0]));
            Json.Write(Paths.Safe(f.Bin, "trilex-toolkit-pending.json"), new ToolkitJournal(backupName, previous, expected));
            Psvr2Toolkit.InstallDriver(f.App, f.Stage, () => { });
            Check(File.ReadAllText(Paths.Safe(f.Bin, "driver_playstation_vr2_orig.dll")) == "SONY ORIGINAL");
            return Task.CompletedTask;
        });
        await test("Toolkit refuses wrapper missing original Sony driver", () =>
        {
            var f = Fixture(); Paths.AtomicCopy(Paths.Safe(f.Stage, Psvr2Toolkit.DriverFiles[0]), Paths.Safe(f.Bin, Psvr2Toolkit.DriverFiles[0]));
            Refuse(() => Psvr2Toolkit.InstallDriver(f.App, f.Stage, () => { }));
            Check(!File.Exists(Paths.Safe(f.Bin, "driver_playstation_vr2_orig.dll")));
            return Task.CompletedTask;
        });
        await test("Toolkit discovers PSVR2 app in a second Steam library", () =>
        {
            var steam = Fresh(); var library = Fresh();
            Directory.CreateDirectory(Path.Combine(steam, "steamapps")); Directory.CreateDirectory(Path.Combine(library, "steamapps"));
            File.WriteAllText(Path.Combine(steam, "steamapps/libraryfolders.vdf"), "\"libraryfolders\" { \"1\" { \"path\" \"" + library.Replace("\\", "\\\\") + "\" } }");
            File.WriteAllText(Path.Combine(library, "steamapps/appmanifest_2580190.acf"), "\"AppState\" { \"appid\" \"2580190\" \"installdir\" \"PSVR2\" }");
            var root = Paths.Safe(library, "steamapps/common/PSVR2"); var driver = Paths.Safe(root, "SteamVR_Plug-In/bin/win64/driver_playstation_vr2.dll");
            Directory.CreateDirectory(Path.GetDirectoryName(driver)!); File.WriteAllText(driver, "SONY");
            Check(Psvr2Toolkit.Discover([steam]).Single() == root);
            return Task.CompletedTask;
        });
        await test("Toolkit extraction rejects archive traversal", () =>
        {
            var root = Fresh(); var zip = Path.Combine(root, "unsafe.zip");
            using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create)) { using var w = new StreamWriter(archive.CreateEntry("../outside.dll").Open()); w.Write("bad"); }
            Refuse(() => Psvr2Toolkit.Extract(zip, Path.Combine(root, "stage")));
            Check(!File.Exists(Path.Combine(root, "outside.dll")));
            return Task.CompletedTask;
        });
        await test("Toolkit downloader refuses tampering and cleans partial files", async () =>
        {
            var root = Fresh(); var bytes = new byte[] { 1, 2, 3 }; using var http = new HttpClient(new Handler(bytes));
            var package = new ToolkitPackage("https://example.test/toolkit.zip", new string('a', 64), 3);
            var refused = false;
            try { await Psvr2Toolkit.DownloadAsync(http, package, root, default); } catch (InvalidDataException) { refused = true; }
            Check(refused && Directory.GetFiles(root).Length == 0);
            package = package with { Sha256 = Convert.ToHexString(SHA256.HashData(bytes)) };
            var cached = await Psvr2Toolkit.DownloadAsync(http, package, root, default); Check(File.ReadAllBytes(cached).SequenceEqual(bytes));
        });
        if (Environment.GetEnvironmentVariable("TRILEX_PSVR2_UPSTREAM_ROOT") is string upstream)
            await test("Toolkit real upstream ZIP digests, layout and copied driver install", () =>
            {
                foreach (var (name, package) in new[] { ("driver.zip", Psvr2Toolkit.Driver), ("setup.zip", Psvr2Toolkit.Calibration), ("capi.zip", Psvr2Toolkit.CapiApps) })
                {
                    var zip = Path.Combine(upstream, name);
                    Check(new FileInfo(zip).Length == package.Bytes && Paths.Same(Paths.Hash(zip), package.Sha256));
                    var stage = Fresh(); Psvr2Toolkit.Extract(zip, stage);
                    if (name == "driver.zip")
                    {
                        var f = Fixture(); Psvr2Toolkit.InstallDriver(f.App, stage, () => { });
                        foreach (var file in Psvr2Toolkit.DriverFiles) Check(Paths.Hash(Paths.Safe(f.Bin, file)) == Paths.Hash(Paths.Safe(stage, file)));
                        Check(File.ReadAllText(Paths.Safe(f.Bin, "driver_playstation_vr2_orig.dll")) == "SONY ORIGINAL");
                    }
                    else if(name == "setup.zip") Check(File.Exists(Paths.Safe(stage, Psvr2Toolkit.CalibrationExe)) && File.Exists(Paths.Safe(stage, "UnityPlayer.dll")));
                    else Check(Directory.GetFiles(stage,"*.exe",SearchOption.AllDirectories).Length > 0);
                }
                return Task.CompletedTask;
            });
    }
    private sealed class Handler(byte[] bytes) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
    }
}
