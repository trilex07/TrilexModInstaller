using System.Diagnostics;

namespace Trilex.Installer.Core;

public static class GameLauncher
{
    public static bool CanLaunch(GameManifest game,GameLocation? location,InstallReceipt? receipt)=>
        location?.ExeHash is { } hash && receipt?.GameId==game.Id &&
        Paths.Same(Path.GetFullPath(receipt.Root),Path.GetFullPath(location.Root)) &&
        ((location.Compatible&&game.ExeSha256.Any(h=>Paths.Same(h,hash))) ||
         ExecutableCompatibility.Matches(receipt.ExecutableConsent,location.Root,hash));

    // Only prepare the launch here; tests never start the game.
    public static ProcessStartInfo CreateStartInfo(GameManifest game,GameLocation location,InstallReceipt receipt)
    {
        if(!CanLaunch(game,location,receipt))throw new InvalidOperationException("Verify a managed installation first");
        var exe=Paths.Safe(location.Root,game.Executable);
        if(!Paths.Same(Paths.Hash(exe),location.ExeHash))throw new IOException("Executable changed after review");
        // Steam-owned copies must enter through Steam so its launcher/DRM context
        // is initialized. Never infer ownership from an EXE hash or Source label.
        if(IsSteamInstallation(game,location.Root))
            return new(game.Id=="wonderlands"?"steam://run/"+game.SteamAppId+"//-dx11/":"steam://rungameid/"+game.SteamAppId){UseShellExecute=true};
        return new(exe){UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(exe)!,Arguments=game.Id=="wonderlands"?"-dx11":""};
    }
    public static bool IsSteamInstallation(GameManifest game,string root)
    {
        if(!System.Text.RegularExpressions.Regex.IsMatch(game.SteamAppId,@"\A[0-9]+\z"))return false;
        try
        {
            var directory=new DirectoryInfo(Path.GetFullPath(root));
            var common=directory.Parent;var apps=common?.Parent;
            if(!Paths.Same(common?.Name??"","common")||!Paths.Same(apps?.Name??"","steamapps"))return false;
            var manifest=Path.Combine(apps!.FullName,"appmanifest_"+game.SteamAppId+".acf");
            if(!File.Exists(manifest))return false;
            var parsed=Vdf.Parse(File.ReadAllText(manifest));
            return parsed.GetValueOrDefault("AppState") is Dictionary<string,object> state &&
                state.GetValueOrDefault("appid") as string==game.SteamAppId &&
                state.GetValueOrDefault("installdir") is string install &&
                Paths.Same(Paths.Safe(common!.FullName,install),directory.FullName);
        }
        catch(Exception e)when(e is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException){return false;}
    }
}
