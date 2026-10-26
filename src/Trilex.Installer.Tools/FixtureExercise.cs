using Trilex.Installer.Core;

internal static class FixtureExercise
{
    public sealed record OwnedExerciseInput(string Module,string Stage);
    public static async Task RunOwned(string inputsPath,string originals,string folder)
    {
        var root=Path.GetFullPath(folder);var allowed=Path.GetFullPath("artifacts")+Path.DirectorySeparatorChar;
        if(!root.StartsWith(allowed,StringComparison.OrdinalIgnoreCase)||Directory.Exists(root))throw new IOException("Fresh local artifacts fixture required");
        Paths.AssertNoLinks(root);Paths.AssertNoLinks(originals);
        var inputs=Json.Read<List<OwnedExerciseInput>>(inputsPath);
        if(inputs.Count==0)throw new InvalidDataException("No fixture modules");
        Directory.CreateDirectory(Path.Combine(root,"Binaries"));var exe=Path.Combine(root,"Binaries/Singularity.exe");File.WriteAllText(exe,Marker);
        var modules=new List<ModuleManifest>();var archives=new Dictionary<string,string>();
        var before=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach(var input in inputs)
        {
            var original=Json.Read<ModuleManifest>(input.Module);
            foreach(var f in original.Files.Where(f=>f.Kind=="recipe"&&f.SystemSource is null))
            {
                var relative=f.SourcePath??f.Path;var source=Paths.Safe(originals,relative);var expected=f.SourceSha256??f.BeforeSha256;
                if(expected is null||!Paths.Same(Paths.Hash(source),expected))throw new InvalidDataException("Fixture original mismatch: "+relative);
                if(!before.ContainsKey(relative)){Paths.AtomicCopy(source,Paths.Safe(root,relative));before.Add(relative,Paths.Hash(source));}
            }
            var archive=Path.Combine(root,original.Id+".zip");
            var entries=original.Files.Where(f=>f.Kind=="copy").Select(f=>f.Entry).Concat(original.Files.SelectMany(f=>f.Recipe??[]).Where(p=>p.Entry is not null).Select(p=>p.Entry!)).Distinct(StringComparer.OrdinalIgnoreCase);
            long expanded=0;
            using(var zip=System.IO.Compression.ZipFile.Open(archive,System.IO.Compression.ZipArchiveMode.Create))foreach(var entry in entries)
            {
                var source=Paths.Safe(input.Stage,entry);expanded+=new FileInfo(source).Length;var target=zip.CreateEntry(entry);
                using var src=File.OpenRead(source);using var dst=target.Open();src.CopyTo(dst);
            }
            // This isolated exercise is deliberately NOT a production or legal approval.
            var module=original with{State="stable",RightsVerified=true,RightsEvidence="SYNTHETIC FIXTURE ONLY; NOT REDISTRIBUTION APPROVAL",ArchiveSha256=Paths.Hash(archive),DownloadBytes=new FileInfo(archive).Length,ExpandedBytes=expanded};
            modules.Add(module);archives.Add(module.Id,archive);
        }
        var plan=new InstallPlan("fixture-owned","v7",root,Paths.Hash(exe),new("pl","pl","en",modules.Select(m=>m.Id).ToList()),modules);
        var installer=new TransactionInstaller(Path.Combine(root,"fixture-state"),new Guard());
        await installer.InstallAsync(plan,archives,null,default);
        if(installer.Verify(root).Count!=0)throw new IOException("Combined installed files mismatch");
        await installer.InstallAsync(plan,archives,null,default);
        var damage=modules.SelectMany(m=>m.Files).First(f=>f.Kind is "copy" or "recipe"&&f.SystemSource is null);
        File.WriteAllText(Paths.Safe(root,damage.Path),"SIMULATED DAMAGE");
        if(installer.Verify(root).Count==0)throw new IOException("Combined damage undetected");
        await installer.InstallAsync(plan,archives,null,default,repair:true);
        if(installer.Verify(root).Count!=0)throw new IOException("Combined repair mismatch");
        installer.Uninstall(root,default);
        foreach(var (relative,hash) in before)if(!Paths.Same(Paths.Hash(Paths.Safe(root,relative)),hash))throw new IOException("Original restoration mismatch: "+relative);
        foreach(var file in modules.SelectMany(m=>m.Files))if(!before.ContainsKey(file.Path)&&File.Exists(Paths.Safe(root,file.Path)))throw new IOException("Uninstall left managed file: "+file.Path);
        Json.Write(Path.Combine(root,"exercise-report.json"),new{status="PASS",modules=modules.Select(m=>m.Id),targets=modules.Sum(m=>m.Files.Count),originalsRestored=before.Count,steps=new[]{"install","exact verification","reinstall","damage detection","repair","uninstall","exact original restoration"},gameLaunched=false,dllLoaded=false,steamWrites=false,redistributionApproved=false,physicalAcceptance="NOT RUN"});
        Console.WriteLine("PASS combined owned-source transaction lifecycle; originals restored: "+before.Count);
    }
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
