using System.Diagnostics;
using Microsoft.Win32;
using Trilex.Installer.Core;

namespace Trilex.Installer.Desktop;
public sealed class WindowsProcessGuard : IProcessGuard
{
    public void RequireIdle(string root)
    {
        // Installed/retail copies do not require Steam. Its Running registry
        // values can describe unrelated tools or stale sessions, not this game.
        foreach(var process in Process.GetProcesses())
        {
            using(process)
            {
                if(process.Id==Environment.ProcessId)continue;
                string name;try{name=process.ProcessName;}catch(InvalidOperationException){continue;}
                string? path=null;try{path=process.MainModule?.FileName;}catch(System.ComponentModel.Win32Exception){}catch(InvalidOperationException){continue;}
                var refusal=ProcessGuardPolicy.Refusal(root,Environment.ProcessId,process.Id,name,path);
                if(refusal is not null)throw new IOException(refusal);
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
