using System.Diagnostics;
namespace Trilex.Installer.Core;

public static class LocalCenturionGenerator
{
    private const string Original="B06D1516E5F4AE15BD9225A38E52BA706EC32D9C6D0E060320149A2F38DA44AF";
    public static string Export(string source,string staging)
    {
        if(!Paths.Same(Paths.Hash(source),Original))throw new InvalidDataException("Centurion original identity mismatch");
        var root=Path.GetFullPath(staging);while(root.Length>100)root=Path.GetDirectoryName(root)??throw new IOException("Short extractor workspace unavailable");
        var folder=Paths.Safe(root,"_local-centurion/"+Original[..16]);Directory.CreateDirectory(folder);
        var extractor=LocalExtractorTools.Resolve();
        var input=Path.Combine(folder,"input");Directory.CreateDirectory(input);var owned=Path.Combine(input,"wp_SP_ARPistol_SF.xxx");
        if(!File.Exists(owned))Paths.AtomicCopy(source,owned);
        if(!Paths.Same(Paths.Hash(owned),Original))throw new IOException("Local extractor input changed");
        var exports=Path.Combine(folder,"export");Directory.CreateDirectory(exports);
        foreach(var (name,kind,relative) in new[]{("ch_inview_alt_pistol_mesh","SkeletalMesh","SkeletalMesh3/ch_inview_alt_pistol_mesh.psk"),("Alt_pistol","AnimSet","AnimSet/Alt_pistol.psa"),("shell_rifle","StaticMesh","StaticMesh3/shell_rifle.pskx")})
        {
            var expected=Paths.Safe(exports,"wp_SP_ARPistol_SF/"+relative);
            if(File.Exists(expected))continue;
            // Resolve again immediately before execution, never search PATH or a package executable.
            extractor=LocalExtractorTools.Resolve();
            var start=new ProcessStartInfo(extractor){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=folder};
            foreach(var arg in new[]{"-export","-notex","-path="+input,"-out="+exports,"wp_SP_ARPistol_SF",name,kind})start.ArgumentList.Add(arg);
            using var process=Process.Start(start)??throw new IOException("Cannot start local extractor");
            var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
            if(!process.WaitForExit(30000)){process.Kill(true);process.WaitForExit();throw new IOException("Local extractor timeout");}
            Task.WaitAll(stdout,stderr);
            if(process.ExitCode!=0||!File.Exists(expected)||new FileInfo(expected).Length>16*1024*1024)throw new IOException("Bounded Centurion export failed: "+name);
        }
        return exports;
    }
}
