namespace Trilex.Installer.Core;

// Steam reports overlay utilities as running apps too. Only these exact tools
// are exempt; actual games and processes inside the installation remain blocked.
public static class SteamRuntimePolicy
{
    public static bool IsRuntimeApp(string appId)=>appId is "250820" or "908520";
    public static bool IsRuntimePath(string path)=>new[]{"SteamVR","fpsVR"}.Any(name=>
        path.Contains(@"\steamapps\common\"+name+@"\",StringComparison.OrdinalIgnoreCase));
}
