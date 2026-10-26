using Trilex.Installer.Core;

internal static class FixtureExercise
{
    private const string Marker="TRILEX PACKAGE FIXTURE ONLY - NOT AN EXECUTABLE";
    private sealed class Guard : IProcessGuard
    {
        public void RequireIdle(string root)
        {
            if(File.ReadAllText(Path.Combine(root,"Binaries/Singularity.exe"))!=Marker)throw new IOException("Not a synthetic fixture");
        }
    }
    public static async Task Run(string modulePath,string archive,string folder)
    {
        var root=Path.GetFullPath(folder);var parent=Path.GetFullPath("artifacts")+Path.DirectorySeparatorChar;
        if(!root.StartsWith(parent,StringComparison.OrdinalIgnoreCase) || Directory.Exists(root))throw new IOException("Fresh local artifacts fixture directory required");
        Paths.AssertNoLinks(root);Directory.CreateDirectory(Path.Combine(root,"Binaries"));Directory.CreateDirectory(Path.Combine(root,"RvGame/CookedPC"));
        var exe=Path.Combine(root,"Binaries/Singularity.exe");File.WriteAllText(exe,Marker);
        // Preview promotion exists ONLY inside this synthetic CLI exercise. Production plans remain gated.
        var module=Json.Read<ModuleManifest>(modulePath) with{State="stable"};
        var plan=new InstallPlan("fixture",module.Version,root,Paths.Hash(exe),new("en","en","en",[module.Id]),[module]);
        var installer=new TransactionInstaller(Path.Combine(root,"fixture-state"),new Guard());
        var archives=new Dictionary<string,string>{{module.Id,Path.GetFullPath(archive)}};
        await installer.InstallAsync(plan,archives,null,default);
        if(installer.Verify(root).Count!=0)throw new IOException("Initial installed content verification failed");
        await installer.InstallAsync(plan,archives,null,default);
        var copied=module.Files.First(x=>x.Kind=="copy");File.WriteAllText(Paths.Safe(root,copied.Path),"SIMULATED DAMAGE");
        if(installer.Verify(root).Count==0)throw new IOException("Damage was not detected");
        await installer.InstallAsync(plan,archives,null,default,repair:true);
        if(installer.Verify(root).Count!=0)throw new IOException("Repair verification failed");
        installer.Uninstall(root,default);
        if(module.Files.Any(x=>File.Exists(Paths.Safe(root,x.Path))))throw new IOException("Uninstall left owned payload behind");
        if(File.ReadAllText(exe)!=Marker)throw new IOException("Fixture EXE changed");
        Json.Write(Path.Combine(root,"exercise-report.json"),new{status="PASS",archiveSha256=Paths.Hash(archive),module=module.Id,steps=new[]{"install","verify exact bytes","idempotent reinstall","detect damage","repair","uninstall"},gameLaunched=false,dllLoaded=false,steamAccess="NONE",scope="Real package bytes through transaction engine in synthetic game directory; not playable or physical validation"});
        Console.WriteLine("PASS real package fixture install/verify/reinstall/repair/uninstall; no game or DLL executed");
    }
}
