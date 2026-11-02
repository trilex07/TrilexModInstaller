namespace Trilex.Installer.Core;

public static class ProcessGuardPolicy
{
    // Steam/VR tools and the extraction drive are unrelated to game writes.
    // File-level transaction checks remain authoritative for locked mod files.
    public static string? Refusal(string root, int installerPid, int pid, string name, string? path)
    {
        if(pid==installerPid)return null;
        var gameName=name.Equals("Singularity",StringComparison.OrdinalIgnoreCase)||
            name.Equals("Singularity.exe",StringComparison.OrdinalIgnoreCase);
        var gamePath=path is not null&&Path.GetFileName(path.Replace('\\','/'))
            .Equals("Singularity.exe",StringComparison.OrdinalIgnoreCase);
        if(!gameName&&!gamePath)return null;
        return $"Installation blocked by Singularity (PID {pid}). Path: {path??"unavailable"}. Close the game normally; no process will be terminated.";
    }
}
