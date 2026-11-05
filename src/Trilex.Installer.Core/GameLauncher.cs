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
        return new(exe){UseShellExecute=true,WorkingDirectory=Path.GetFullPath(location.Root)};
    }
}
