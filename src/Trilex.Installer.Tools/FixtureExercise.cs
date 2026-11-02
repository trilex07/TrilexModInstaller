using Trilex.Installer.Core;

internal static class FixtureExercise
{
    public static async Task RunBundle(Catalog catalog,string bundle,string originals,string folder,Catalog? previousCatalog=null,string? previousBundle=null)
    {
        var root=Path.GetFullPath(folder);var allowed=Path.GetFullPath("artifacts")+Path.DirectorySeparatorChar;
        if(!root.StartsWith(allowed,StringComparison.OrdinalIgnoreCase)||Directory.Exists(root))throw new IOException("Fresh artifacts fixture required");
        var game=catalog.Games.Single(g=>g.Id=="singularity");var archives=new Dictionary<string,string>();
        foreach(var module in game.Modules)archives.Add(module.Id,BundleSource.ArchiveAt(bundle,module)??throw new FileNotFoundException("Missing bundled archive "+module.Id));
        Directory.CreateDirectory(Path.Combine(root,"Binaries"));var exe=Path.Combine(root,"Binaries/Singularity.exe");File.WriteAllText(exe,Marker);
        var before=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach(var file in game.Modules.SelectMany(m=>m.Files).Where(f=>f.Kind=="recipe"&&f.SystemSource is null))
        {
            var relative=file.SourcePath??file.Path;var source=Paths.Safe(originals,relative);
            if(!Paths.Same(Paths.Hash(source),file.SourceSha256??file.BeforeSha256))throw new InvalidDataException("Original mismatch "+relative);
            if(before.TryAdd(relative,Paths.Hash(source)))Paths.AtomicCopy(source,Paths.Safe(root,relative));
        }
        // Only this marker-only fixture substitutes EXE identity. Signed modules, reviews,
        // archives and final output hashes remain unchanged through the actual consumer.
        var fixtureGame=game with{ExeSha256=[Paths.Hash(exe)]};
        var choices=new InstallChoices("pl","pl","pl",game.Modules.Select(m=>m.Id).ToList());
        var plan=PlanBuilder.Build(fixtureGame,new(root,"Frozen owned-source fixture",true,"Not a playable game",Paths.Hash(exe)),choices);
        var installer=new TransactionInstaller(Path.Combine(root,"fixture-state"),new Guard());
        if(previousCatalog is not null)
        {
            var oldGame=previousCatalog.Games.Single(g=>g.Id=="singularity") with{ExeSha256=[Paths.Hash(exe)]};
            var oldChoices=choices with{SelectedModules=oldGame.Modules.Select(m=>m.Id).ToList()};
            var oldPlan=PlanBuilder.Build(oldGame,new(root,"Frozen upgrade fixture",true,"Not a playable game",Paths.Hash(exe)),oldChoices);
            var oldArchives=oldGame.Modules.ToDictionary(m=>m.Id,m=>BundleSource.ArchiveAt(previousBundle!,m)??throw new FileNotFoundException("Previous archive absent: "+m.Id));
            await installer.InstallAsync(oldPlan,oldArchives,null,default);
            if(installer.Verify(root).Count!=0)throw new IOException("Predecessor fixture failed exact verification");
            Console.WriteLine("PASS predecessor signed bundle installation: "+previousCatalog.Version);
        }
        await installer.InstallAsync(plan,archives,null,default);
        if(installer.Verify(root).Count!=0)throw new IOException("Bundle installation failed exact verification");
        Console.WriteLine("PASS complete bundle installation and exact output hashes");
        await installer.InstallAsync(plan,archives,null,default);
        var damage=plan.Modules.SelectMany(m=>m.Files).First(f=>f.Kind=="copy");File.WriteAllText(Paths.Safe(root,damage.Path),"SIMULATED DAMAGE");
        if(installer.Verify(root).Count==0)throw new IOException("Bundle damage not detected");
        await installer.RepairAsync(plan,archives,null,default);
        if(installer.Verify(root).Count!=0)throw new IOException("Bundle repair failed");
        var installed=plan.Modules.SelectMany(m=>m.Files).Where(f=>f.Kind!="ini").Select(f=>new{f.Path,sha256=Paths.Hash(Paths.Safe(root,f.Path)),expected=f.Sha256}).ToList();
        installer.Uninstall(root,default);
        foreach(var pair in before)if(!Paths.Same(Paths.Hash(Paths.Safe(root,pair.Key)),pair.Value))throw new IOException("Bundle original restoration failed "+pair.Key);
        foreach(var file in plan.Modules.SelectMany(m=>m.Files))if(!before.ContainsKey(file.Path)&&File.Exists(Paths.Safe(root,file.Path)))throw new IOException("Bundle uninstall left "+file.Path);
        Json.Write(Path.Combine(root,"exercise-report.json"),new{status="PASS",catalog=catalog.Version,predecessor=previousCatalog?.Version,modules=plan.Modules.Count,targets=plan.Modules.SelectMany(m=>m.Files).Select(f=>f.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count(),fileDeclarations=plan.Modules.Sum(m=>m.Files.Count),originalsRestored=before.Count,signedModulesUnchanged=true,rightsFlagsUnchanged=true,installed,steps=new[]{"signed catalog","all bundled archive hashes","actual PL full preset planning","install or predecessor upgrade","verify","reinstall","detect damage","repair","uninstall","byte-exact originals"},steamAccess="NONE",physicalAcceptance="NOT RUN"});
        Console.WriteLine("PASS signed complete bundle lifecycle; originals restored: "+before.Count);
    }
    // Continue a marker-only upgrade fixture after disk-space preflight refused
    // the repair. Never run on a real game or manufacture prior PASS.
    public static async Task FinishUpgradedBundle(Catalog catalog,string bundle,string originals,string folder)
    {
        var root=Path.GetFullPath(folder);var allowed=Path.GetFullPath("artifacts")+Path.DirectorySeparatorChar;
        if(catalog.Version!="1.0.0-rc.7"||Path.GetFileName(root)!="rc7-upgrade-lifecycle"||
           !root.StartsWith(allowed,StringComparison.OrdinalIgnoreCase)||!Directory.Exists(root)||
           File.ReadAllText(Path.Combine(root,"Binaries/Singularity.exe"))!=Marker||
           File.Exists(Path.Combine(root,"exercise-report.json")))throw new IOException("Existing marker-only fixture required");
        var game=catalog.Games.Single(g=>g.Id=="singularity");
        var archives=game.Modules.ToDictionary(m=>m.Id,m=>BundleSource.ArchiveAt(bundle,m)??throw new FileNotFoundException("Bundle archive missing: "+m.Id));
        var before=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach(var f in game.Modules.SelectMany(m=>m.Files).Where(f=>f.Kind=="recipe"&&f.SystemSource is null))
        {
            var relative=f.SourcePath??f.Path;var expected=f.SourceSha256??f.BeforeSha256;
            if(expected is null||!Paths.Same(Paths.Hash(Paths.Safe(originals,relative)),expected))throw new InvalidDataException("Owned original mismatch: "+relative);
            before[relative]=expected;
        }
        var installer=new TransactionInstaller(Path.Combine(root,"fixture-state"),new Guard());
        var damage=game.Modules.SelectMany(m=>m.Files).First(f=>f.Kind=="copy");
        var existingDamage=installer.Verify(root);
        if(existingDamage.Count!=1||!Paths.Same(existingDamage[0],damage.Path)||
           File.ReadAllText(Paths.Safe(root,damage.Path))!="SIMULATED DAMAGE")
            throw new IOException("Only the intentional fixture damage may remain at resume");
        foreach(var f in game.Modules.SelectMany(m=>m.Files).Where(f=>f.Kind!="ini"&&f.SystemSource is null))
            if(!Paths.Same(f.Path,damage.Path)&&!Paths.Same(Paths.Hash(Paths.Safe(root,f.Path)),f.Sha256))throw new IOException("Upgrade is not requested signed catalog: "+f.Path);
        var exeHash=Paths.Hash(Path.Combine(root,"Binaries/Singularity.exe"));
        var fixtureGame=game with{ExeSha256=[exeHash]};
        var choices=new InstallChoices("pl","pl","pl",game.Modules.Select(m=>m.Id).ToList());
        var plan=PlanBuilder.Build(fixtureGame,new(root,"Resumed owned-source upgrade fixture",true,"Not a playable game",exeHash),choices);
        Console.WriteLine("PASS resumed signed upgrade: only the intentional damage differs from the current catalog");
        if(installer.Verify(root).Count==0)throw new IOException("Damage undetected");
        await installer.RepairAsync(plan,archives,null,default);
        if(installer.Verify(root).Count!=0)throw new IOException("Resumed upgrade repair failed");
        var installed=plan.Modules.SelectMany(m=>m.Files).Where(f=>f.Kind!="ini").Select(f=>new{f.Path,sha256=Paths.Hash(Paths.Safe(root,f.Path)),expected=f.Sha256}).ToList();
        installer.Uninstall(root,default);
        foreach(var p in before)if(!Paths.Same(Paths.Hash(Paths.Safe(root,p.Key)),p.Value))throw new IOException("Original not restored: "+p.Key);
        foreach(var f in plan.Modules.SelectMany(m=>m.Files))if(!before.ContainsKey(f.Path)&&File.Exists(Paths.Safe(root,f.Path)))throw new IOException("Uninstall left file: "+f.Path);
        Json.Write(Path.Combine(root,"exercise-report.json"),new{status="PASS",catalog=catalog.Version,predecessor="1.0.0-rc.6",modules=plan.Modules.Count,targets=plan.Modules.SelectMany(m=>m.Files).Select(f=>f.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count(),originalsRestored=before.Count,installed,steps=new[]{"RC6 signed install","RC7 signed upgrade","reinstall before intentional damage","exact current catalog hashes except deliberate damage","detect damage","repair","uninstall","byte-exact originals"},recovery="Repair preflight refused insufficient disk; only the deliberate fixture damage remained; repaired/uninstalled after freeing finished fixture copies",steamAccess="NONE",physicalAcceptance="NOT RUN"});
        Console.WriteLine("PASS resumed upgrade repair/uninstall; originals restored: "+before.Count);
    }

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
