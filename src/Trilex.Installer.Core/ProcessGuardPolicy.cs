namespace Trilex.Installer.Core;

public static class ProcessGuardPolicy
{
    // The installer can itself be unpacked below steamapps/common. It is not
    // a game. Exempt only this PID, not other processes with the same name.
    public static string? Refusal(string root, int installerPid, int pid, string name, string? path)
    {
        if(pid==installerPid)return null;
        var target=Path.TrimEndingDirectorySeparator(Path.GetFullPath(root))+Path.DirectorySeparatorChar;
        var inTarget=path is not null&&path.StartsWith(target,StringComparison.OrdinalIgnoreCase);
        var steamProcess=path is not null&&!SteamRuntimePolicy.IsRuntimePath(path)&&
            path.Contains(@"\steamapps\common\",StringComparison.OrdinalIgnoreCase);
        if(!name.Equals("Singularity",StringComparison.OrdinalIgnoreCase)&&!inTarget&&!steamProcess)return null;
        var reason=inTarget?"process in the selected game folder":
            name.Equals("Singularity",StringComparison.OrdinalIgnoreCase)?"Singularity process":"process in a Steam application folder";
        return $"Installation blocked by {name} (PID {pid}): {reason}. Path: {path??"unavailable"}. Close it normally. If this is a tool rather than a game, send this message and the installer log; no process will be terminated.";
    }
}
