using System.Diagnostics;
using Microsoft.Win32;
using Trilex.Installer.Core;

namespace Trilex.Installer.Desktop;
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
