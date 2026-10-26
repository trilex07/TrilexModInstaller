using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Trilex.Installer.Core;

try
{
    if(args is ["keygen",var privatePath,var publicPath])
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
    else throw new ArgumentException("Commands: prepare-audio-tools | keygen PRIVATE.pem PUBLIC.pem | sign CATALOG.json PRIVATE.pem OUTPUT.json | verify SIGNED.json PUBLIC.pem | pack MODULE.json STAGE OUTPUT.zip | exercise MODULE.json ARCHIVE.zip artifacts/FRESH_FIXTURE | verify-recipes MODULE.json STAGE ORIGINALS artifacts/FRESH_OUTPUT");
}
catch(Exception e){Console.Error.WriteLine(e.GetType().Name+": "+e.Message);Environment.ExitCode=1;}

