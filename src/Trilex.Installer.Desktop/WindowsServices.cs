using System.Diagnostics;
using Microsoft.Win32;
using Trilex.Installer.Core;

namespace Trilex.Installer.Desktop;
public sealed class WindowsProcessGuard : IProcessGuard
{
    public void RequireIdle(string root)
    {
        using var apps=Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam\Apps");
        if(apps is not null)foreach(var id in apps.GetSubKeyNames()){if(id=="250820")continue;using var app=apps.OpenSubKey(id);if(Convert.ToInt32(app?.GetValue("Running",0))!=0)throw new IOException("A Steam game is running. Close it normally; no process will be terminated.");}
        foreach(var process in Process.GetProcesses())
        {
            using(process)
            {
                if(process.ProcessName.Equals("Singularity",StringComparison.OrdinalIgnoreCase))throw new IOException("Singularity is running. Close it normally before installation.");
                string? path;try{path=process.MainModule?.FileName;}catch(System.ComponentModel.Win32Exception){continue;}catch(InvalidOperationException){continue;}
                var runtimeTool=path?.Contains(@"\steamapps\common\SteamVR\",StringComparison.OrdinalIgnoreCase)==true;
                if(path is not null && !runtimeTool && (path.StartsWith(Path.GetFullPath(root)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase) || path.Contains(@"\steamapps\common\",StringComparison.OrdinalIgnoreCase)))throw new IOException("A game is active. Installation is deferred until it exits.");
            }
        }
    }
}
public static class SteamRoots
{
    public static IEnumerable<string> Discover()
    {
        using var key=Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
        if(key?.GetValue("SteamPath") is string path)yield return path;
        using var machine=Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam");
        if(machine?.GetValue("InstallPath") is string other)yield return other;
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Steam");
    }
}
