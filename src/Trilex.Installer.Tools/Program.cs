using Trilex.Installer.Desktop;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Trilex.Installer.Core;

try
{
    if(args is ["scan-darkness-renderer",var executable])
        Console.WriteLine(JsonSerializer.Serialize(DarknessRendererScan.Scan(executable),Json.Options));
    else if(args is ["exercise-wonderlands",var exerciseCatalog,var exerciseKey,var exerciseBundle,var exerciseDirectory])
    {
        var exercise=Path.GetFullPath(exerciseDirectory);if(Directory.Exists(exercise))throw new IOException("Use a fresh fixture directory");
        Directory.CreateDirectory(exercise);var gameRoot=Path.Combine(exercise,"game");
        Directory.CreateDirectory(Paths.Safe(gameRoot,"OakGame/Content"));
        var exe=Paths.Safe(gameRoot,"OakGame/Binaries/Win64/Wonderlands.exe");Directory.CreateDirectory(Path.GetDirectoryName(exe)!);
        var bytes=new byte[256];using(var memory=new MemoryStream(bytes))using(var writer=new BinaryWriter(memory)){writer.Write((ushort)0x5a4d);memory.Position=60;writer.Write(128);memory.Position=128;writer.Write(0x4550);writer.Write((ushort)0x8664);memory.Position=152;writer.Write((ushort)0x20b);}File.WriteAllBytes(exe,bytes);
        var iniPath=Paths.Safe(gameRoot,"OakGame/Binaries/Win64/WonderlandsVR.ini");var original="[User]\r\nKeep=fixture\r\n";File.WriteAllText(iniPath,original);
        using var http=new HttpClient();var game=new CatalogService(http,File.ReadAllText(exerciseKey)).Verify(File.ReadAllBytes(exerciseCatalog)).Games.Single(g=>g.Id=="wonderlands");
        var location=new WonderlandsGameAdapter().Validate(gameRoot,game);
        var plan=PlanBuilder.Build(game,location,new("en","en","en",[]),new(gameRoot,location.ExeHash!));
        var archives=plan.Modules.ToDictionary(m=>m.Id,m=>BundleSource.ArchiveAt(exerciseBundle,m,game.Id)??throw new IOException("Missing bundled package"));
        var installer=new TransactionInstaller(Path.Combine(exercise,"state"),new WindowsProcessGuard());
        var receipt=await installer.InstallAsync(plan,archives,null,default);if(installer.Verify(gameRoot).Count>0)throw new IOException("Installed fixture differs");
        var export=ManualExporter.Export(plan,archives,Path.Combine(exercise,"manual"),installer,null,default);
        var manual=Json.Read<ManualExportReceipt>(Path.Combine(export,"manual-manifest.json"));
        foreach(var file in manual.Files)if(!Paths.Same(Paths.Hash(Paths.Safe(export,"files/"+file.Path)),Paths.Hash(Paths.Safe(gameRoot,file.Path))))throw new IOException("Manual/managed divergence");
        installer.Uninstall(gameRoot,default);
        if(File.ReadAllText(iniPath)!=original||File.Exists(Paths.Safe(gameRoot,"OakGame/Binaries/Win64/xinput1_3.dll")))throw new IOException("Original restore failed");
        Json.Write(Path.Combine(exercise,"verification.json"),new{status="PASS",files=receipt.Files.Count,manualFiles=manual.Files.Count,restore="byte-exact",gameLaunched=false,source="synthetic PE header, never executed",headset="NOT RUN"});
        Console.WriteLine("PASS actual signed Wonderlands package: managed install, manual export equality, verify and exact restore");
    }
    else if(args is ["keygen",var privatePath,var publicPath])
    {
        if(File.Exists(privatePath)||File.Exists(publicPath))throw new IOException("Refusing key overwrite");
        using var key=ECDsa.Create(ECCurve.NamedCurves.nistP256);
        File.WriteAllText(privatePath,key.ExportECPrivateKeyPem());File.WriteAllText(publicPath,key.ExportSubjectPublicKeyInfoPem());
        Console.WriteLine("Created P256 publisher key; keep private key offline and outside source control.");
    }
    else if(args is ["sign",var catalogPath,var signKey,var signedOutput])
    {
        var payload=File.ReadAllBytes(catalogPath);var catalog=Json.Read<Catalog>(catalogPath);
        foreach(var game in catalog.Games)PlanBuilder.ValidateManifest(game);
        using var key=ECDsa.Create();key.ImportFromPem(File.ReadAllText(signKey));
        if(key.KeySize!=256)throw new InvalidDataException("P256 key required");
        var envelope=new SignedEnvelope("ECDSA-P256-SHA256-P1363",Convert.ToBase64String(payload),Convert.ToBase64String(key.SignData(payload,HashAlgorithmName.SHA256,DSASignatureFormat.IeeeP1363FixedFieldConcatenation)));
        var bytes=JsonSerializer.SerializeToUtf8Bytes(envelope,Json.Options);
        using var http=new HttpClient();new CatalogService(http,key.ExportSubjectPublicKeyInfoPem()).Verify(bytes);
        Paths.AtomicBytes(signedOutput,bytes);Console.WriteLine(Paths.Hash(signedOutput));
    }
    else if(args is ["verify",var signedPath,var verifyKey])
    {
        using var http=new HttpClient();var catalog=new CatalogService(http,File.ReadAllText(verifyKey)).Verify(File.ReadAllBytes(signedPath));Console.WriteLine("Verified catalog "+catalog.Version);
    }
    else if(args is ["verify-downloads",var signedCatalog,var publisherKey,var gameId,var downloadCache,..var moduleIds] && moduleIds.Length>0)
    {
        using var http=CatalogService.CreateHttpClient();
        var catalog=new CatalogService(http,File.ReadAllText(publisherKey)).Verify(File.ReadAllBytes(signedCatalog));
        var game=catalog.Games.Single(g=>g.Id==gameId);
        var downloader=new PackageDownloader(http,downloadCache);
        var rows=new List<object>();
        foreach(var id in moduleIds)
        {
            var module=game.Modules.Single(m=>m.Id==id&&m.State=="stable");
            var archive=await downloader.GetAsync(module,false,null,default);
            rows.Add(new{id,sha256=Paths.Hash(archive),bytes=new FileInfo(archive).Length});
            Console.WriteLine("PASS verified download: "+id);
        }
        Json.Write(Path.Combine(downloadCache,"download-verification.json"),new{status="PASS",gameId,rows,gameAccess="NONE"});
    }
    else if(args is ["require-idle",var idleRoot])
    {
        new WindowsProcessGuard().RequireIdle(idleRoot);Console.WriteLine("PASS game processes idle");
    }
    else if(args is ["install-managed",var installCatalog,var installKey,var installGame,var installRoot,var installBundle,var installState,var installLanguage,..var localTextureSource] && localTextureSource.Length<=1)
    {
        using var http=CatalogService.CreateHttpClient();
        var catalog=new CatalogService(http,File.ReadAllText(installKey)).Verify(File.ReadAllBytes(installCatalog));
        var game=catalog.Games.Single(g=>g.Id==installGame);
        IGameAdapter adapter=installGame=="singularity"?new SingularityGameAdapter():installGame=="darkness2"?new Darkness2GameAdapter():installGame=="wonderlands"?new WonderlandsGameAdapter():throw new InvalidDataException("Unknown game");
        var location=adapter.Validate(installRoot,game);
        var selected=game.Modules.Where(m=>m.State=="stable"&&m.Id is not ("polish-text" or "polish-dubbing")).Select(m=>m.Id).ToList();
        var plan=PlanBuilder.Build(game,location,new(installLanguage,installGame=="singularity"?installLanguage:"en",installLanguage,selected));
        var guard=new WindowsProcessGuard();guard.RequireIdle(plan.Root);
        if(LocalAudioTools.Required(plan.Modules))await LocalAudioTools.EnsureAsync(http,false,null,default);
        if(LocalExtractorTools.Required(plan.Modules))await LocalExtractorTools.EnsureAsync(http,false,null,default,installBundle);
        var downloader=new PackageDownloader(http,Path.Combine(Path.GetDirectoryName(Path.GetFullPath(installState))!,"cache"));
        var archives=new Dictionary<string,string>();
        foreach(var module in plan.Modules)archives[module.Id]=BundleSource.ArchiveAt(installBundle,module,game.Id)??await downloader.GetAsync(module,false,null,default);
        var installer=new TransactionInstaller(installState,guard);
        if(localTextureSource.Length==1)
        {
            if(installGame!="darkness2")throw new InvalidDataException("Local texture conversion is Darkness II only");
            var work=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(installState))!,"texture-work",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(work);
            var prepared=await DarknessTextureInstall.PrepareAsync(plan.Root,installer,work,localTextureSource[0],default);
            plan=plan with {Modules=[..plan.Modules,prepared.Module],Choices=plan.Choices with {SelectedModules=[..plan.Choices.SelectedModules,"hd-textures"]}};
            archives[prepared.Module.Id]=prepared.Archive;
        }
        // Adopt a previously deployed accepted payload only when its bytes match
        // this signed target. Never overwrite an arbitrary external edit.
        var previous=installer.Receipt(plan.Root);
        var repairAccepted=false;
        if(previous is not null)foreach(var old in previous.Files.Where(f=>f.Kind!="ini"))
        {
            var live=Paths.HashOrNull(Paths.Safe(plan.Root,old.Path));
            if(Paths.Same(live,old.InstalledHash))continue;
            var target=plan.Modules.SelectMany(m=>m.Files).FirstOrDefault(f=>Paths.Same(f.Path,old.Path));
            if(target is null||!Paths.Same(live,target.Sha256))throw new IOException("External edit does not match signed target: "+old.Path);
            repairAccepted=true;
        }
        await installer.InstallAsync(plan,archives,null,default,repair:repairAccepted);
        var failures=installer.Verify(plan.Root);
        if(failures.Count!=0)throw new IOException("Installed verification failed: "+string.Join(",",failures));
        Console.WriteLine("PASS managed install and all owned file hashes: "+game.Id);
    }
    else if(args is ["verify-darkness-textures",var textureDestination])
    {
        using var http=CatalogService.CreateHttpClient();
        await new DarknessTextureDownload(http).SaveAsync(textureDestination,null,default);
        Console.WriteLine("PASS bounded PNG source ZIP downloaded; SHA256="+Paths.Hash(textureDestination));
    }
    else if(args is ["exercise-darkness",var signedInput,var publicInput,var bundleInput,var originalInput,var fixtureOutput])
    {
        var output=Path.GetFullPath(fixtureOutput);
        if(!output.StartsWith(Path.GetFullPath("artifacts")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||Directory.Exists(output))throw new ArgumentException("Fresh artifacts fixture required");
        using var http=CatalogService.CreateHttpClient();
        var catalog=new CatalogService(http,File.ReadAllText(publicInput)).Verify(File.ReadAllBytes(signedInput));
        var game=catalog.Games.Single(g=>g.Id=="darkness2");
        var owned=Path.Combine(output,"game");Directory.CreateDirectory(owned);
        var paths=game.Modules.SelectMany(m=>m.Files).Select(f=>f.SourcePath??f.Path).Append(game.Executable).Distinct(StringComparer.OrdinalIgnoreCase);
        foreach(var relative in paths)
        {
            var source=Paths.Safe(originalInput,relative);if(!File.Exists(source))continue;
            var target=Paths.Safe(owned,relative);Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(source,target);
        }
        var originals=Directory.GetFiles(owned,"*",SearchOption.AllDirectories).ToDictionary(p=>p,Paths.Hash);
        var location=new Darkness2GameAdapter().Validate(owned,game);
        var installer=new TransactionInstaller(Path.Combine(output,"state"),new FixtureGuard(owned));
        foreach(var language in new[]{"en","pl","en"})
        {
            var plan=PlanBuilder.Build(game,location,new(language,"en","en",["darkness-assets","darkness-menu"]));
            var archives=plan.Modules.ToDictionary(m=>m.Id,m=>BundleSource.ArchiveAt(bundleInput,m,game.Id)??throw new IOException("Missing bundled Darkness package"));
            await installer.InstallAsync(plan,archives,null,default);
            if(installer.Verify(owned).Count!=0)throw new IOException("Installed verification failed");
            var fonts=game.Modules.Single(m=>m.Id=="polish-text").Files.Where(f=>Paths.DarknessFontTarget(f.Path)).ToArray();
            if(fonts.Length!=4 || fonts.Any(f=>
                !Paths.Same(Paths.Hash(Paths.Safe(owned,f.Path)),language=="pl"?f.Sha256:originals[Paths.Safe(owned,f.Path)])))
                throw new IOException("Polish font selection/restoration failed: "+language);
            Console.WriteLine("PASS install/update/verify and font ownership: "+language);
        }
        installer.Uninstall(owned,default);
        if(Directory.GetFiles(owned,"*",SearchOption.AllDirectories).Length!=originals.Count||originals.Any(p=>Paths.Hash(p.Key)!=p.Value))throw new IOException("Original restoration failed");
        Json.Write(Path.Combine(output,"lifecycle-report.json"),new{status="PASS",languages=new[]{"en","pl","en"},restoredOriginals=originals.Count,gameAccess="NONE; copied fixture only"});
        Console.WriteLine("PASS byte-exact uninstall restoration");
    }
    else if(args is ["prepare-extractor-tools",var extractorBundle])
    {
        // Release fixture: fail if the actual portable directory cannot prepare
        // its pinned exporter with all network access disabled.
        using var http=new HttpClient();
        await LocalExtractorTools.EnsureAsync(http,true,null,default,extractorBundle);
        Console.WriteLine("Verified bundled exporter offline: "+LocalExtractorTools.Resolve());
    }
    else if(args is ["prepare-extractor-tools"])
    {
        using var http=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false});
        await LocalExtractorTools.EnsureAsync(http,false,null,default);
        Console.WriteLine("Verified optional exporter: "+LocalExtractorTools.Resolve());
    }
    else if(args is ["prepare-audio-tools"])
    {
        using var http=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false});
        await LocalAudioTools.EnsureAsync(http,false,null,default);
        Console.WriteLine("Verified optional codec: "+LocalAudioTools.Resolve());
    }
    else if(args is ["pack",var modulePath,var stage,var output])
    {
        var module=Json.Read<ModuleManifest>(modulePath);
        if(!PlanBuilder.HasDistributionReview(module))throw new InvalidDataException("Explicit file provenance or publisher fan-mod review required");
        if(File.Exists(output))throw new IOException("Refusing package overwrite");
        var entries=module.Files.Where(x=>x.Kind=="copy").Select(x=>x.Entry).Concat(module.Files.SelectMany(x=>x.Recipe??[]).Where(x=>x.Source is "payload" or "xor-original" or "instructions" or "image" or "knife" or "audio").Select(x=>x.Entry??"")).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToList();
        foreach(var f in module.Files){Paths.AllowedTarget(f.Path);if(f.Kind=="copy"&&f.Path.EndsWith(".xxx",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Whole commercial packages forbidden");}
        long expanded=0;Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        using(var zip=ZipFile.Open(output,ZipArchiveMode.Create))foreach(var entry in entries){var path=Paths.Safe(stage,entry);expanded+=new FileInfo(path).Length;var item=zip.CreateEntry(entry,CompressionLevel.Optimal);item.LastWriteTime=new DateTimeOffset(2026,1,1,0,0,0,TimeSpan.Zero);using var src=File.OpenRead(path);using var dst=item.Open();src.CopyTo(dst);}
        module=module with{ArchiveSha256=Paths.Hash(output),DownloadBytes=new FileInfo(output).Length,ExpandedBytes=expanded};
        var temp=output+".audit-"+Guid.NewGuid().ToString("N");
        try{PackageExtractor.Extract(output,temp,module);}finally{if(Directory.Exists(temp))Directory.Delete(temp,true);}
        Json.Write(output+".module.json",module);Console.WriteLine(module.ArchiveSha256);
    }
    else if(args is ["exercise-bundle",var bundle,var bundleKey,var bundleOriginals,var bundleFixture])
    {
        using var http=new HttpClient();var service=new CatalogService(http,File.ReadAllText(bundleKey));
        var catalog=BundleSource.CatalogAt(bundle,service)??throw new InvalidDataException("Signed bundle catalog absent");
        await FixtureExercise.RunBundle(catalog,bundle,bundleOriginals,bundleFixture);
    }
    else if(args is ["exercise-bundle-upgrade",var bundleNew,var upgradeKey,var upgradeOriginals,var upgradeFixture,var bundleOld])
    {
        using var http=new HttpClient();var service=new CatalogService(http,File.ReadAllText(upgradeKey));
        var current=BundleSource.CatalogAt(bundleNew,service)??throw new InvalidDataException("Current signed catalog absent");
        var previous=BundleSource.CatalogAt(bundleOld,service)??throw new InvalidDataException("Previous signed catalog absent");
        await FixtureExercise.RunBundle(current,bundleNew,upgradeOriginals,upgradeFixture,previous,bundleOld);
    }
    else if(args is ["finish-bundle-upgrade-fixture",var resumeBundle,var resumeKey,var resumeOriginals,var resumeRoot])
    {
        using var http=new HttpClient();var service=new CatalogService(http,File.ReadAllText(resumeKey));
        var catalog=BundleSource.CatalogAt(resumeBundle,service)??throw new InvalidDataException("Signed resume catalog absent");
        await FixtureExercise.FinishUpgradedBundle(catalog,resumeBundle,resumeOriginals,resumeRoot);
    }
    else if(args is ["exercise-owned",var ownedInputs,var ownedSources,var ownedRoot])
        await FixtureExercise.RunOwned(ownedInputs,ownedSources,ownedRoot);
    else if(args is ["exercise",var exerciseModule,var exerciseArchive,var exerciseRoot])
        await FixtureExercise.Run(exerciseModule,exerciseArchive,exerciseRoot);
    else if(args is ["verify-recipes",var recipeModule,var recipeStage,var originals,var recipeOutput])
    {
        var reconstructedRoot=Path.GetFullPath(recipeOutput);var allowed=Path.GetFullPath("artifacts")+Path.DirectorySeparatorChar;
        if(!reconstructedRoot.StartsWith(allowed,StringComparison.OrdinalIgnoreCase)||Directory.Exists(reconstructedRoot))throw new IOException("Fresh artifacts output required");
        var module=Json.Read<ModuleManifest>(recipeModule);Directory.CreateDirectory(reconstructedRoot);
        foreach(var file in module.Files)
        {
            if(file.Kind!="recipe"||file.SystemSource is not null)throw new InvalidDataException("Only owned-file offline reconstruction in this command");
            LocalSources.Validate(file,reconstructedRoot);PackageExtractor.ReconstructTo(Paths.Safe(originals,file.SourcePath??file.Path),recipeStage,file,Paths.Safe(reconstructedRoot,file.Path));
        }
        Json.Write(Path.Combine(reconstructedRoot,"recipe-report.json"),new{status="PASS",files=module.Files.Count,scope="Exact preimage and final SHA256; read-only originals; not a public license or game acceptance",steamAccess="NONE"});Console.WriteLine("PASS reconstructed exact files: "+module.Files.Count);
    }
    else throw new ArgumentException("Commands: require-idle ROOT | install-managed SIGNED PUBLIC GAME ROOT BUNDLE STATE LANGUAGE [DARKNESS_PNG_ZIP] | exercise-darkness SIGNED PUBLIC BUNDLE ORIGINALS artifacts/FRESH_FIXTURE | verify-darkness-textures ZIP | prepare-audio-tools | keygen PRIVATE.pem PUBLIC.pem | sign CATALOG.json PRIVATE.pem OUTPUT.json | verify SIGNED.json PUBLIC.pem | pack MODULE.json STAGE OUTPUT.zip | exercise MODULE.json ARCHIVE.zip artifacts/FRESH_FIXTURE | verify-recipes MODULE.json STAGE ORIGINALS artifacts/FRESH_OUTPUT");
}
catch(Exception e){Console.Error.WriteLine(e.GetType().Name+": "+e.Message);Environment.ExitCode=1;}

sealed class FixtureGuard(string root):IProcessGuard
{
    public void RequireIdle(string candidate){if(!Paths.Same(Path.GetFullPath(candidate),root))throw new IOException("Fixture boundary violation");}
}

