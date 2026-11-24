namespace Trilex.Installer.Core;

public static class Borderlands3Runtime
{
    public const string GameId = "borderlands3";
    public const string Bin = "OakGame/Binaries/Win64/";
    public const string Executable = Bin + "Borderlands3.exe";
    public const string Profile = Bin + "Borderlands3VR.ini";
    public const string Loader = Bin + "BL3VR/openxr_loader.dll";

    public static List<ModuleManifest> Configure(List<ModuleManifest> modules, string root)
    {
        var manifest = SteamRoots.Libraries(SteamRoots.Discover())
            .Select(library => Path.Combine(library, "steamapps", "common", "SteamVR", "steamxr_win64.json"))
            .FirstOrDefault(File.Exists)
            ?? throw new InvalidOperationException("Install SteamVR before setting up Borderlands 3 VR.");
        var localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return modules.Select(module => module with
        {
            Files = module.Files.Select(file => file.Kind == "ini" && Paths.Same(file.Path, Profile)
                ? file with
                {
                    IniValues = new Dictionary<string, string>(file.IniValues ?? [], StringComparer.OrdinalIgnoreCase)
                    {
                        ["OpenXR/Loader"] = Paths.Safe(root, Loader),
                        ["OpenXR/RuntimeManifest"] = manifest,
                        ["OpenXR/SimulatorDataDir"] = Paths.Safe(root, Bin.TrimEnd('/')),
                        ["OpenXR/AdaptiveTriggerLoader"] = Psvr2ClientRuntime.LoaderPath(localData)
                    }
                }
                : file).ToList()
        }).ToList();
    }
}
