namespace Trilex.Installer.Core;

public static class ProcessGuardPolicy
{
    // Steam/VR tools and the extraction drive are unrelated to game writes.
    // File-level transaction checks remain authoritative for locked mod files.
    public static string? Refusal(string root, int installerPid, int pid, string name, string? path)
    {
        if(pid==installerPid)return null;
        if(File.Exists(Path.Combine(root,"OakGame","Binaries","Win64","Wonderlands.exe")))
        {
            if(!Paths.Same(name,"Wonderlands")&&!Paths.Same(name,"Wonderlands.exe")&&!Paths.Same(Path.GetFileName(path),"Wonderlands.exe"))return null;
            return $"Installation blocked by Tiny Tina's Wonderlands (PID {pid}). Close the game normally; no process will be terminated.";
        }
        if(File.Exists(Path.Combine(root,Borderlands3Runtime.Executable)))
        {
            if(!Paths.Same(name,"Borderlands3")&&!Paths.Same(name,"Borderlands3.exe")&&!Paths.Same(Path.GetFileName(path),"Borderlands3.exe"))return null;
            return $"Installation blocked by Borderlands 3 (PID {pid}). Close the game normally; no process will be terminated.";
        }
        if(File.Exists(Path.Combine(root,"Bin64","Rapture_Release.exe")))
        {
            if(!Paths.Same(name,"Rapture_Release")&&!Paths.Same(name,"Rapture_Release.exe")&&!Paths.Same(Path.GetFileName(path),"Rapture_Release.exe"))return null;
            return $"Installation blocked by Everybody's Gone to the Rapture (PID {pid}). Close the game normally; no process will be terminated.";
        }
        if(File.Exists(Path.Combine(root,"DarknessII.exe")))
        {
            if(!Paths.Same(name,"DarknessII") && !Paths.Same(name,"DarknessII.exe") &&
               !Paths.Same(Path.GetFileName(path),"DarknessII.exe"))return null;
            return $"Installation blocked by The Darkness II (PID {pid}). Close the game normally; no process will be terminated.";
        }
        var gameName=name.Equals("Singularity",StringComparison.OrdinalIgnoreCase)||
            name.Equals("Singularity.exe",StringComparison.OrdinalIgnoreCase);
        var gamePath=path is not null&&Path.GetFileName(path.Replace('\\','/'))
            .Equals("Singularity.exe",StringComparison.OrdinalIgnoreCase);
        if(!gameName&&!gamePath)return null;
        return $"Installation blocked by Singularity (PID {pid}). Path: {path??"unavailable"}. Close the game normally; no process will be terminated.";
    }
}
