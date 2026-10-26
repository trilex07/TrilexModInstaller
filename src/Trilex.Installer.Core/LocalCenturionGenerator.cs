using System.Diagnostics;
using System.Security.Cryptography;
namespace Trilex.Installer.Core;

public static class LocalCenturionGenerator
{
    private const string Original="B06D1516E5F4AE15BD9225A38E52BA706EC32D9C6D0E060320149A2F38DA44AF";
    public static string Export(string source,string staging)
    {
        if(!Paths.Same(Paths.Hash(source),Original))throw new InvalidDataException("Centurion original identity mismatch");
        var root=Path.GetFullPath(staging);while(root.Length>100)root=Path.GetDirectoryName(root)??throw new IOException("Short extractor workspace unavailable");
        var folder=Paths.Safe(root,"_local-centurion/"+Original[..16]);Directory.CreateDirectory(folder);
        var bin=Path.Combine(folder,"bin");Directory.CreateDirectory(bin);
        foreach(var (resource,name) in new[]{("Trilex.UEViewer.exe","umodel.exe"),("Trilex.SDL2.dll","SDL2.dll")})
        {
            using var data=typeof(LocalCenturionGenerator).Assembly.GetManifestResourceStream(resource)??throw new IOException("Reviewed local extractor missing");
            using var memory=new MemoryStream();data.CopyTo(memory);var bytes=memory.ToArray();var target=Paths.Safe(bin,name);
            if(!File.Exists(target))Paths.AtomicBytes(target,bytes);
            if(!Paths.Same(Paths.Hash(target),Convert.ToHexString(SHA256.HashData(bytes))))throw new IOException("Local extractor was modified");
        }
        var input=Path.Combine(folder,"input");Directory.CreateDirectory(input);var owned=Path.Combine(input,"wp_SP_ARPistol_SF.xxx");
        if(!File.Exists(owned))Paths.AtomicCopy(source,owned);
        if(!Paths.Same(Paths.Hash(owned),Original))throw new IOException("Local extractor input changed");
        var exports=Path.Combine(folder,"export");Directory.CreateDirectory(exports);
        foreach(var (name,kind,relative) in new[]{("ch_inview_alt_pistol_mesh","SkeletalMesh","SkeletalMesh3/ch_inview_alt_pistol_mesh.psk"),("Alt_pistol","AnimSet","AnimSet/Alt_pistol.psa"),("shell_rifle","StaticMesh","StaticMesh3/shell_rifle.pskx")})
        {
            var expected=Paths.Safe(exports,"wp_SP_ARPistol_SF/"+relative);
            if(File.Exists(expected))continue;
            var start=new ProcessStartInfo(Path.Combine(bin,"umodel.exe")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=folder};
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
