using Microsoft.Win32;

namespace Trilex.Installer.Core;

public static class SteamRoots
{
    public static IEnumerable<string> Discover()
    {
        if(!OperatingSystem.IsWindows())yield break;
        using var key=Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
        if(key?.GetValue("SteamPath") is string path)yield return path;
        using var machine=Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam");
        if(machine?.GetValue("InstallPath") is string other)yield return other;
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Steam");
    }

    public static IEnumerable<string> Libraries(IEnumerable<string> steamRoots)
    {
        var libraries=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var root in steamRoots.Where(Directory.Exists))
        {
            libraries.Add(Path.GetFullPath(root));
            var vdf=Path.Combine(root,"steamapps","libraryfolders.vdf");if(!File.Exists(vdf))continue;
            Dictionary<string,object> parsed;try{parsed=Vdf.Parse(File.ReadAllText(vdf));}catch(InvalidDataException){continue;}
            if(parsed.GetValueOrDefault("libraryfolders") is Dictionary<string,object> entries)
                foreach(var (key,value) in entries)if(int.TryParse(key,out _)){if(value is Dictionary<string,object> child && child.GetValueOrDefault("path") is string path)libraries.Add(path);else if(value is string old)libraries.Add(old);}
        }
        return libraries;
    }
}
