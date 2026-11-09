using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Trilex.Installer.Core;

var testRoot=Path.GetFullPath(Environment.GetEnvironmentVariable("TRILEX_TEST_ROOT")??Path.Combine(AppContext.BaseDirectory,"fixtures"));
Directory.CreateDirectory(testRoot);
if(args is ["--crash",var childRoot,var childState,var childArchive,var moduleJson])
{
    var module=Json.Read<ModuleManifest>(moduleJson);var f=new TestFixture(childRoot,childState);
    await f.Installer.InstallAsync(f.Plan(module),new(){{module.Id,childArchive}},null,default,_=>Environment.Exit(86));return;
}
var results=new List<object>();int failed=0;
async Task Test(string name,Func<Task> body)
{
    try{await body();results.Add(new{name,status="PASS"});Console.WriteLine("PASS "+name);}
    catch(Exception e){failed++;results.Add(new{name,status="FAIL",error=e.ToString()});Console.WriteLine("FAIL "+name+": "+e);}
}
Task Sync(Action a){a();return Task.CompletedTask;}
void Assert(bool condition,string message="Assertion failed"){if(!condition)throw new Exception(message);}
async Task Reject(Func<Task> action){try{await action();}catch(Exception e)when(e is IOException or InvalidDataException or InvalidOperationException or CryptographicException or UnauthorizedAccessException){return;}throw new Exception("Expected refusal");}
TestFixture Fixture()=>new(Path.Combine(testRoot,Guid.NewGuid().ToString("N")),Path.Combine(testRoot,"state-"+Guid.NewGuid().ToString("N")));
ModuleManifest Package(TestFixture f,string suffix="v1",string content="MOD-AUTHORED")
{
    var archive=Path.Combine(f.Root,suffix+".zip");using(var zip=ZipFile.Open(archive,ZipArchiveMode.Create)){var entry=zip.CreateEntry("data/mod.bin");using var writer=new StreamWriter(entry.Open(),new UTF8Encoding(false));writer.Write(content);}
    return new("vr-core",new(){{"en","VR Core"},{"pl","VR Core"}},suffix,"stable",true,[],new FileInfo(archive).Length,"https://github.com/trilex07/TrilexModInstaller/releases/download/test/"+suffix+".zip",Paths.Hash(archive),4096,true,"Synthetic newly authored fixture",[new("Binaries/trilex-test.dll","data/mod.bin",Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))),"copy","mod-authored"),new("Binaries/SingularityVR.ini","","","ini","mod-authored",IniValues:new(){{"Input/Test","1"}})],"");
}
Dictionary<string,string> Archives(TestFixture f,ModuleManifest m)=>new(){{m.Id,Path.Combine(f.Root,m.Version+".zip")}};
ModuleManifest MismatchPackage(TestFixture f,bool second=false)
{
    var module=Package(f);var recipes=new List<PackageFile>();
    foreach(var path in second?new[]{"RvGame/CookedPC/test.xxx","RvGame/CookedPC/test2.xxx"}:new[]{"RvGame/CookedPC/test.xxx"})
    {
        var target=Paths.Safe(f.Root,path);File.WriteAllText(target,"ORIGINAL");
        recipes.Add(new(path,"",new string('0',64),"recipe","local-transform",Paths.Hash(target),Recipe:[new("payload",0,3,"data/mod.bin")]));
    }
    return module with{Files=[..module.Files,..recipes]};
}
byte[] Envelope(Catalog catalog,ECDsa key)
{
    var payload=JsonSerializer.SerializeToUtf8Bytes(catalog,Json.Options);return JsonSerializer.SerializeToUtf8Bytes(new SignedEnvelope("ECDSA-P256-SHA256-P1363",Convert.ToBase64String(payload),Convert.ToBase64String(key.SignData(payload,HashAlgorithmName.SHA256,DSASignatureFormat.IeeeP1363FixedFieldConcatenation))),Json.Options);
}
await Test("valid signature and independent language selections",()=>Sync(()=>
{
    var f=Fixture();var m=Package(f);var game=f.Game(m);using var key=ECDsa.Create(ECCurve.NamedCurves.nistP256);using var http=new HttpClient();var service=new CatalogService(http,key.ExportSubjectPublicKeyInfoPem());
    var cat=service.Verify(Envelope(new(1,"test",DateTimeOffset.UtcNow.AddHours(1),[game]),key));Assert(cat.Games.Count==1);
    foreach(var text in new[]{"pl","en"})foreach(var voice in new[]{"pl","en"})
    {
        var plText=m with {Id="polish-text",Required=false,Files=[]};var plVoice=plText with {Id="polish-dubbing"};
        // Empty optional manifests remain unavailable until a real package exists; test selections against real fixture files.
        plText=plText with{Files=[new("Binaries/text.txt","data/mod.bin",m.Files[0].Sha256,"copy","mod-authored")]};
        plVoice=plVoice with{Files=[new("Binaries/voice.txt","data/mod.bin",m.Files[0].Sha256,"copy","mod-authored")]};
        var plan=PlanBuilder.Build(game with{Modules=[m,plText,plVoice]},f.Location(),new(text,voice,"en",[]));Assert(plan.Choices.TextLanguage==text&&plan.Choices.VoiceLanguage==voice);Assert(plan.Modules.Any(x=>x.Id=="polish-text")== (text=="pl"));Assert(plan.Modules.Any(x=>x.Id=="polish-dubbing")== (voice=="pl"));
    }
}));
await Test("tampered catalog signature fails",async()=>{using var key=ECDsa.Create(ECCurve.NamedCurves.nistP256);using var other=ECDsa.Create(ECCurve.NamedCurves.nistP256);using var http=new HttpClient();await Reject(()=>Sync(()=>new CatalogService(http,key.ExportSubjectPublicKeyInfoPem()).Verify(Envelope(new(1,"bad",DateTimeOffset.UtcNow.AddHours(1),[]),other))));});
await Test("expired signed catalog fails",async()=>{using var key=ECDsa.Create(ECCurve.NamedCurves.nistP256);using var http=new HttpClient();await Reject(()=>Sync(()=>new CatalogService(http,key.ExportSubjectPublicKeyInfoPem()).Verify(Envelope(new(1,"expired",DateTimeOffset.UtcNow.AddSeconds(-1),[]),key))));});
await Test("pinned repository and immutable release paths",async()=>
{
    CatalogService.ValidateReleaseUrl(new("https://github.com/trilex07/TrilexModInstaller/releases/download/v1/a.zip"),"trilex07/TrilexModInstaller");
    foreach(var url in new[]{"http://github.com/trilex07/TrilexModInstaller/releases/download/v1/a.zip","https://evil.test/releases/download/v1/a.zip","https://github.com/evil/repo/releases/download/v1/a.zip","https://github.com/trilex07/TrilexModInstaller/releases/latest/a.zip"})await Reject(()=>Sync(()=>CatalogService.ValidateReleaseUrl(new(url),"trilex07/TrilexModInstaller")));
});
await Test("multiple Steam libraries and nested VDF",()=>Sync(()=>
{
    var f=Fixture();var secondary=Path.Combine(f.Root,"Other library");var steam=Path.Combine(f.Root,"Steam");Directory.CreateDirectory(Path.Combine(steam,"steamapps"));Directory.CreateDirectory(Path.Combine(secondary,"steamapps","common"));
    var gameRoot=Path.Combine(secondary,"steamapps","common","Singularity Test");Directory.CreateDirectory(gameRoot);Directory.CreateDirectory(Path.Combine(gameRoot,"Binaries"));Directory.CreateDirectory(Path.Combine(gameRoot,"RvGame","CookedPC"));File.Copy(Path.Combine(f.Root,"Binaries","Singularity.exe"),Path.Combine(gameRoot,"Binaries","Singularity.exe"));
    File.WriteAllText(Path.Combine(steam,"steamapps","libraryfolders.vdf"),"\"libraryfolders\" { \"1\" { \"path\" \""+secondary.Replace("\\","\\\\")+"\" \"apps\" { \"42670\" \"123\" } } }");
    File.WriteAllText(Path.Combine(secondary,"steamapps","appmanifest_42670.acf"),"\"AppState\" { \"appid\" \"42670\" \"installdir\" \"Singularity Test\" }");
    var found=new SingularityGameAdapter().Detect([steam],f.Game()).ToList();Assert(found.Count==1&&found[0].Compatible);
}));
await Test("Darkness source consent binds only scoped raw recipes without changing the signed output",async()=>
{
    var path="Cache.DLC/PCPatchManifests/Cache.Windows/B.Font.cache";
    var file=new PackageFile(path,"",new string('A',64),"recipe","local-transform",new string('B',64),Recipe:[new("instructions",0,3,"patch.svrp")]);
    var bound=DarknessRecipeConsent.Bind("darkness2",file,new string('C',64));
    Assert(bound.Sha256==file.Sha256&&ReferenceEquals(bound.Recipe,file.Recipe)&&bound.SourceSha256 is null&&bound.BeforeSha256==new string('C',64));
    foreach(var invalid in new[]{file with{Path="d3d9.dll"},file with{SourcePath=path},file with{Recipe=[new("image",0,3,"patch.svrp")]}})
        await Reject(()=>Sync(()=>DarknessRecipeConsent.Bind("darkness2",invalid,new string('C',64))));
    await Reject(()=>Sync(()=>DarknessRecipeConsent.Bind("singularity",file,new string('C',64))));
    await Reject(()=>Sync(()=>DarknessRecipeConsent.Bind("darkness2",file,null!)));
});
await Test("Darkness consented source attempts replacement, binds bytes, and restores exact originals",async()=>
{
    foreach(var mode in new[]{"replace","decline-output","changed-source","invalid-range","already-target"})
    {
        var f=Fixture();var module=Package(f);var path="Cache.DLC/PCPatchManifests/Cache.Windows/B.Font.cache";
        var target=Paths.Safe(f.Root,path);Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllText(target,mode=="already-target"?"NEW":"OWNED");var original=Paths.Hash(target);
        var recipePath=Path.Combine(f.Root,"font.svrp");
        using(var writer=new BinaryWriter(File.Create(recipePath)))
        {
            writer.Write("SVRP"u8.ToArray());writer.Write((uint)1);writer.Write((ulong)3);writer.Write((uint)1);
            writer.Write((byte)0);writer.Write((ulong)(mode=="invalid-range"?999:0));writer.Write((uint)3);
        }
        using(var zip=ZipFile.Open(Archives(f,module)[module.Id],ZipArchiveMode.Update)){zip.GetEntry("data/mod.bin")!.Delete();zip.CreateEntryFromFile(recipePath,"font.svrp");}
        var expected=Convert.ToHexString(SHA256.HashData("NEW"u8.ToArray()));
        var file=new PackageFile(path,"",expected,"recipe","local-transform",new string('0',64),Recipe:[new("instructions",0,3,"font.svrp")]);
        var bound=mode=="already-target"?file:DarknessRecipeConsent.Bind("darkness2",file,original);
        module=module with {ArchiveSha256=Paths.Hash(Archives(f,module)[module.Id]),DownloadBytes=new FileInfo(Archives(f,module)[module.Id]).Length,Files=[bound]};
        if(mode=="changed-source")File.WriteAllText(target,"LATER");
        var beforeInstall=Paths.Hash(target);var confirmations=0;
        bool Confirm(ReconstructionWarning warning){confirmations++;return mode=="replace";}
        if(mode is "replace" or "already-target")
        {
            var receipt=await f.Installer.InstallAsync(f.Plan(module),Archives(f,module),null,default,confirmMismatch:Confirm);
            Assert(File.ReadAllText(target)==(mode=="replace"?"OWN":"NEW"));
            Assert(confirmations==(mode=="replace"?1:0)&&receipt.Files.Single().OriginalHash==original);
            Assert(f.Installer.Verify(f.Root).Count==0);f.Installer.Uninstall(f.Root,default);Assert(Paths.Hash(target)==original);
        }
        else
        {
            await Reject(()=>f.Installer.InstallAsync(f.Plan(module),Archives(f,module),null,default,confirmMismatch:Confirm));
            Assert(Paths.Hash(target)==beforeInstall&&f.Installer.Receipt(f.Root) is null);
            Assert(confirmations==(mode=="decline-output"?1:0));
        }
    }
});
await Test("manual location and executable mismatch",()=>Sync(()=>{var f=Fixture();var game=f.Game();var adapter=new SingularityGameAdapter();Assert(adapter.Validate(f.Root,game).Compatible);File.AppendAllText(Path.Combine(f.Root,"Binaries","Singularity.exe"),"different build");Assert(!adapter.Validate(f.Root,game).Compatible);Assert(!adapter.Validate(Path.Combine(f.Root,"missing"),game).Compatible);}));
await Test("unverified disc/GOG executable needs exact explicit consent",async()=>
{
    var f=Fixture();var module=Package(f);var game=f.Game(module);var exe=Path.Combine(f.Root,"Binaries","Singularity.exe");
    var pe=new byte[256];pe[0]=0x4D;pe[1]=0x5A;pe[0x3C]=0x80;pe[0x80]=0x50;pe[0x81]=0x45;pe[0x84]=0x4C;pe[0x85]=1;pe[0x98]=0x0B;pe[0x99]=1;File.WriteAllBytes(exe,pe);
    var location=new SingularityGameAdapter().Validate(f.Root,game);Assert(!location.Compatible&&ExecutableCompatibility.CanTry(game,location));
    var choices=new InstallChoices("en","en","en",[]);var consent=new ExecutableConsent(f.Root,location.ExeHash!);
    await Reject(()=>Sync(()=>PlanBuilder.Build(game,location,choices)));
    await Reject(()=>Sync(()=>PlanBuilder.Build(game,location,choices,consent with{Sha256=new string('0',64)})));
    await Reject(()=>Sync(()=>PlanBuilder.Build(game,location,choices,consent with{Root=Path.Combine(f.Root,"other")})));
    await Reject(()=>Sync(()=>PlanBuilder.Build(game with{State="unavailable"},location,choices,consent)));
    var plan=PlanBuilder.Build(game,location,choices,consent);Assert(plan.ExecutableConsent==consent&&plan.ExeSha256==location.ExeHash);
    var installed=await f.Installer.InstallAsync(plan,Archives(f,module),null,default);Assert(installed.ExecutableConsent==consent&&f.Installer.Verify(f.Root).Count==0);
    Assert(f.Installer.Receipt(f.Root)!.ExecutableConsent==consent);f.Installer.Uninstall(f.Root,default);Assert(Paths.Hash(exe)==location.ExeHash);
});
await Test("unverified executable consent preserves integrity and freshness gates",async()=>
{
    var f=Fixture();var module=Package(f);var game=f.Game(module);var exe=Path.Combine(f.Root,"Binaries","Singularity.exe");
    var pe=new byte[256];pe[0]=0x4D;pe[1]=0x5A;pe[0x3C]=0x80;pe[0x80]=0x50;pe[0x81]=0x45;pe[0x84]=0x4C;pe[0x85]=1;pe[0x98]=0x0B;pe[0x99]=1;File.WriteAllBytes(exe,pe);
    var location=new SingularityGameAdapter().Validate(f.Root,game);var consent=new ExecutableConsent(f.Root,location.ExeHash!);
    var plan=PlanBuilder.Build(game,location,new("en","en","en",[]),consent);
    await Reject(()=>f.Installer.InstallAsync(plan with{Modules=[module with{ArchiveSha256=new string('0',64)}]},Archives(f,module),null,default));
    await Reject(()=>f.Installer.InstallAsync(plan with{ExecutableConsent=consent with{Root=Path.Combine(f.Root,"other")}},Archives(f,module),null,default));
    File.AppendAllText(exe,"changed after review");await Reject(()=>f.Installer.InstallAsync(plan,Archives(f,module),null,default));
    Assert(!ExecutableCompatibility.CanTry(game,location));Assert(!File.Exists(Path.Combine(f.Root,"Binaries","d3d9.dll")));
});
await Test("missing and non-x86 executable cannot use unverified consent",()=>Sync(()=>
{
    var f=Fixture();var game=f.Game();var exe=Path.Combine(f.Root,"Binaries","Singularity.exe");File.AppendAllText(exe,"not a PE");
    var adapter=new SingularityGameAdapter();Assert(!ExecutableCompatibility.CanTry(game,adapter.Validate(f.Root,game)));
    File.Delete(exe);Assert(!ExecutableCompatibility.CanTry(game,adapter.Validate(f.Root,game)));
}));
await Test("only pinned PSVR2 helper executable can install and restore",async()=>
{
    var f=Fixture();var module=Package(f);var helper="Binaries/SingularityPsvr2Bridge.exe";
    module=module with{Files=[module.Files[0] with{Path=helper}]};
    await f.Installer.InstallAsync(f.Plan(module),Archives(f,module),null,default);
    Assert(Paths.Same(Paths.Hash(Paths.Safe(f.Root,helper)),module.Files[0].Sha256));
    f.Installer.Uninstall(f.Root,default);Assert(!File.Exists(Paths.Safe(f.Root,helper)));
    foreach(var name in new[]{"Binaries/Singularity.exe","Binaries/other.exe","RvGame/SingularityPsvr2Bridge.exe","Binaries/subdir/SingularityPsvr2Bridge.exe"})
        await Reject(()=>Sync(()=>Paths.AllowedTarget(name)));
    var bad=module with{Files=[module.Files[0] with{Sha256=new string('0',64)}]};
    await Reject(()=>f.Installer.InstallAsync(f.Plan(bad),Archives(f,bad),null,default));
    Assert(!File.Exists(Paths.Safe(f.Root,helper)));
});
await Test("Darkness II detects the signed Steam fingerprint and rejects unknown builds",async ()=>{
    var f=Fixture();
    var library=Path.Combine(f.Root,"steamapps");
    var gameRoot=Path.Combine(library,"common","Darkness II");
    Directory.CreateDirectory(Path.Combine(gameRoot,"Cache.Windows"));
    var exe=Path.Combine(gameRoot,"DarknessII.exe");
    File.WriteAllText(exe,"synthetic Darkness II");
    File.WriteAllText(Path.Combine(library,"appmanifest_67370.acf"),""" "AppState" { "appid" "67370" "installdir" "Darkness II" } """);
    var module=Package(f);
    module=module with{Files=[module.Files[0] with{Path="d3d9.dll"}]};
    var game=new GameManifest("darkness2","Darkness II","preview","Darkness2GameAdapter","67370","DarknessII.exe",[Paths.Hash(exe)],[module]);
    var adapter=new Darkness2GameAdapter();
    var location=adapter.Detect([f.Root],game).Single();
    Assert(location.Compatible);
    var plan=PlanBuilder.Build(game,location,new("en","en","en",[]));
    await f.Installer.InstallAsync(plan,Archives(f,module),null,default);
    Assert(File.Exists(Path.Combine(gameRoot,"d3d9.dll")));
    f.Installer.Uninstall(gameRoot,default);
    Assert(!File.Exists(Path.Combine(gameRoot,"d3d9.dll")));
    Assert(ProcessGuardPolicy.Refusal(gameRoot,1,2,"DarknessII",null)!=null);
    Assert(ProcessGuardPolicy.Refusal(gameRoot,1,2,"Singularity",null)==null);
    File.AppendAllText(exe,"changed");
    await Reject(()=>Sync(()=>PlanBuilder.Build(game,adapter.Validate(gameRoot,game),new("en","en","en",[]))));
});
await Test("Darkness II manual x86 build requires exact consent and rejects executable changes",async ()=>{
    var f=Fixture();
    Directory.CreateDirectory(Path.Combine(f.Root,"Cache.Windows"));
    var exe=Path.Combine(f.Root,"DarknessII.exe");
    var pe=new byte[256];pe[0]=0x4D;pe[1]=0x5A;pe[0x3C]=0x80;pe[0x80]=0x50;pe[0x81]=0x45;
    pe[0x84]=0x4C;pe[0x85]=1;pe[0x98]=0x0B;pe[0x99]=1;File.WriteAllBytes(exe,pe);
    var module=Package(f);module=module with{Files=[module.Files[0] with{Path="d3d9.dll"}]};
    var game=new GameManifest("darkness2","Darkness II","preview","Darkness2GameAdapter","67370","DarknessII.exe",[new string('0',64)],[module]);
    var adapter=new Darkness2GameAdapter();
    var location=adapter.Validate(f.Root,game);
    Assert(!location.Compatible&&ExecutableCompatibility.CanTry(game,location));
    var choices=new InstallChoices("en","en","en",[]);
    await Reject(()=>Sync(()=>PlanBuilder.Build(game,location,choices)));
    var plan=PlanBuilder.Build(game,location,choices,new(f.Root,location.ExeHash!));
    var receipt=await f.Installer.InstallAsync(plan,Archives(f,module),null,default);
    Assert(receipt.ExecutableConsent==plan.ExecutableConsent);
    f.Installer.Uninstall(f.Root,default);
    pe[0x84]=0x64;pe[0x85]=0x86;File.WriteAllBytes(exe,pe);
    Assert(!ExecutableCompatibility.CanTry(game,adapter.Validate(f.Root,game)));
    await Reject(()=>f.Installer.InstallAsync(plan,Archives(f,module),null,default));
});
await Test("Only the Darkness II source download accepts changed texture ZIPs",async ()=>{
    var f=Fixture();
    var target=Path.Combine(f.Root,"textures.zip");
    byte[] Zip(string name,byte sample)
    {
        using var memory=new MemoryStream();
        using(var zip=new ZipArchive(memory,ZipArchiveMode.Create,true))
        using(var entry=zip.CreateEntry(name).Open())entry.Write(new byte[]{137,80,78,71,13,10,26,10,sample});
        return memory.ToArray();
    }
    var first=Zip("weapons/one.png",1);
    var update=Zip("tentacles/another.png",2);
    foreach(var bytes in new[]{first,update})
    {
        using var http=new HttpClient(new FakeHttp(bytes));
        await new DarknessTextureDownload(http).SaveAsync(target,null,default);
        Assert(File.ReadAllBytes(target).SequenceEqual(bytes));
    }
    using var bad=new HttpClient(new FakeHttp(Zip("../escape.png",3)));
    await Reject(()=>new DarknessTextureDownload(bad).SaveAsync(target,null,default));
    Assert(File.ReadAllBytes(target).SequenceEqual(update));
    Assert(!Directory.EnumerateFiles(f.Root,"*.part").Any());
});
await Test("Darkness texture targets cannot enter a Singularity manifest",async ()=>{
    var f=Fixture();var m=Package(f);
    var texture=m with{Files=[m.Files[0] with{Path="Cache.Windows/H.Texture.toc"}]};
    await Reject(()=>Sync(()=>PlanBuilder.ValidateManifest(f.Game(texture))));
    var game=new GameManifest("darkness2","Darkness II","preview","Darkness2GameAdapter","67370","DarknessII.exe",[new string('0',64)],[texture]);
    PlanBuilder.ValidateManifest(game);
});
await Test("Local texture conversion rejects unvalidated ZIPs before copying game data",async ()=>{
    var f=Fixture();var archive=Path.Combine(f.Root,"bad.zip");
    using(var zip=ZipFile.Open(archive,ZipArchiveMode.Create))
    using(var output=zip.CreateEntry("../escape.png").Open())output.Write(new byte[]{137,80,78,71,13,10,26,10});
    var workspace=Path.Combine(f.Root,"conversion");
    await Reject(()=>DarknessTextureInstall.PrepareAsync(f.Root,f.Installer,workspace,archive,default));
    Assert(!Directory.Exists(workspace));
    using var cancellation=new CancellationTokenSource();cancellation.Cancel();
    try{await DarknessTextureDownload.ValidateArchiveAsync(archive,cancellation.Token);throw new Exception("Expected cancellation");}
    catch(OperationCanceledException){}
});
await Test("Drive package bytes remain hash pinned and redirects cannot leave Drive",async ()=>{
    var f=Fixture();
    var m=Package(f) with{DownloadUrl="https://drive.usercontent.google.com/download?id=fixture_123&export=download&confirm=t"};
    using var http=new HttpClient(new FakeHttp(File.ReadAllBytes(Archives(f,m)[m.Id])));
    var path=await new PackageDownloader(http,Path.Combine(f.Root,"drive")).GetAsync(m,false,null,default);
    Assert(Paths.Hash(path)==m.ArchiveSha256);
    using var bad=new HttpClient(new FakeHttp([]){Redirect="https://release-assets.githubusercontent.com/file"});
    await Reject(()=>new PackageDownloader(bad,Path.Combine(f.Root,"bad-drive")).GetAsync(m,false,null,default));
    await Reject(()=>Sync(()=>CatalogService.ValidateReleaseUrl(new Uri(m.DownloadUrl!),"trilex07/TrilexModInstaller")));
});
await Test("missing packages and dependency cycles",async()=>
{
    var f=Fixture();var m=Package(f);await Reject(()=>Sync(()=>PlanBuilder.Build(f.Game(m with{Dependencies=["missing"]}),f.Location(),new("en","en","en",[]))));
    var a=m with{Dependencies=["ui"]};var b=m with{Id="ui",Required=false,Dependencies=["vr-core"]};await Reject(()=>Sync(()=>PlanBuilder.Build(f.Game(a,b),f.Location(),new("en","en","en",[]))));
    await Reject(()=>Sync(()=>PlanBuilder.Build(f.Game(m with{State="unavailable"}),f.Location(),new("en","en","en",[]))));
});
await Test("rights-unapproved stable modules refused",async()=>{var f=Fixture();var m=Package(f);await Reject(()=>Sync(()=>PlanBuilder.ValidateManifest(f.Game(m with{RightsVerified=false}))));});
await Test("Explicit signed publisher review preserves unverified rights and refuses incomplete approval",async()=>
{
    var f=Fixture();var m=Package(f) with{RightsVerified=false,PublisherReview=new("fan-mod-publisher-approved","docs/distribution/FIRST-EDITION.md","Third-party rights unverified; reserved to their owners")};
    using var key=ECDsa.Create(ECCurve.NamedCurves.nistP256);using var http=new HttpClient();
    var catalog=new CatalogService(http,key.ExportSubjectPublicKeyInfoPem()).Verify(Envelope(new(1,"fan-review",DateTimeOffset.UtcNow.AddHours(1),[f.Game(m)]),key));
    Assert(!catalog.Games[0].Modules[0].RightsVerified);
    Assert(PlanBuilder.Build(catalog.Games[0],f.Location(),new("en","en","en",[])).Modules.Count==1);
    foreach(var review in new[]{new PublisherReview("unknown","evidence","notice"),new("fan-mod-publisher-approved","","notice"),new("fan-mod-publisher-approved","evidence","")})
        await Reject(()=>Sync(()=>PlanBuilder.ValidateManifest(f.Game(m with{PublisherReview=review}))));
});
await Test("unsafe paths and Windows device names",async()=>
{
    foreach(var p in new[]{"../outside","/absolute","C:/absolute","Binaries/a.dll:stream","Binaries/CON.dll","Binaries/a./x","Binaries//x","Binaries/../x"})await Reject(()=>Sync(()=>Paths.Safe(testRoot,p)));
});
await Test("corrupt ZIP and wrong SHA refused",async()=>{var f=Fixture();var m=Package(f);File.AppendAllText(Archives(f,m)[m.Id],"corrupt");await Reject(()=>Sync(()=>PackageExtractor.Extract(Archives(f,m)[m.Id],Path.Combine(f.Root,"stage"),m)));});
await Test("zip slip refused without outside writes",async()=>
{
    var f=Fixture();var m=Package(f);var zip=Archives(f,m)[m.Id];File.Delete(zip);using(var z=ZipFile.Open(zip,ZipArchiveMode.Create)){using var w=new StreamWriter(z.CreateEntry("../../escape").Open());w.Write("bad");}m=m with{ArchiveSha256=Paths.Hash(zip),DownloadBytes=new FileInfo(zip).Length};await Reject(()=>Sync(()=>PackageExtractor.Extract(zip,Path.Combine(f.Root,"stage"),m)));Assert(!File.Exists(Path.Combine(testRoot,"escape")));
});
await Test("duplicate ZIP names and symlinks refused",async()=>
{
    foreach(var symlink in new[]{false,true}){var f=Fixture();var m=Package(f);var path=Archives(f,m)[m.Id];File.Delete(path);using(var z=ZipFile.Open(path,ZipArchiveMode.Create)){var entry=z.CreateEntry("data/mod.bin");if(symlink)entry.ExternalAttributes=unchecked((int)0xA1FF0000);using(var w=new StreamWriter(entry.Open()))w.Write("x");if(!symlink)using(var w=new StreamWriter(z.CreateEntry("DATA/MOD.BIN").Open()))w.Write("x");}m=m with{ArchiveSha256=Paths.Hash(path),DownloadBytes=new FileInfo(path).Length};await Reject(()=>Sync(()=>PackageExtractor.Extract(path,Path.Combine(f.Root,"stage"),m)));}
});
await Test("whole original cooked packages never distributed",async()=>{var f=Fixture();var m=Package(f);m=m with{Files=[m.Files[0] with{Path="RvGame/CookedPC/commercial.xxx"}]};await Reject(()=>Sync(()=>PackageExtractor.Extract(Archives(f,m)[m.Id],Path.Combine(f.Root,"stage"),m)));});
await Test("install verify repeat and uninstall preserve originals",async()=>
{
    var f=Fixture();var m=Package(f);var ini=Path.Combine(f.Root,"Binaries","SingularityVR.ini");File.WriteAllText(ini,"; custom\r\n[User]\r\nValue=keep\r\n");var before=File.ReadAllText(ini);
    await f.Installer.InstallAsync(f.Plan(m),Archives(f,m),null,default);Assert(f.Installer.Verify(f.Root).Count==0);await f.Installer.InstallAsync(f.Plan(m),Archives(f,m),null,default);Assert(f.Installer.Verify(f.Root).Count==0);
    f.Installer.Uninstall(f.Root,default);Assert(!File.Exists(Path.Combine(f.Root,"Binaries","trilex-test.dll")));Assert(new IniDocument(File.ReadAllText(ini)).Get("User/Value")=="keep");Assert(new IniDocument(File.ReadAllText(ini)).Get("Input/Test") is null);Assert(f.Installer.Receipt(f.Root) is null);
});
await Test("preparation progress identifies active work before game writes; install follows complete staging",async()=>
{
    var f=Fixture();var m=Package(f);var rows=new List<ProgressInfo>();
    var progress=new InlineProgress(p=>
    {
        rows.Add(p);Assert(p.Total>0&&p.Completed>=0&&p.Completed<=p.Total);
        if(p.Stage is "Extract" or "Prepare")
            Assert(!File.Exists(Path.Combine(f.Root,"Binaries/trilex-test.dll"))&&!File.Exists(Path.Combine(f.Root,"Binaries/SingularityVR.ini")),"Game modified before preparation completed");
        if(p.Stage=="Install")Assert(rows.Any(r=>r.Stage=="Prepare"&&r.Completed==r.Total),"Install started without complete preparation");
    });
    await f.Installer.InstallAsync(f.Plan(m),Archives(f,m),progress,default);
    Assert(rows[0] is {Stage:"Extract",Completed:0,Total:1});
    Assert(rows.Count(p=>p.Stage=="Prepare")==m.Files.Count*2);
    foreach(var file in m.Files)Assert(rows.Any(p=>p.Stage=="Prepare"&&p.Item==file.Path&&p.Completed<p.Total));
    Assert(rows[^1] is {Stage:"Install",Completed:2,Total:2});Assert(f.Installer.Verify(f.Root).Count==0);
});
await Test("cancelling preparation reports active target and leaves game untouched",async()=>
{
    var f=Fixture();var m=Package(f);using var cancel=new CancellationTokenSource();var rows=new List<ProgressInfo>();
    var progress=new InlineProgress(p=>{rows.Add(p);if(p.Stage=="Prepare")cancel.Cancel();});
    try{await f.Installer.InstallAsync(f.Plan(m),Archives(f,m),progress,cancel.Token);throw new Exception("Expected preparation cancellation");}catch(OperationCanceledException){}
    Assert(rows[^1].Stage=="Prepare"&&rows[^1].Item==m.Files[0].Path);
    Assert(!rows.Any(p=>p.Stage=="Install")&&!File.Exists(Path.Combine(f.Root,"Binaries/trilex-test.dll"))&&f.Installer.Receipt(f.Root)is null);
});
await Test("fault halfway rolls back exact preimages",async()=>
{
    var f=Fixture();var m=Package(f);var ini=Path.Combine(f.Root,"Binaries","SingularityVR.ini");File.WriteAllText(ini,"[User]\nKeep=1\n");var hash=Paths.Hash(ini);await Reject(()=>f.Installer.InstallAsync(f.Plan(m),Archives(f,m),null,default,i=>throw new IOException("Injected failure")));Assert(!File.Exists(Path.Combine(f.Root,"Binaries","trilex-test.dll")));Assert(Paths.Hash(ini)==hash);Assert(f.Installer.Receipt(f.Root) is null);
});
await Test("upgrade and deselect restore original ownership",async()=>
{
    var f=Fixture();var m=Package(f);await f.Installer.InstallAsync(f.Plan(m),Archives(f,m),null,default);var v2=Package(f,"v2","VERSION2");await f.Installer.InstallAsync(f.Plan(v2),Archives(f,v2),null,default);Assert(File.ReadAllText(Path.Combine(f.Root,"Binaries","trilex-test.dll"))=="VERSION2");f.Installer.Uninstall(f.Root,default);Assert(!File.Exists(Path.Combine(f.Root,"Binaries","trilex-test.dll")));
});
await Test("repair corrupt and missing file retains original backup",async()=>
{
    var f=Fixture();var m=Package(f);await f.Installer.InstallAsync(f.Plan(m),Archives(f,m),null,default);var target=Path.Combine(f.Root,"Binaries","trilex-test.dll");File.WriteAllText(target,"BROKEN");Assert(f.Installer.Verify(f.Root).Count==1);await f.Installer.RepairAsync(f.Plan(m),Archives(f,m),null,default);Assert(f.Installer.Verify(f.Root).Count==0);File.Delete(target);await f.Installer.RepairAsync(f.Plan(m),Archives(f,m),null,default);Assert(f.Installer.Verify(f.Root).Count==0);f.Installer.Uninstall(f.Root,default);Assert(!File.Exists(target));
});
await Test("foreign mod and external edits preserved",async()=>
{
    var f=Fixture();var m=Package(f);var target=Path.Combine(f.Root,"Binaries","trilex-test.dll");File.WriteAllText(target,"OTHER-MOD");await Reject(()=>f.Installer.InstallAsync(f.Plan(m),Archives(f,m),null,default));Assert(File.ReadAllText(target)=="OTHER-MOD");File.Delete(target);await f.Installer.InstallAsync(f.Plan(m),Archives(f,m),null,default);File.WriteAllText(target,"USER-EDIT");var kept=f.Installer.Uninstall(f.Root,default);Assert(kept.Count==1&&File.ReadAllText(target)=="USER-EDIT"&&f.Installer.Receipt(f.Root) is null);
});
await Test("game reinstalled outside installer: repair, uninstall and fresh install still work",async()=>
{
    var f=Fixture();var m=Package(f);await f.Installer.InstallAsync(f.Plan(m),Archives(f,m),null,default);
    var dll=Path.Combine(f.Root,"Binaries","trilex-test.dll");var ini=Path.Combine(f.Root,"Binaries","SingularityVR.ini");
    File.Delete(dll);File.Delete(ini);Assert(f.Installer.Verify(f.Root).Count==2);
    await f.Installer.RepairAsync(f.Plan(m),Archives(f,m),null,default);Assert(f.Installer.Verify(f.Root).Count==0);
    File.Delete(dll);File.Delete(ini);
    f.Installer.Uninstall(f.Root,default);Assert(f.Installer.Receipt(f.Root) is null&&!File.Exists(dll));
    await f.Installer.InstallAsync(f.Plan(m),Archives(f,m),null,default);Assert(f.Installer.Verify(f.Root).Count==0);
    File.WriteAllText(dll,"LEFTOVER-OLDER-MOD");await Reject(()=>f.Installer.InstallAsync(f.Plan(m),Archives(f,m),null,default));
    await f.Installer.RepairAsync(f.Plan(m),Archives(f,m),null,default);Assert(f.Installer.Verify(f.Root).Count==0);
    File.Delete(dll);File.Delete(ini);var v2=Package(f,"v2");
    await f.Installer.InstallAsync(f.Plan(v2),Archives(f,v2),null,default);Assert(f.Installer.Verify(f.Root).Count==0);
});
await Test("user INI changes preserved across upgrade and uninstall",async()=>
{
    var f=Fixture();var m=Package(f);await f.Installer.InstallAsync(f.Plan(m),Archives(f,m),null,default);var target=Path.Combine(f.Root,"Binaries","SingularityVR.ini");var ini=new IniDocument(File.ReadAllText(target));ini.Set("Input/Test","user");ini.Set("User/New","42");File.WriteAllText(target,ini.ToString());var v2=Package(f,"v2");await f.Installer.InstallAsync(f.Plan(v2),Archives(f,v2),null,default);f.Installer.Uninstall(f.Root,default);ini=new(File.ReadAllText(target));Assert(ini.Get("Input/Test")=="user"&&ini.Get("User/New")=="42");
});
await Test("running game blocks writes and recovery",async()=>{var f=Fixture();var m=Package(f);f.Guard.Busy=true;await Reject(()=>f.Installer.InstallAsync(f.Plan(m),Archives(f,m),null,default));await Reject(()=>Sync(()=>f.Installer.Recover(f.Root)));Assert(!File.Exists(Path.Combine(f.Root,"Binaries","trilex-test.dll")));});
await Test("cancel before installation leaves no writes",async()=>{var f=Fixture();var m=Package(f);using var ct=new CancellationTokenSource();ct.Cancel();try{await f.Installer.InstallAsync(f.Plan(m),Archives(f,m),null,ct.Token);throw new Exception("Expected cancellation");}catch(OperationCanceledException){}Assert(!File.Exists(Path.Combine(f.Root,"Binaries","trilex-test.dll")));});
await Test("local reconstruction reads owned originals and verifies output",async()=>
{
    var f=Fixture();var source=Path.Combine(f.Root,"source.bin");File.WriteAllText(source,"ABCD1234");var stage=Path.Combine(f.Root,"stage");Directory.CreateDirectory(stage);File.WriteAllText(Path.Combine(stage,"new.bin"),"MOD");var expected=SHA256.HashData(Encoding.UTF8.GetBytes("ABMOD34"));var recipe=new PackageFile("RvGame/CookedPC/new.xxx","",Convert.ToHexString(expected),"recipe","local-transform",Paths.Hash(source),Recipe:[new("original",0,2),new("payload",0,3,"new.bin"),new("original",6,2)]);Assert(Encoding.UTF8.GetString(PackageExtractor.Reconstruct(source,stage,recipe))=="ABMOD34");await Reject(()=>Sync(()=>PackageExtractor.Reconstruct(source,stage,recipe with{BeforeSha256=new string('0',64)})));
});
await Test("reconstruction mismatch identifies output and operation in both output paths",async()=>
{
    var f=Fixture();var source=Path.Combine(f.Root,"source.bin");File.WriteAllText(source,"OWNED");
    var stage=Path.Combine(f.Root,"stage");Directory.CreateDirectory(stage);
    var actual=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("OWNED")));
    var recipe=new PackageFile("Binaries/vr-assets/failing.bin","",new string('0',64),"recipe","local-transform",Paths.Hash(source),Recipe:[new("original",0,5)],SourcePath:"RvGame/CookedPC/owned.xxx");
    foreach(var disk in new[]{false,true})
    {
        try
        {
            if(disk)PackageExtractor.ReconstructTo(source,stage,recipe,Path.Combine(stage,"out.bin"));
            else PackageExtractor.Reconstruct(source,stage,recipe);
            throw new Exception("Mismatched output was accepted");
        }
        catch(InvalidDataException e)
        {
            Assert(e.Message.Contains("Locally reconstructed output hash mismatch: "+recipe.Path));
            Assert(e.Message.Contains("source="+recipe.SourcePath)&&e.Message.Contains("codec=none")&&e.Message.Contains("operations=original"));
            Assert(e.Message.Contains("bytes=5")&&e.Message.Contains("expected SHA256="+recipe.Sha256)&&e.Message.Contains("actual SHA256="+actual));
            Assert(!e.Message.Contains(f.Root));
        }
    }
    await Task.CompletedTask;
});
await Test("output mismatch requires explicit approval and records actual bytes for verify and uninstall",async()=>
{
    var f=Fixture();var m=MismatchPackage(f);var path="RvGame/CookedPC/test.xxx";var target=Paths.Safe(f.Root,path);var original=Paths.Hash(target);
    await Reject(()=>f.Installer.InstallAsync(f.Plan(m),Archives(f,m),null,default));
    var calls=0;await Reject(()=>f.Installer.InstallAsync(f.Plan(m),Archives(f,m),null,default,confirmMismatch:w=>{calls++;return false;}));
    Assert(calls==1&&Paths.Hash(target)==original&&f.Installer.Receipt(f.Root) is null);
    var receipt=await f.Installer.InstallAsync(f.Plan(m),Archives(f,m),null,default,confirmMismatch:w=>{calls++;Assert(w.Path==path&&w.ExpectedSha256==new string('0',64));return true;});
    Assert(calls==2&&File.ReadAllText(target)=="MOD"&&f.Installer.Verify(f.Root).Count==0);
    var stored=f.Installer.Receipt(f.Root)!;var warning=stored.ReconstructionWarnings!.Single();
    Assert(warning.ActualSha256==Paths.Hash(target)&&warning.ExpectedSha256!=warning.ActualSha256);
    Assert(stored.Files.Single(x=>x.Path==path).InstalledHash==warning.ActualSha256);
    Assert(receipt.ReconstructionWarnings!.Count==1);
    // Approval is not a persistent bypass for the next regeneration.
    await Reject(()=>f.Installer.InstallAsync(f.Plan(m),Archives(f,m),null,default));
    Assert(f.Installer.Receipt(f.Root)!.TransactionId==receipt.TransactionId);
    File.WriteAllText(target,"DAMAGED");Assert(f.Installer.Verify(f.Root).Count==1);
    await Reject(()=>f.Installer.RepairAsync(f.Plan(m),Archives(f,m),null,default));
    Assert(File.ReadAllText(target)=="DAMAGED");
    await f.Installer.RepairAsync(f.Plan(m),Archives(f,m),null,default,w=>true);
    Assert(File.ReadAllText(target)=="MOD"&&f.Installer.Verify(f.Root).Count==0);
    f.Installer.Uninstall(f.Root,default);Assert(Paths.Hash(target)==original);
    Assert(!File.Exists(Paths.Safe(f.Root,"Binaries/trilex-test.dll"))&&f.Installer.Receipt(f.Root) is null);
});
await Test("extractor override accepts only a completed output mismatch in both APIs",async()=>
{
    var f=Fixture();var source=Path.Combine(f.Root,"owned.bin");File.WriteAllText(source,"OWNED");var stage=Path.Combine(f.Root,"stage");Directory.CreateDirectory(stage);
    var file=new PackageFile("RvGame/CookedPC/generated.xxx","",new string('0',64),"recipe","local-transform",Paths.Hash(source),Recipe:[new("original",0,5)]);
    var calls=0;bool Approve(ReconstructionWarning warning){calls++;Assert(warning.Bytes==5&&warning.ActualSha256==Paths.Hash(source));return true;}
    Assert(Encoding.UTF8.GetString(PackageExtractor.Reconstruct(source,stage,file,Approve))=="OWNED");
    var output=Path.Combine(stage,"output.bin");PackageExtractor.ReconstructTo(source,stage,file,output,Approve);Assert(File.ReadAllText(output)=="OWNED"&&calls==2);
    await Reject(()=>Sync(()=>PackageExtractor.Reconstruct(source,stage,file with{BeforeSha256=new string('1',64)},Approve)));
    Assert(calls==2);
});
await Test("mismatch approval cannot bypass original, archive or game checks",async()=>
{
    foreach(var fault in new[]{"source","archive","game"})
    {
        var f=Fixture();var m=MismatchPackage(f);var calls=0;
        if(fault=="source")File.WriteAllText(Paths.Safe(f.Root,"RvGame/CookedPC/test.xxx"),"FOREIGN");
        var plan=f.Plan(m);
        if(fault=="archive")File.AppendAllText(Archives(f,m)[m.Id],"TAMPER");
        if(fault=="game")File.AppendAllText(Paths.Safe(f.Root,"Binaries/Singularity.exe"),"TAMPER");
        await Reject(()=>f.Installer.InstallAsync(plan,Archives(f,m),null,default,confirmMismatch:w=>{calls++;return true;}));
        Assert(calls==0&&f.Installer.Receipt(f.Root) is null&&!File.Exists(Paths.Safe(f.Root,"Binaries/trilex-test.dll")));
    }
});
await Test("declining a later mismatch writes no prepared replacements",async()=>
{
    var f=Fixture();var m=MismatchPackage(f,true);var calls=0;
    await Reject(()=>f.Installer.InstallAsync(f.Plan(m),Archives(f,m),null,default,confirmMismatch:w=>++calls==1));
    Assert(calls==2&&f.Installer.Receipt(f.Root) is null&&!File.Exists(Paths.Safe(f.Root,"Binaries/trilex-test.dll")));
    foreach(var path in new[]{"RvGame/CookedPC/test.xxx","RvGame/CookedPC/test2.xxx"})Assert(File.ReadAllText(Paths.Safe(f.Root,path))=="ORIGINAL");
});
await Test("approved mismatch still rolls back on apply failure and respects cancellation",async()=>
{
    var f=Fixture();var m=MismatchPackage(f);var target=Paths.Safe(f.Root,"RvGame/CookedPC/test.xxx");
    await Reject(()=>f.Installer.InstallAsync(f.Plan(m),Archives(f,m),null,default,i=>{if(i==2)throw new IOException("Synthetic failure after installing the approved mismatch");},confirmMismatch:w=>true));
    Assert(File.ReadAllText(target)=="ORIGINAL"&&f.Installer.Receipt(f.Root) is null&&!File.Exists(Paths.Safe(f.Root,"Binaries/trilex-test.dll")));
    using var ct=new CancellationTokenSource();
    try
    {
        await f.Installer.InstallAsync(f.Plan(m),Archives(f,m),null,ct.Token,confirmMismatch:w=>{ct.Cancel();return true;});
        throw new Exception("Cancellation ignored");
    }
    catch(OperationCanceledException){}
    Assert(File.ReadAllText(target)=="ORIGINAL"&&f.Installer.Receipt(f.Root) is null);
});
await Test("download selection cache retries and resumed range",async()=>
{
    var f=Fixture();var m=Package(f);var payload=File.ReadAllBytes(Archives(f,m)[m.Id]);var handler=new FakeHttp(payload);using var http=new HttpClient(handler);var cache=Path.Combine(f.Root,"cache");Directory.CreateDirectory(cache);File.WriteAllBytes(Path.Combine(cache,m.ArchiveSha256!.ToLowerInvariant()+".zip.part"),payload[..10]);var downloader=new PackageDownloader(http,cache);var path=await downloader.GetAsync(m,false,null,default);Assert(handler.Requests==1&&handler.LastRange==10&&Paths.Same(Paths.Hash(path),m.ArchiveSha256));await downloader.GetAsync(m,true,null,default);Assert(handler.Requests==1);await Reject(()=>downloader.GetAsync(m with{ArchiveSha256=new string('A',64)},true,null,default));
});
await Test("download wrong hash and unsafe redirects fail",async()=>
{
    var f=Fixture();var m=Package(f);using var http=new HttpClient(new FakeHttp(File.ReadAllBytes(Archives(f,m)[m.Id])));await Reject(()=>new PackageDownloader(http,Path.Combine(f.Root,"cache")).GetAsync(m with{ArchiveSha256=new string('0',64)},false,null,default));
    using var bad=new HttpClient(new FakeHttp([]){Redirect="https://127.0.0.1/secret"});await Reject(()=>new PackageDownloader(bad,Path.Combine(f.Root,"cache2")).GetAsync(m,false,null,default));
});
await Test("hard process exit recovers durable journal",async()=>
{
    var f=Fixture();var m=Package(f);var moduleJson=Path.Combine(f.Root,"module.json");Json.Write(moduleJson,m);
    var exe=Path.Combine(AppContext.BaseDirectory,"Trilex.Installer.Tests.exe");
    var start=new System.Diagnostics.ProcessStartInfo(exe){UseShellExecute=false,CreateNoWindow=true};foreach(var a in new[]{"--crash",f.Root,f.State,Archives(f,m)[m.Id],moduleJson})start.ArgumentList.Add(a);
    using var child=System.Diagnostics.Process.Start(start)!;await child.WaitForExitAsync();Assert(child.ExitCode==86);Assert(File.Exists(Path.Combine(f.Root,"Binaries","trilex-test.dll")));Assert(f.Installer.NeedsRecovery(f.Root));f.Installer.Recover(f.Root);Assert(!f.Installer.NeedsRecovery(f.Root));Assert(!File.Exists(Path.Combine(f.Root,"Binaries","trilex-test.dll")));Assert(f.Installer.Receipt(f.Root) is null);
});
await Test("duplicate controlled INI keys fail closed",async()=>{await Reject(()=>Sync(()=>new IniDocument("[Input]\nTest=1\nTest=2\n").Set("Input/Test","3")));});
await Test("catalog follows trusted GitHub release redirect and verifies signature",async()=>
{
    using var key=ECDsa.Create(ECCurve.NamedCurves.nistP256);var bytes=Envelope(new(1,"redirect",DateTimeOffset.UtcNow.AddHours(1),[]),key);
    using var http=new HttpClient(new FakeHttp(bytes){Redirect="https://release-assets.githubusercontent.com/github-production-release-asset/test?signature=temporary"});
    var cat=await new CatalogService(http,key.ExportSubjectPublicKeyInfoPem()).FetchAsync(new("https://github.com/trilex07/TrilexModInstaller/releases/download/v1/catalog.json"),default);Assert(cat.Version=="redirect");
});
await Test("transient HTTP failure retries only requested package",async()=>
{
    var f=Fixture();var m=Package(f);var handler=new FakeHttp(File.ReadAllBytes(Archives(f,m)[m.Id])){FailFirst=true};using var http=new HttpClient(handler);
    var path=await new PackageDownloader(http,Path.Combine(f.Root,"cache")).GetAsync(m,false,null,default);Assert(handler.Requests==2&&Paths.Hash(path)==m.ArchiveSha256);
});
await Test("cancellation after applied write restores exact prior files",async()=>
{
    var f=Fixture();var m=Package(f);using var cancel=new CancellationTokenSource();
    try{await f.Installer.InstallAsync(f.Plan(m),Archives(f,m),null,cancel.Token,_=>cancel.Cancel());throw new Exception("Cancellation expected");}catch(OperationCanceledException){}
    Assert(!File.Exists(Path.Combine(f.Root,"Binaries/trilex-test.dll"))&&f.Installer.Receipt(f.Root)is null);
});
await Test("game started mid transaction defers recovery safely",async()=>
{
    var f=Fixture();var m=Package(f);await Reject(()=>f.Installer.InstallAsync(f.Plan(m),Archives(f,m),null,default,_=>f.Guard.Busy=true));
    Assert(File.Exists(Path.Combine(f.Root,"Binaries/trilex-test.dll")));f.Guard.Busy=false;f.Installer.Recover(f.Root);Assert(!File.Exists(Path.Combine(f.Root,"Binaries/trilex-test.dll"))&&f.Installer.Receipt(f.Root)is null);
});
await Test("executable changed after review is refused before writes",async()=>
{
    var f=Fixture();var m=Package(f);var plan=f.Plan(m);File.AppendAllText(Path.Combine(f.Root,"Binaries/Singularity.exe"),"changed");await Reject(()=>f.Installer.InstallAsync(plan,Archives(f,m),null,default));Assert(!File.Exists(Path.Combine(f.Root,"Binaries/trilex-test.dll")));
});
await Test("deselected module removes only its owned content",async()=>
{
    var f=Fixture();var m=Package(f);await f.Installer.InstallAsync(f.Plan(m),Archives(f,m),null,default);var v2=Package(f,"v2");v2=v2 with{Files=v2.Files.Where(x=>x.Kind=="copy").ToList()};await f.Installer.InstallAsync(f.Plan(v2),Archives(f,v2),null,default);Assert(!File.Exists(Path.Combine(f.Root,"Binaries/SingularityVR.ini")));Assert(f.Installer.Verify(f.Root).Count==0);
});
await Test("streamed local recipe installs and uninstalls exact owned original",async()=>
{
    var f=Fixture();var target=Path.Combine(f.Root,"RvGame/CookedPC/fixture.xxx");File.WriteAllText(target,"ABCD1234");
    var before=Paths.Hash(target);var zipPath=Path.Combine(f.Root,"recipe.zip");using(var zip=ZipFile.Open(zipPath,ZipArchiveMode.Create)){using var w=new StreamWriter(zip.CreateEntry("new.bin").Open(),new UTF8Encoding(false));w.Write("MOD");}
    var file=new PackageFile("RvGame/CookedPC/fixture.xxx","",Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("ABMOD34"))),"recipe","local-transform",before,Recipe:[new("original",0,2),new("payload",0,3,"new.bin"),new("original",6,2)]);
    var module=new ModuleManifest("vr-core",new(){{"en","Fixture"}},"recipe","stable",true,[],new FileInfo(zipPath).Length,"https://github.com/trilex07/TrilexModInstaller/releases/download/test/recipe.zip",Paths.Hash(zipPath),3,true,"Synthetic authored bytes; original remains local",[file],"");
    await f.Installer.InstallAsync(f.Plan(module),new(){{module.Id,zipPath}},null,default);Assert(File.ReadAllText(target)=="ABMOD34");f.Installer.Uninstall(f.Root,default);Assert(Paths.Hash(target)==before);
});
await Test("untouched original UTF16 INI restores byte exact",async()=>
{
    var f=Fixture();var m=Package(f);var ini=Path.Combine(f.Root,"Binaries/SingularityVR.ini");File.WriteAllText(ini,"; ORIGINAL\r\n[User]\r\nKeep=ĹĽĂłĹ‚Ä‡\r\n",Encoding.Unicode);var before=Paths.Hash(ini);
    await f.Installer.InstallAsync(f.Plan(m),Archives(f,m),null,default);f.Installer.Uninstall(f.Root,default);Assert(Paths.Hash(ini)==before);
});
await Test("corrupt original backup refuses uninstall without touching managed bytes",async()=>
{
    var f=Fixture();var m=Package(f);var target=Path.Combine(f.Root,"Binaries/trilex-test.dll");File.WriteAllText(target,"ORIGINAL");m=m with{Files=m.Files.Select(x=>x.Kind=="copy"?x with{BeforeSha256=Paths.Hash(target)}:x).ToList()};
    var receipt=await f.Installer.InstallAsync(f.Plan(m),Archives(f,m),null,default);var installed=Paths.Hash(target);var owned=receipt.Files.First(x=>x.Kind=="copy");File.WriteAllText(Paths.Safe(f.Installer.StateDirectory(f.Root),owned.Backup),"BAD BACKUP");await Reject(()=>Sync(()=>f.Installer.Uninstall(f.Root,default)));Assert(Paths.Hash(target)==installed);
});
await Test("shared INI modules merge but conflicting values fail before game writes",async()=>
{
    var f=Fixture();var m=Package(f);var second=new ModuleManifest("ui",new(){{"en","UI"}},"v1","stable",false,["vr-core"],22,"https://github.com/trilex07/TrilexModInstaller/releases/download/test/ini.zip","",0,true,"Synthetic INI settings",[new("Binaries/SingularityVR.ini","","","ini","mod-authored",IniValues:new(){{"Input/Other","2"}})],"");
    var empty=Path.Combine(f.Root,"ini.zip");using(ZipFile.Open(empty,ZipArchiveMode.Create)){}second=second with{ArchiveSha256=Paths.Hash(empty),DownloadBytes=new FileInfo(empty).Length};var archives=Archives(f,m);archives[second.Id]=empty;
    await f.Installer.InstallAsync(f.Plan(m,second),archives,null,default);var iniPath=Path.Combine(f.Root,"Binaries/SingularityVR.ini");var ini=new IniDocument(File.ReadAllText(iniPath));Assert(ini.Get("Input/Test")=="1"&&ini.Get("Input/Other")=="2");var before=Paths.Hash(iniPath);second=second with{Files=[second.Files[0] with{IniValues=new(){{"Input/Test","CONFLICT"}}}]};await Reject(()=>f.Installer.InstallAsync(f.Plan(m,second),archives,null,default));Assert(Paths.Hash(iniPath)==before);
});
await Test("local original proxy is preserved without redistribution across install repair and uninstall",async()=>
{
    var f=Fixture();var m=Package(f);var source=Path.Combine(f.Root,"Binaries/dinput8.dll");File.WriteAllText(source,"SYNTHETIC ORIGINAL DIRECTINPUT");var hash=Paths.Hash(source);var length=(int)new FileInfo(source).Length;
    var proxy=m.Files.Single(x=>x.Kind=="copy") with{Path="Binaries/dinput8.dll",BeforeSha256=hash};
    var local=new PackageFile("Binaries/dinput8_orig.dll","",hash,"recipe","local-transform",Recipe:[new("original",0,length)],SourcePath:"Binaries/dinput8.dll",SourceSha256:hash);
    m=m with{Files=[proxy,local]};var archives=Archives(f,m);
    await f.Installer.InstallAsync(f.Plan(m),archives,null,default);Assert(Paths.Hash(Path.Combine(f.Root,local.Path))==hash);Assert(Paths.Hash(source)==proxy.Sha256);
    await f.Installer.InstallAsync(f.Plan(m),archives,null,default);File.Delete(Path.Combine(f.Root,local.Path));await f.Installer.InstallAsync(f.Plan(m),archives,null,default,repair:true);Assert(Paths.Hash(Path.Combine(f.Root,local.Path))==hash);
    f.Installer.Uninstall(f.Root,default);Assert(Paths.Hash(source)==hash&&!File.Exists(Path.Combine(f.Root,local.Path)));
});
await Test("external local reconstruction source mismatch is refused before writes",async()=>
{
    var f=Fixture();var m=Package(f);var source=Path.Combine(f.Root,"Binaries/dinput8.dll");File.WriteAllText(source,"WRONG ORIGINAL");var before=Paths.Hash(source);
    var local=new PackageFile("Binaries/dinput8_orig.dll","",new string('A',64),"recipe","local-transform",Recipe:[new("original",0,1)],SourcePath:"Binaries/dinput8.dll",SourceSha256:new string('B',64));m=m with{Files=[local]};
    await Reject(()=>f.Installer.InstallAsync(f.Plan(m),Archives(f,m),null,default));Assert(Paths.Hash(source)==before&&!File.Exists(Path.Combine(f.Root,local.Path)));
});
await Test("unsafe cross-file reconstruction sources are rejected",async()=>
{
    var f=Fixture();var m=Package(f);foreach(var path in new[]{"../outside.dll","Binaries/Singularity.exe","C:/outside.dll"})
    {
        var local=new PackageFile("Binaries/local.dll","",new string('A',64),"recipe","local-transform",Recipe:[new("original",0,1)],SourcePath:path,SourceSha256:new string('B',64));
        await Reject(()=>Sync(()=>PackageExtractor.Extract(Archives(f,m)[m.Id],Path.Combine(f.State,Guid.NewGuid().ToString("N")),m with{Files=[..m.Files,local]})));
    }
});
await Test("system source identities cannot redirect to arbitrary files",()=>Sync(()=>
{
    foreach(var source in new[]{"windows-directinput8-x86/../../secret","C:/Windows/System32/dinput8.dll","powershell.exe"})
    {
        var f=new PackageFile("Binaries/local.dll","",new string('A',64),"recipe","local-transform",Recipe:[new("original",0,1)],SourceSha256:new string('A',64),SystemSource:source);
        try{LocalSources.Validate(f,testRoot);throw new Exception("Unknown system source accepted");}catch(InvalidDataException){}
    }
}));
await Test("local source declarations require one exact identity and hash",()=>Sync(()=>
{
    var f=new PackageFile("Binaries/local.dll","",new string('A',64),"recipe","local-transform",Recipe:[new("original",0,1)]);
    foreach(var bad in new[]{f with{SourceSha256=new string('A',64)},f with{SourcePath="Binaries/a.dll"},f with{SourcePath="Binaries/a.dll",SystemSource="windows-directinput8-x86",SourceSha256=new string('A',64)}})
    {try{LocalSources.Validate(bad,testRoot);throw new Exception("Invalid source accepted");}catch(InvalidDataException){}}
}));
await Test("native package XOR deltas install grow shrink repair and restore originals",async()=>
{
    foreach(var text in new[]{"EDITED","LONGER REBUILT NATIVE PACKAGE","CUT"})
    {
        var f=Fixture();var target=Path.Combine(f.Root,"RvGame/CookedPC/fixture.xxx");var old=Encoding.UTF8.GetBytes("ORIGINAL SYNTHETIC PACKAGE");var next=Encoding.UTF8.GetBytes(text);File.WriteAllBytes(target,old);var before=Paths.Hash(target);var common=Math.Min(old.Length,next.Length);var mask=old.Take(common).Select((x,i)=>(byte)(x^next[i])).ToArray();var archive=Path.Combine(f.Root,"delta.zip");
        using(var zip=ZipFile.Open(archive,ZipArchiveMode.Create)){using(var dst=zip.CreateEntry("fixture.xor").Open())dst.Write(mask);if(next.Length>common){using var dst=zip.CreateEntry("tail.bin").Open();dst.Write(next.AsSpan(common));}}
        var parts=new List<PatchPart>{new("xor-original",0,common,"fixture.xor")};if(next.Length>common)parts.Add(new("payload",0,next.Length-common,"tail.bin"));
        var file=new PackageFile("RvGame/CookedPC/fixture.xxx","",Convert.ToHexString(SHA256.HashData(next)),"recipe","local-transform",BeforeSha256:before,Recipe:parts);
        var module=new ModuleManifest("delta",new(){{"en","Synthetic delta"}},"v1","stable",true,[],new FileInfo(archive).Length,"https://github.com/trilex07/TrilexModInstaller/releases/download/test/delta.zip",Paths.Hash(archive),old.Length+next.Length,true,"Synthetic test data; no original game bytes",[file],"");
        await f.Installer.InstallAsync(f.Plan(module),new(){{module.Id,archive}},null,default);Assert(File.ReadAllBytes(target).SequenceEqual(next));
        File.WriteAllText(target,"CORRUPT");await f.Installer.InstallAsync(f.Plan(module),new(){{module.Id,archive}},null,default,repair:true);Assert(File.ReadAllBytes(target).SequenceEqual(next));
        f.Installer.Uninstall(f.Root,default);Assert(Paths.Hash(target)==before);
    }
});
await Test("invalid XOR range and corrupted delta cannot produce accepted output",async()=>
{
    var f=Fixture();var original=Path.Combine(f.Root,"RvGame/CookedPC/source.xxx");File.WriteAllText(original,"ORIGINAL");var stage=Path.Combine(f.Root,"delta-stage");Directory.CreateDirectory(stage);File.WriteAllBytes(Path.Combine(stage,"mask"),[0]);
    var file=new PackageFile("RvGame/CookedPC/source.xxx","",Paths.Hash(original),"recipe","local-transform",BeforeSha256:Paths.Hash(original),Recipe:[new("xor-original",0,8,"mask")]);
    await Reject(()=>Sync(()=>PackageExtractor.Reconstruct(original,stage,file)));
    File.WriteAllBytes(Path.Combine(stage,"mask"),new byte[8]);file=file with{Sha256=new string('A',64)};await Reject(()=>Sync(()=>PackageExtractor.Reconstruct(original,stage,file)));
});
await Test("local UE3 decoder reconstructs from owned compressed input and restores originals",async()=>
{
    var f=Fixture();var original=Paths.Safe(f.Root,"RvGame/CookedPC/source.xxx");
    byte[] decoded=[0xc1,0x83,0x2a,0x9e,0x48,2,0x7e,0,1,2,3,4,5,6,7,8];
    using(var data=new MemoryStream()) {using(var writer=new BinaryWriter(data,System.Text.Encoding.UTF8,true)){writer.Write(0x9e2a83c1u);writer.Write(131072u);writer.Write(decoded.Length);writer.Write(decoded.Length);writer.Write(decoded.Length);writer.Write(decoded.Length);writer.Write(decoded);writer.Write(new byte[16]);}File.WriteAllBytes(original,data.ToArray());}
    var hash=Convert.ToHexString(SHA256.HashData(decoded));var stage=Path.Combine(f.Root,"decoder-stage");Directory.CreateDirectory(stage);
    var file=new PackageFile("Binaries/vr-assets/test/native.bin","",hash,"recipe","local-transform",Recipe:[new("original",0,decoded.Length)],SourcePath:"RvGame/CookedPC/source.xxx",SourceSha256:Paths.Hash(original),SourceCodec:"ue3-whole-lzo",DecodedSha256:hash,DecodedBytes:decoded.Length);
    Assert(PackageExtractor.Reconstruct(original,stage,file).SequenceEqual(decoded));
    Assert(PackageExtractor.Reconstruct(original,stage,file).SequenceEqual(decoded));
    File.WriteAllText(Path.Combine(stage,"_local-decoded",hash+".u"),"CORRUPT");await Reject(()=>Sync(()=>PackageExtractor.Reconstruct(original,stage,file)));
    var archive=Path.Combine(f.Root,"decode.zip");using(ZipFile.Open(archive,ZipArchiveMode.Create)){}
    var module=new ModuleManifest("decoded","en".Split().ToDictionary(x=>x,x=>"Local decode"),"v1","stable",true,[],new FileInfo(archive).Length,"https://github.com/trilex07/TrilexModInstaller/releases/download/test/decode.zip",Paths.Hash(archive),decoded.Length,true,"Synthetic owned UE3 container, bundled GPL standalone decoder",[file],"");
    var before=Paths.Hash(original);await f.Installer.InstallAsync(f.Plan(module),new(){{module.Id,archive}},null,default);
    Assert(File.ReadAllBytes(Paths.Safe(f.Root,file.Path)).SequenceEqual(decoded));Assert(Paths.Hash(original)==before);
    f.Installer.Uninstall(f.Root,default);Assert(!File.Exists(Paths.Safe(f.Root,file.Path))&&Paths.Hash(original)==before);
});
await Test("decoder declarations reject arbitrary executables codecs and unbounded output",async()=>
{
    var file=new PackageFile("Binaries/vr-assets/native.bin","",new string('A',64),"recipe","local-transform",BeforeSha256:new string('B',64),Recipe:[new("original",0,1)],SourceCodec:"ue3-whole-lzo",DecodedSha256:new string('C',64),DecodedBytes:16);
    foreach(var invalid in new[]{file with{SourceCodec="powershell"},file with{DecodedBytes=536870913},file with{DecodedSha256="bad"},file with{SourceCodec=null},file with{DecodedBytes=null}})await Reject(()=>Sync(()=>LocalSources.Validate(invalid,testRoot)));
});
await Test("binary recipes preserve copies and literals and reject malformed instructions",async()=>
{
    var f=Fixture();var source=Path.Combine(f.Root,"source.bin");File.WriteAllBytes(source,[10,20,30,40]);
    var recipe=Path.Combine(f.Root,"recipe.svrp");
    byte[] Make(byte opcode=0,ulong offset=1,bool trailing=false){using var m=new MemoryStream();using(var w=new BinaryWriter(m,System.Text.Encoding.UTF8,true)){w.Write("SVRP"u8.ToArray());w.Write(1u);w.Write(5ul);w.Write(2u);w.Write(opcode);if(opcode==0)w.Write(offset);w.Write(2u);w.Write((byte)1);w.Write(3u);w.Write(new byte[]{50,60,70});if(trailing)w.Write((byte)99);}return m.ToArray();}
    File.WriteAllBytes(recipe,Make());using(var output=new MemoryStream()){BinaryRecipe.Apply(source,recipe,output,5);Assert(output.ToArray().SequenceEqual(new byte[]{20,30,50,60,70}));}
    foreach(var bad in new[]{Make(2),Make(offset:4),Make(trailing:true),Make()[..^1]}){File.WriteAllBytes(recipe,bad);await Reject(()=>Sync(()=>{using var output=new MemoryStream();BinaryRecipe.Apply(source,recipe,output,5);}));}
});
await Test("owned TFC bulk mip becomes exact local image without a Python installation",async()=>
{
    var f=Fixture();var source=Paths.Safe(f.Root,"RvGame/CookedPC/coalesced.tfc");
    byte[] block=[0,248,31,0,0,0,0,0];
    using(var data=new MemoryStream()){using(var w=new BinaryWriter(data,System.Text.Encoding.UTF8,true)){w.Write(new byte[16]);w.Write(0x9e2a83c1u);w.Write(131072u);w.Write(8u);w.Write(8u);w.Write(8u);w.Write(8u);w.Write(block);}File.WriteAllBytes(source,data.ToArray());}
    var stage=Path.Combine(f.Root,"image-stage");Directory.CreateDirectory(stage);Json.Write(Path.Combine(stage,"image.json"),new ImageOperation(4,4,"svrt",1,"DXT1"));
    byte[] expected;using(var m=new MemoryStream()){using(var w=new BinaryWriter(m,System.Text.Encoding.UTF8,true)){w.Write("SVRT"u8.ToArray());w.Write(1);w.Write(4);w.Write(4);for(var i=0;i<16;i++)w.Write(new byte[]{0,0,255,255});}expected=m.ToArray();}
    var targetHash=Convert.ToHexString(SHA256.HashData(expected));
    var file=new PackageFile("Binaries/vr-assets/test/texture.svrt","",targetHash,"recipe","local-transform",Recipe:[new("image",0,expected.Length,"image.json")],SourcePath:"RvGame/CookedPC/coalesced.tfc",SourceSha256:Paths.Hash(source),SourceCodec:"ue3-bulk-lzo",DecodedSha256:Convert.ToHexString(SHA256.HashData(block)),DecodedBytes:8,SourceOffset:16,SourceBytes:32);
    Assert(PackageExtractor.Reconstruct(source,stage,file).SequenceEqual(expected));
    var deep=Path.Combine(stage,new string('a',75),new string('b',75),new string('c',75));Directory.CreateDirectory(deep);
    Json.Write(Path.Combine(deep,"image.json"),new ImageOperation(4,4,"svrt",1,"DXT1"));
    Assert(PackageExtractor.Reconstruct(source,deep,file).SequenceEqual(expected),"Full transaction long-path image failed");
    // A clean Steam layout has coalesced.tfc only. Exercise the installer,
    // rather than constructing a fixture with the developer's renamed cache.
    var module=Package(f);var archive=Archives(f,module)[module.Id];
    using(var zip=ZipFile.Open(archive,ZipArchiveMode.Update))zip.CreateEntryFromFile(Path.Combine(stage,"image.json"),"image.json");
    module=module with{Files=[..module.Files,file],ArchiveSha256=Paths.Hash(archive),DownloadBytes=new FileInfo(archive).Length};
    await f.Installer.InstallAsync(f.Plan(module),new(){{module.Id,archive}},null,default);
    Assert(f.Installer.Verify(f.Root).Count==0&&Paths.Hash(Paths.Safe(f.Root,file.Path))==targetHash);
    Assert(!File.Exists(Paths.Safe(f.Root,"RvGame/CookedPC/Textures_P0.tfc"))&&Paths.Hash(source)==file.SourceSha256);
    f.Installer.Uninstall(f.Root,default);
    Assert(!File.Exists(Paths.Safe(f.Root,file.Path))&&Paths.Hash(source)==file.SourceSha256);
    try{PackageExtractor.Reconstruct(Paths.Safe(f.Root,"RvGame/CookedPC/Textures_P0.tfc"),stage,file);throw new Exception("Missing preimage accepted");}
    catch(InvalidDataException e){Assert(e.Message.Contains(file.Path)&&e.Message.Contains(file.SourcePath!)&&e.Message.Contains("MISSING"));}
    foreach(var invalid in new[]{file with{SourceOffset=-1},file with{SourceBytes=536870913},file with{SourceCodec="ue3-whole-lzo"}})await Reject(()=>Sync(()=>LocalSources.Validate(invalid,stage)));
    Json.Write(Path.Combine(stage,"bad.json"),new ImageOperation(8192,8192,"svrt",4,"DXT1"));await Reject(()=>Sync(()=>{using var o=new MemoryStream();LocalImageGenerator.Apply(source,stage,Path.Combine(stage,"bad.json"),o,4);}));
});
await Test("image metadata cannot redirect paired sources or run unsupported operations",async()=>
{
    var f=Fixture();var raw=Path.Combine(f.Root,"raw");File.WriteAllBytes(raw,new byte[8]);var stage=Path.Combine(f.Root,"stage");Directory.CreateDirectory(stage);
    foreach(var op in new[]{new ImageOperation(4,4,"powershell",2,"DXT1"),new ImageOperation(4,4,"weapon-diffuse-v2",4,"DXT1"),new ImageOperation(4,4,"weapon-normal-v2",2,"DXT1"),new ImageOperation(4,4,"normal",2,"DXT1",new(0,32,new string('A',64))),new ImageOperation(4,4,"weapon-normal-v2",2,"DXT1",new(0,32,new string('A',64)))})
    {var metadata=Path.Combine(stage,"operation.json");Json.Write(metadata,op);await Reject(()=>Sync(()=>{using var output=new MemoryStream();LocalImageGenerator.Apply(raw,stage,metadata,output,4);}));}
});
await Test("knife geometry references cannot escape decoded original or change fixed topology",async()=>
{
    var f=Fixture();var source=Path.Combine(f.Root,"core");File.WriteAllBytes(source,new byte[7000]);var stage=Path.Combine(f.Root,"stage");Directory.CreateDirectory(stage);
    foreach(var op in new[]{new{kind="svrk",positions=new long[661],uvs=new long[662],indices=0L},new{kind="svrk",positions=Enumerable.Repeat(-1L,662).ToArray(),uvs=new long[662],indices=0L},new{kind="svnl",positions=new long[662],uvs=new long[662],indices=7000L}})
    {var metadata=Path.Combine(stage,"knife.json");Json.Write(metadata,op);await Reject(()=>Sync(()=>{using var output=new MemoryStream();LocalImageGenerator.ApplyKnife(source,stage,metadata,output,16+662*24+6060);}));}
});
await Test("V7 choice overrides reach actual INI and preserve accepted gameplay",async()=>
{
    var f=Fixture();var m=Package(f) with{Id="portable-settings"};
    var ini=m.Files.Single(x=>x.Kind=="ini") with{IniValues=new(){{"Diagnostic/NativeMuzzleStereoMode","1"},{"Localization/PolishLanguage","1"}}};
    m=m with{Files=[m.Files[0],ini]};
    foreach(var choice in new[]{new InstallChoices("en","en","pl",[]),new InstallChoices("pl","en","en",[]),new InstallChoices("pl","pl","pl",[])})
    {
        var applied=SingularityV7Settings.Apply([m],choice).Single().Files.Single(x=>x.Kind=="ini").IniValues!;
        Assert(applied["Localization/PolishLanguage"]==(choice.TextLanguage=="pl"?"1":"0"));
        Assert(applied["Localization/NativePolishSpeech"]==(choice.VoiceLanguage=="pl"?"1":"0"));
        Assert(applied["Localization/PolishIntroAudioFix"]==(choice.VoiceLanguage=="pl"?"1":"0"));
        Assert(applied["Diagnostic/NativeMuzzleStereoMode"]=="1");
    }
    await Reject(()=>Sync(()=>SingularityV7Settings.Apply([m],new("en","pl","en",[]))));
    Assert(m.Files.Single(x=>x.Kind=="ini").IniValues!["Localization/PolishLanguage"]=="1","Source manifest mutated");
});
await Test("VC++ prerequisite rejects missing corrupt and x64 without loading DLL",()=>Sync(()=>
{
    var f=Fixture();var runtime=Path.Combine(f.Root,"runtime");Directory.CreateDirectory(runtime);
    Assert(!WindowsPrerequisites.InspectDirectory(runtime).Present);
    File.WriteAllText(Path.Combine(runtime,"vcruntime140.dll"),"corrupt");Assert(!WindowsPrerequisites.InspectDirectory(runtime).Present);
    var bytes=new byte[160];BitConverter.GetBytes((ushort)0x5a4d).CopyTo(bytes,0);BitConverter.GetBytes(64).CopyTo(bytes,60);BitConverter.GetBytes(0x4550).CopyTo(bytes,64);BitConverter.GetBytes((ushort)0x8664).CopyTo(bytes,68);
    File.WriteAllBytes(Path.Combine(runtime,"vcruntime140.dll"),bytes);Assert(!WindowsPrerequisites.InspectDirectory(runtime).Present);
    Assert(WindowsPrerequisites.VisualCppX86Url=="https://aka.ms/vc14/vc_redist.x86.exe");
}));
await Test("PSVR2 x64 helper imports require x64 VCRUNTIME140 and VCRUNTIME140_1",()=>Sync(()=>
{
    var f=Fixture();var runtime=Path.Combine(f.Root,"runtime");Directory.CreateDirectory(runtime);
    Assert(!WindowsPrerequisites.InspectTriggerDirectory(runtime).Present);
    var bytes=new byte[160];BitConverter.GetBytes((ushort)0x5a4d).CopyTo(bytes,0);BitConverter.GetBytes(64).CopyTo(bytes,60);BitConverter.GetBytes(0x4550).CopyTo(bytes,64);BitConverter.GetBytes((ushort)0x14c).CopyTo(bytes,68);
    File.WriteAllBytes(Path.Combine(runtime,"vcruntime140.dll"),bytes);
    Assert(WindowsPrerequisites.InspectTriggerDirectory(runtime).Detail.Contains("Expected x64"));
    if(WindowsPrerequisites.InspectTriggerHelper().Present)
    {
        foreach(var name in new[]{"vcruntime140.dll","vcruntime140_1.dll"})File.Copy(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),name),Path.Combine(runtime,name),true);
        Assert(WindowsPrerequisites.InspectTriggerDirectory(runtime).Present);
        File.Delete(Path.Combine(runtime,"vcruntime140_1.dll"));Assert(!WindowsPrerequisites.InspectTriggerDirectory(runtime).Present);
    }
    Assert(WindowsPrerequisites.VisualCppX64Url=="https://aka.ms/vc14/vc_redist.x64.exe");
}));
await Test("Centurion fixed operations reject unknown mode wrong size and foreign original",async()=>
{
    var f=Fixture();var original=Path.Combine(f.Root,"not-original.xxx");File.WriteAllText(original,"synthetic foreign package");
    await Reject(()=>Sync(()=>LocalImageGenerator.ApplyCenturion(original,original,f.Root,"arbitrary",new MemoryStream(),1)));
    await Reject(()=>Sync(()=>LocalImageGenerator.ApplyCenturion(original,original,f.Root,"clip",new MemoryStream(),1)));
    await Reject(()=>Sync(()=>LocalImageGenerator.ApplyCenturion(original,original,f.Root,"clip",new MemoryStream(),33136)));
});
await Test("Inline texture slices reject invalid ranges and non-SVRT operations",async()=>
{
    var f=Fixture();var raw=Path.Combine(f.Root,"raw");File.WriteAllBytes(raw,new byte[128]);var metadata=Path.Combine(f.Root,"image.json");
    foreach(var op in new[]{new ImageOperation(4,4,"svrt",1,"DXT1",RawOffset:-1),new ImageOperation(4,4,"svrt",1,"DXT1",RawOffset:125),new ImageOperation(4,4,"diffuse",1,"DXT1",RawOffset:0)})
    {
        Json.Write(metadata,op);await Reject(()=>Sync(()=>LocalImageGenerator.Apply(raw,f.Root,metadata,new MemoryStream(),80)));
    }
});
await Test("Audio codec rejects arbitrary executable and offline missing cache without download",async()=>
{
    var f=Fixture();var priorHome=Environment.GetEnvironmentVariable("TRILEX_HOME");var priorCodec=Environment.GetEnvironmentVariable("TRILEX_AUDIO_CODEC");
    try
    {
        Environment.SetEnvironmentVariable("TRILEX_HOME",f.Root);Environment.SetEnvironmentVariable("TRILEX_AUDIO_CODEC",null);
        var handler=new FakeHttp([]);using var http=new HttpClient(handler);
        await Reject(()=>LocalAudioTools.EnsureAsync(http,true,null,default));Assert(handler.Requests==0);
        var bad=Path.Combine(f.Root,"foreign.exe");File.WriteAllText(bad,"not executable");
        await Reject(()=>Sync(()=>LocalAudioTools.VerifyEncoder(bad)));
        Environment.SetEnvironmentVariable("TRILEX_AUDIO_CODEC",bad);
        await Reject(()=>LocalAudioTools.EnsureAsync(http,false,null,default));Assert(handler.Requests==0);
    }
    finally{Environment.SetEnvironmentVariable("TRILEX_HOME",priorHome);Environment.SetEnvironmentVariable("TRILEX_AUDIO_CODEC",priorCodec);}
});
await Test("Optional extractor is not embedded and refuses missing offline cache or arbitrary import",async()=>
{
    var f=Fixture();var priorHome=Environment.GetEnvironmentVariable("TRILEX_HOME");var priorArchive=Environment.GetEnvironmentVariable("TRILEX_UEVIEWER_ARCHIVE");
    try
    {
        Environment.SetEnvironmentVariable("TRILEX_HOME",f.Root);Environment.SetEnvironmentVariable("TRILEX_UEVIEWER_ARCHIVE",null);
        Assert(!typeof(LocalCenturionGenerator).Assembly.GetManifestResourceNames().Any(n=>n.Contains("UEViewer")||n.Contains("SDL2")));
        var handler=new FakeHttp([]);using var http=new HttpClient(handler);
        await Reject(()=>LocalExtractorTools.EnsureAsync(http,true,null,default));Assert(handler.Requests==0);
        var bad=Path.Combine(f.Root,"foreign.zip");File.WriteAllText(bad,"not the pinned archive");
        await Reject(()=>Sync(()=>LocalExtractorTools.Import(bad)));
        Environment.SetEnvironmentVariable("TRILEX_UEVIEWER_ARCHIVE",bad);
        await Reject(()=>LocalExtractorTools.EnsureAsync(http,false,null,default));Assert(handler.Requests==0);
        await Reject(()=>Sync(()=>LocalExtractorTools.Resolve()));
        Environment.SetEnvironmentVariable("TRILEX_UEVIEWER_ARCHIVE",null);
        await Reject(()=>LocalExtractorTools.EnsureAsync(http,false,null,default));
        Assert(handler.Requests==1&&handler.LastUserAgent=="TrilexModInstaller/1.0"&&handler.LastReferrer=="https://www.gildor.org/en/projects/umodel");
    }
    finally{Environment.SetEnvironmentVariable("TRILEX_HOME",priorHome);Environment.SetEnvironmentVariable("TRILEX_UEVIEWER_ARCHIVE",priorArchive);}
});
await Test("Adjacent pinned exporter archive supports offline use and refuses tampering before network",async()=>
{
    var f=Fixture();var priorHome=Environment.GetEnvironmentVariable("TRILEX_HOME");var priorArchive=Environment.GetEnvironmentVariable("TRILEX_UEVIEWER_ARCHIVE");
    try
    {
        Environment.SetEnvironmentVariable("TRILEX_HOME",f.Root);Environment.SetEnvironmentVariable("TRILEX_UEVIEWER_ARCHIVE",null);
        var bundle=Path.Combine(f.Root,"bundle");Directory.CreateDirectory(Path.Combine(bundle,"tools"));
        var adjacent=Path.Combine(bundle,"tools/umodel_win32.zip");File.WriteAllText(adjacent,"tampered");
        var handler=new FakeHttp([]);using var http=new HttpClient(handler);
        await Reject(()=>LocalExtractorTools.EnsureAsync(http,false,null,default,bundle));Assert(handler.Requests==0);
        File.Delete(adjacent);handler.DnsFailure=true;
        try{await LocalExtractorTools.EnsureAsync(http,false,null,default,bundle);throw new Exception("Expected download failure");}
        catch(HttpRequestException e){Assert(e.Message.Contains("tools folder")&&e.Message.Contains("No game files"));}
        var official=Environment.GetEnvironmentVariable("TRILEX_TEST_UEVIEWER_ARCHIVE");
        if(!string.IsNullOrWhiteSpace(official))
        {
            File.Copy(official,adjacent);var requests=handler.Requests;
            await LocalExtractorTools.EnsureAsync(http,true,null,default,bundle);
            Assert(File.Exists(LocalExtractorTools.Resolve())&&handler.Requests==requests);
        }
    }
    finally{Environment.SetEnvironmentVariable("TRILEX_HOME",priorHome);Environment.SetEnvironmentVariable("TRILEX_UEVIEWER_ARCHIVE",priorArchive);}
});
await Test("Complete bundle uses pinned signed catalog and exact adjacent packages",async()=>
{
    var f=Fixture();var m=Package(f);using var key=ECDsa.Create(ECCurve.NamedCurves.nistP256);using var http=new HttpClient();var service=new CatalogService(http,key.ExportSubjectPublicKeyInfoPem());
    Assert(BundleSource.CatalogAt(f.Root,service) is null&&BundleSource.ArchiveAt(f.Root,m) is null);
    File.WriteAllBytes(Path.Combine(f.Root,"catalog.signed.json"),Envelope(new(1,"bundle",DateTimeOffset.UtcNow.AddHours(1),[f.Game(m)]),key));
    Assert(BundleSource.CatalogAt(f.Root,service)!.Version=="bundle");
    Directory.CreateDirectory(Path.Combine(f.Root,"packages"));var adjacent=Path.Combine(f.Root,"packages/vr-core.zip");File.Copy(Archives(f,m)[m.Id],adjacent);
    Assert(Paths.Same(BundleSource.ArchiveAt(f.Root,m),Path.GetFullPath(adjacent)));
    File.AppendAllText(adjacent,"tampered");await Reject(()=>Sync(()=>BundleSource.ArchiveAt(f.Root,m)));
    await Reject(()=>Sync(()=>BundleSource.ArchiveAt(f.Root,m with{Id="../foreign"})));
    File.WriteAllText(Path.Combine(f.Root,"catalog.signed.json"),"{\"algorithm\":\"untrusted\",\"payload\":\"\",\"signature\":\"\"}");await Reject(()=>Sync(()=>BundleSource.CatalogAt(f.Root,service)));
});
await Test("Windows dependency binds locally without changing pinned game assets or accepting arbitrary targets",async()=>
{
    var f=Fixture();var m=Package(f);var original=m.Files[0] with{Path="Binaries/dinput8_orig.dll",Kind="recipe",Ownership="local-transform",Entry="",SystemSource="windows-directinput8-x86",SourceSha256=new string('0',64),UseCurrentSystemVersion=true,Recipe=[new("original",0,1)]};
    var bound=LocalSources.BindCurrentSystem([m with{Files=[original,m.Files[1]]}])[0];
    var source=LocalSources.SystemPath("windows-directinput8-x86");Assert(bound.Files[0].Sha256==Paths.Hash(source)&&bound.Files[0].SourceSha256==Paths.Hash(source)&&bound.Files[0].Recipe![0].Length==new FileInfo(source).Length);
    Assert(bound.Files[1]==m.Files[1]&&original.Sha256==m.Files[0].Sha256);
    foreach(var wrong in new[]{original with{Path="Binaries/d3d9.dll"},original with{SourceCodec="ue3-whole-lzo"},original with{SourcePath="Binaries/foreign.dll"},original with{Recipe=[new("payload",0,1,"a")]}})
        await Reject(()=>Sync(()=>LocalSources.Validate(wrong,f.Root)));
});
await Test("SteamVR and fpsVR are tools, not running games; exact boundaries remain enforced",()=>Sync(()=>
{
    Assert(SteamRuntimePolicy.IsRuntimeApp("250820")&&SteamRuntimePolicy.IsRuntimeApp("908520"));
    foreach(var id in new[]{"42670","9085200","0",""})Assert(!SteamRuntimePolicy.IsRuntimeApp(id));
    foreach(var name in new[]{"SteamVR","fpsVR"})Assert(SteamRuntimePolicy.IsRuntimePath(@"D:\SteamLibrary\steamapps\common\"+name+@"\bin\tool.exe"));
    foreach(var name in new[]{"Singularity","fpsVR-game","SteamVR2"})Assert(!SteamRuntimePolicy.IsRuntimePath(@"D:\SteamLibrary\steamapps\common\"+name+@"\game.exe"));
}));
await Test("process guard blocks only Singularity across Steam/retail drives and identifies PID",()=>Sync(()=>
{
    const string root=@"D:\SteamLibrary\steamapps\common\Singularity";
    string? Check(int pid,string name,string? path)=>ProcessGuardPolicy.Refusal(root,123,pid,name,path);
    Assert(Check(123,"TrilexModInstaller",root+@"\installer\TrilexModInstaller.exe") is null);
    Assert(Check(124,"TrilexModInstaller",root+@"\installer\TrilexModInstaller.exe") is null);
    Assert(Check(124,"Singularity",null)?.Contains("PID 124")==true);
    Assert(Check(124,"Singularity",root+@"\Binaries\Singularity.exe")?.Contains(root)==true);
    Assert(Check(124,"tool",@"D:\SteamLibrary\steamapps\common\OtherTool\tool.exe") is null);
    Assert(Check(124,"vrserver",@"D:\SteamLibrary\steamapps\common\SteamVR\bin\vrserver.exe") is null);
    Assert(Check(124,"fpsVR",@"D:\SteamLibrary\steamapps\common\fpsVR\fpsVR.exe") is null);
    Assert(Check(124,"steam",@"D:\SteamLibrary\steam.exe") is null);
    Assert(Check(124,"explorer",null) is null);
    Assert(Check(124,"tool",@"C:\Downloads\tool.exe") is null);
    Assert(Check(124,"tool",root+@"\Binaries\tool.exe") is null);
    Assert(Check(124,"OtherGame",@"D:\SteamLibrary\steamapps\common\OtherGame\Game.exe") is null);
    Assert(Check(124,"singularity",@"D:\Retail\Singularity\Binaries\Singularity.exe") is not null);
    Assert(Check(124,"Singularity.exe",null) is not null);
    Assert(Check(124,"unknown",@"C:\Retail\Singularity\Binaries\SINGULARITY.EXE") is not null);
    Assert(Check(124,"SingularityLauncher",@"D:\Retail\SingularityLauncher.exe") is null);
}));
await Test("Universal launch prepares only the exact reviewed game EXE and its working directory",async()=>
{
    var f=Fixture();var module=Package(f);var game=f.Game(module);var location=f.Location();
    var receipt=new InstallReceipt("1",f.Root,"singularity","test",new("en","en","en",[]),[],"test");
    Assert(GameLauncher.CanLaunch(game,location,receipt));
    var info=GameLauncher.CreateStartInfo(game,location,receipt);
    Assert(Paths.Same(info.FileName,Paths.Safe(f.Root,game.Executable))&&Paths.Same(info.WorkingDirectory,Path.GetDirectoryName(info.FileName))&&info.Arguments=="");
    var unknown=location with{Compatible=false};
    Assert(!GameLauncher.CanLaunch(game,unknown,receipt));
    var accepted=receipt with{ExecutableConsent=new(f.Root,location.ExeHash!)};
    Assert(GameLauncher.CanLaunch(game,unknown,accepted));
    Assert(!GameLauncher.CanLaunch(game,unknown,accepted with{GameId="darkness2"}));
    Assert(!GameLauncher.CanLaunch(game,unknown,accepted with{Root=f.Root+"-other"}));
    Assert(!GameLauncher.CanLaunch(game,unknown,accepted with{ExecutableConsent=new(f.Root,new string('0',64))}));
    File.AppendAllText(Paths.Safe(f.Root,game.Executable),"changed");
    await Reject(()=>Sync(()=>GameLauncher.CreateStartInfo(game,unknown,accepted)));
});
await Test("Steam launch requires matching library manifest; retail and malformed manifests stay direct",async()=>
{
    var f=Fixture();var m=Package(f);var game=f.Game(m);
    var root=Path.Combine(f.Root,"library","steamapps","common","Singularity");
    var exe=Paths.Safe(root,game.Executable);Directory.CreateDirectory(Path.GetDirectoryName(exe)!);File.WriteAllText(exe,"fixture never executed");
    var hash=Paths.Hash(exe);var loc=new GameLocation(root,"Manual",false,"Unknown hash",hash);
    var receipt=new InstallReceipt("1",root,game.Id,"test",new("en","en","en",[]),[],"test",ExecutableConsent:new(root,hash));
    Assert(!GameLauncher.IsSteamInstallation(game,root));
    var manifest=Path.Combine(f.Root,"library","steamapps","appmanifest_"+game.SteamAppId+".acf");
    void Write(string app,string folder)=>File.WriteAllText(manifest,"\"AppState\" { \"appid\" \""+app+"\" \"installdir\" \""+folder+"\" }");
    Write(game.SteamAppId,"Singularity");
    var start=GameLauncher.CreateStartInfo(game,loc,receipt);
    Assert(start.FileName=="steam://rungameid/"+game.SteamAppId&&start.UseShellExecute&&start.Arguments=="");
    var tiny=game with{Id="wonderlands"};
    Assert(GameLauncher.CreateStartInfo(tiny,loc,receipt with{GameId="wonderlands"}).FileName=="steam://run/"+game.SteamAppId+"//-dx11/");
    var graphicsIni=Path.Combine(Path.GetDirectoryName(exe)!,"WonderlandsVR.ini");
    File.WriteAllText(graphicsIni,"[VR]\nD3D12=1\n");
    Assert(GameLauncher.CreateStartInfo(tiny,loc,receipt with{GameId="wonderlands"}).FileName=="steam://run/"+game.SteamAppId+"//-dx12/");
    File.Delete(graphicsIni);
    var dark=game with{Id="darkness2"};
    Assert(GameLauncher.CreateStartInfo(dark,loc,receipt with{GameId="darkness2"}).FileName=="steam://rungameid/"+game.SteamAppId);
    Assert(!GameLauncher.IsSteamInstallation(game with{SteamAppId="42/anything"},root));
    Write("99999","Singularity");Assert(!GameLauncher.IsSteamInstallation(game,root));
    Write(game.SteamAppId,"Another copy");Assert(!GameLauncher.IsSteamInstallation(game,root));
    Write(game.SteamAppId,"../outside");Assert(!GameLauncher.IsSteamInstallation(game,root));
    File.WriteAllText(manifest,"\"AppState\" {");Assert(!GameLauncher.IsSteamInstallation(game,root));
    var direct=GameLauncher.CreateStartInfo(game,loc,receipt);
    Assert(Paths.Same(direct.FileName,exe)&&Paths.Same(direct.WorkingDirectory,Path.GetDirectoryName(exe)));
    Write(game.SteamAppId,"Singularity");File.AppendAllText(exe,"changed");
    await Reject(()=>Sync(()=>GameLauncher.CreateStartInfo(game,loc,receipt)));
});
await Test("manual export prepares copy, merged INI and recipe without touching game or receipts",async()=>
{
    var f=Fixture();var m=Package(f,content:"MOD");var source=Paths.Safe(f.Root,"RvGame/CookedPC/manual.xxx");File.WriteAllText(source,"ORIGINAL");
    var recipe=new PackageFile("RvGame/CookedPC/manual.xxx","",m.Files[0].Sha256,"recipe","local-transform",Paths.Hash(source),Recipe:[new("payload",0,3,"data/mod.bin")]);
    // Fixture payload is MOD, so both the copied file and recipe have the same verified output.
    m=m with{Files=[..m.Files,recipe]};
    File.WriteAllText(Paths.Safe(f.Root,"Binaries/SingularityVR.ini"),"[User]\nKeep=1\n");
    var before=Directory.GetFiles(f.Root,"*",SearchOption.AllDirectories).ToDictionary(p=>p,Paths.Hash);
    var output=f.Root+"-manual";
    ManualExporter.Export(f.Plan(m),Archives(f,m),output,f.Installer,null,default);
    Assert(before.All(p=>Paths.Hash(p.Key)==p.Value)&&Directory.GetFiles(f.Root,"*",SearchOption.AllDirectories).Length==before.Count);
    Assert(f.Installer.Receipt(f.Root) is null&&!Directory.Exists(f.State));
    Assert(File.ReadAllText(Paths.Safe(output,"files/RvGame/CookedPC/manual.xxx"))=="MOD");
    var ini=new IniDocument(File.ReadAllText(Paths.Safe(output,"files/Binaries/SingularityVR.ini")));
    Assert(ini.Get("User/Keep")=="1"&&ini.Get("Input/Test")=="1");
    Assert(File.Exists(Paths.Safe(output,"README-EN.md"))&&File.Exists(Paths.Safe(output,"README-PL.md"))&&!Directory.Exists(Paths.Safe(output,"work")));
    foreach(var row in Json.Read<ManualExportReceipt>(Paths.Safe(output,"manual-manifest.json")).Files)Assert(Paths.Hash(Paths.Safe(output,"files/"+row.Path))==row.Sha256);
    await Reject(()=>Sync(()=>ManualExporter.Export(f.Plan(m),Archives(f,m),output,f.Installer,null,default)));
    await Reject(()=>Sync(()=>ManualExporter.Export(f.Plan(m),Archives(f,m),Path.Combine(f.Root,"export"),f.Installer,null,default)));
});
await Test("manual export rejects tampering and cancellation without partial deliveries",async()=>
{
    var f=Fixture();var m=Package(f,content:"MOD");var output=f.Root+"-manual-cancel";using var cancel=new CancellationTokenSource();cancel.Cancel();
    var cancelled=false;try{ManualExporter.Export(f.Plan(m),Archives(f,m),output,f.Installer,null,cancel.Token);}catch(OperationCanceledException){cancelled=true;}Assert(cancelled);
    Assert(!Directory.Exists(output)&&!Directory.GetDirectories(Path.GetDirectoryName(output)!,Path.GetFileName(output)+".partial-*").Any());
    File.AppendAllText(Archives(f,m)[m.Id],"tampered");
    await Reject(()=>Sync(()=>ManualExporter.Export(f.Plan(m),Archives(f,m),output,f.Installer,null,default)));
    Assert(!Directory.Exists(output)&&f.Installer.Receipt(f.Root) is null);
});
await Test("manual export uses verified managed originals rather than patching installed output twice",async()=>
{
    var f=Fixture();var m=Package(f,content:"MOD");var source=Paths.Safe(f.Root,"RvGame/CookedPC/manual.xxx");File.WriteAllText(source,"ORIGINAL");
    m=m with{Files=[..m.Files,new("RvGame/CookedPC/manual.xxx","",m.Files[0].Sha256,"recipe","local-transform",Paths.Hash(source),Recipe:[new("payload",0,3,"data/mod.bin")])]};
    await f.Installer.InstallAsync(f.Plan(m),Archives(f,m),null,default);
    var before=Paths.Hash(source);var receipt=File.ReadAllBytes(Path.Combine(f.Installer.StateDirectory(f.Root),"installed.json"));
    ManualExporter.Export(f.Plan(m),Archives(f,m),f.Root+"-managed-export",f.Installer,null,default);
    Assert(Paths.Hash(source)==before&&receipt.SequenceEqual(File.ReadAllBytes(Path.Combine(f.Installer.StateDirectory(f.Root),"installed.json"))));
});
await Test("Wonderlands x64 adapter, unknown hash consent, install/export/restore and DX11 launch",async()=>
{
    var f=Fixture();var bin=Paths.Safe(f.Root,"OakGame/Binaries/Win64/xinput1_3.dll");Directory.CreateDirectory(Path.GetDirectoryName(bin)!);Directory.CreateDirectory(Paths.Safe(f.Root,"OakGame/Content"));
    var exe=Paths.Safe(f.Root,"OakGame/Binaries/Win64/Wonderlands.exe");var pe=new byte[256];
    using(var stream=new MemoryStream(pe))using(var writer=new BinaryWriter(stream)){writer.Write((ushort)0x5A4D);stream.Position=60;writer.Write(128);stream.Position=128;writer.Write(0x4550);writer.Write((ushort)0x8664);stream.Position=152;writer.Write((ushort)0x20B);}File.WriteAllBytes(exe,pe);
    var initial=Package(f);var m=initial with{Files=[new("OakGame/Binaries/Win64/xinput1_3.dll","data/mod.bin",initial.Files[0].Sha256,"copy","mod-authored"),new("OakGame/Binaries/Win64/WonderlandsVR.ini","","","ini","mod-authored",IniValues:new(){{"Simulator/Enabled","0"}})]};
    var game=new GameManifest("wonderlands","Tiny Tina's Wonderlands VR","stable","WonderlandsGameAdapter","1286680","OakGame/Binaries/Win64/Wonderlands.exe",[new string('0',64)],[m]);
    var location=new WonderlandsGameAdapter().Validate(f.Root,game);Assert(!location.Compatible&&ExecutableCompatibility.CanTry(game,location));
    var plan=PlanBuilder.Build(game,location,new("en","en","en",[]),new(f.Root,location.ExeHash!));
    var receipt=await f.Installer.InstallAsync(plan,Archives(f,m),null,default);Assert(f.Installer.Verify(f.Root).Count==0);
    var start=GameLauncher.CreateStartInfo(game,location,receipt);Assert(start.Arguments=="-dx11"&&Paths.Same(start.WorkingDirectory,Path.GetDirectoryName(exe)));
    var graphicsIni=Path.Combine(Path.GetDirectoryName(exe)!,"WonderlandsVR.ini");
    File.WriteAllText(graphicsIni,"[VR]\nD3D12=1\n");
    Assert(GameLauncher.CreateStartInfo(game,location,receipt).Arguments=="-dx12");
    File.WriteAllText(graphicsIni,"[Simulator]\nEnabled=0\n");
    Assert(ProcessGuardPolicy.Refusal(f.Root,1,2,"Wonderlands",exe) is not null&&ProcessGuardPolicy.Refusal(f.Root,1,2,"Steam",null) is null);
    ManualExporter.Export(plan,Archives(f,m),f.Root+"-tiny-export",f.Installer,null,default);
    f.Installer.Uninstall(f.Root,default);Assert(!File.Exists(bin)&&File.Exists(exe));
    await Reject(()=>Sync(()=>PlanBuilder.ValidateManifest(game with{Modules=[m with{Files=[m.Files[0] with{Path="Binaries/foreign.dll"}]}]})));
    await Reject(()=>Sync(()=>PlanBuilder.ValidateManifest(f.Game(m))));
});
await Psvr2ToolkitTests.Run(Test,testRoot);
await Test("Darkness English choices override legacy Polish defaults in install and manual export",async()=>
{
    var f=Fixture();var original=Package(f,content:"[Render]\r\nSubmitMode=4\r\n[VRSettings]\r\nPolishText=1\r\nPolishVoice=1\r\nAppliedPolishText=1\r\nAppliedPolishVoice=1\r\n");
    var module=original with{Files=[new("Darkness2VR.ini","data/mod.bin",original.Files[0].Sha256,"copy","mod-authored")]};
    var plan=f.Plan(module) with{GameId="darkness2"};var archives=Archives(f,module);
    var exported=ManualExporter.Export(plan,archives,f.Root+"-dark-en",f.Installer,null,default);
    var text=new IniDocument(File.ReadAllText(Path.Combine(exported,"files","Darkness2VR.ini")));
    Assert(text.Get("VRSettings/PolishText")=="0"&&text.Get("VRSettings/PolishVoice")=="0");
    Assert(!File.Exists(Path.Combine(f.Root,"Darkness2VR.ini")));
    var receipt=await f.Installer.InstallAsync(plan,archives,null,default);
    var installed=new IniDocument(File.ReadAllText(Path.Combine(f.Root,"Darkness2VR.ini")));
    foreach(var key in new[]{"PolishText","PolishVoice","AppliedPolishText","AppliedPolishVoice","LanguageError"})Assert(installed.Get("VRSettings/"+key)=="0");
    Assert(installed.Get("Render/SubmitMode")=="4"&&f.Installer.Verify(f.Root).Count==0);
    Assert(receipt.Files.Single().InstalledHash!=module.Files[0].Sha256);
    f.Installer.Uninstall(f.Root,default);Assert(!File.Exists(Path.Combine(f.Root,"Darkness2VR.ini")));
});
await Test("Darkness language config requires matching resources and rejects tampering",async()=>
{
    var f=Fixture();var m=Package(f,content:"[VRSettings]\nPolishText=0\nPolishVoice=1\n");
    var file=new PackageFile("Darkness2VR.ini","data/mod.bin",m.Files[0].Sha256,"copy","mod-authored");
    var prepared=Path.Combine(f.Root,"prepared.ini");
    var plan=f.Plan(m) with{GameId="darkness2",Choices=new("pl","en","en",[])};
    File.WriteAllText(prepared,"[VRSettings]\nPolishText=0\nPolishVoice=1\n");
    await Reject(()=>Sync(()=>DarknessLanguageSettings.ApplyPrepared(plan,file,prepared)));
    var polish=m with{Id="polish-text",Required=false};plan=plan with{Modules=[m,polish]};
    Assert(DarknessLanguageSettings.ApplyPrepared(plan,file,prepared));
    var ini=new IniDocument(File.ReadAllText(prepared));Assert(ini.Get("VRSettings/PolishText")=="1"&&ini.Get("VRSettings/PolishVoice")=="0");
    await Reject(()=>Sync(()=>DarknessLanguageSettings.ApplyPrepared(plan,file,prepared)));
    Assert(!DarknessLanguageSettings.ApplyPrepared(plan with{GameId="singularity"},file,prepared));
});
await Test("Darkness text choice selects matching native menu resources",()=>Sync(()=>
{
    var f=Fixture();var fixture=Package(f);
    var core=fixture with{Files=[new("d3d9.dll","data/mod.bin",fixture.Files[0].Sha256,"copy","mod-authored")]};
    var english=fixture with{Id="darkness-menu",Required=false,Dependencies=["vr-core"],Files=[new("Cache.Windows/H.Misc.cache","",fixture.Files[0].Sha256,"recipe","local-transform",fixture.Files[0].Sha256,Recipe:[new("payload",0,3,"data/mod.bin")])]};
    var polish=english with{Id="polish-text"};
    var game=f.Game(core,english,polish) with{Id="darkness2",Adapter="Darkness2GameAdapter",SteamAppId="67370",Executable="DarknessII.exe"};
    var en=PlanBuilder.Build(game,f.Location(),new("en","en","en",[]));
    Assert(en.Modules.Any(m=>m.Id=="darkness-menu")&&!en.Modules.Any(m=>m.Id=="polish-text"));
    var pl=PlanBuilder.Build(game,f.Location(),new("pl","en","en",["darkness-menu"]));
    Assert(pl.Modules.Any(m=>m.Id=="polish-text")&&!pl.Modules.Any(m=>m.Id=="darkness-menu"));
}));
await DarknessHandsTests.Run(Test,testRoot);
var report=new{time=DateTimeOffset.UtcNow,passed=results.Count-failed,failed,fixtureRoot=testRoot,steamAccess="NONE",results};
Json.Write(Path.Combine(testRoot,"test-report.json"),report);Console.WriteLine(JsonSerializer.Serialize(new{passed=results.Count-failed,failed,report=Path.Combine(testRoot,"test-report.json")}));Environment.ExitCode=failed==0?0:1;

sealed class FakeGuard : IProcessGuard {public bool Busy;public void RequireIdle(string root){if(Busy)throw new IOException("Synthetic active Steam game");}}
sealed class InlineProgress(Action<ProgressInfo> report) : IProgress<ProgressInfo> {public void Report(ProgressInfo value)=>report(value);}
sealed class TestFixture
{
    public string Root{get;}public string State{get;}public FakeGuard Guard{get;}=new();public TransactionInstaller Installer{get;}
    public TestFixture(string root,string state){Root=root;State=state;Directory.CreateDirectory(Path.Combine(root,"Binaries"));Directory.CreateDirectory(Path.Combine(root,"RvGame","CookedPC"));var exe=Path.Combine(root,"Binaries","Singularity.exe");if(!File.Exists(exe))File.WriteAllText(exe,"SYNTHETIC OWNED GAME FIXTURE â€” NOT A GAME EXECUTABLE");Installer=new(state,Guard);}
    public GameLocation Location()=>new(Root,"Fixture",true,"Synthetic",Paths.Hash(Path.Combine(Root,"Binaries","Singularity.exe")));
    public GameManifest Game(params ModuleManifest[] modules)=>new("singularity","Synthetic Singularity","stable","SingularityGameAdapter","42670","Binaries/Singularity.exe",[Location().ExeHash!],[..modules]);
    public InstallPlan Plan(params ModuleManifest[] modules)=>new("singularity",modules[0].Version,Root,Location().ExeHash!,new("en","en","en",modules.Select(x=>x.Id).ToList()),[..modules]);
}
sealed class FakeHttp(byte[] bytes) : HttpMessageHandler
{
    public int Requests;public long? LastRange;public string? Redirect;public bool FailFirst;public bool DnsFailure;public string? LastUserAgent,LastReferrer;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();LastUserAgent=request.Headers.UserAgent.ToString();LastReferrer=request.Headers.Referrer?.AbsoluteUri;Requests++;if(DnsFailure)throw new HttpRequestException("No such host is known. (www.gildor.org:443)");if(FailFirst&&Requests==1)return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));if(Redirect is not null&&Requests==1){var r=new HttpResponseMessage(HttpStatusCode.Redirect);r.Headers.Location=new(Redirect);return Task.FromResult(r);}
        LastRange=request.Headers.Range?.Ranges.Single().From;var offset=(int)(LastRange??0);var response=new HttpResponseMessage(offset>0?HttpStatusCode.PartialContent:HttpStatusCode.OK){Content=new ByteArrayContent(bytes[offset..])};if(offset>0)response.Content.Headers.ContentRange=new(offset,bytes.Length-1,bytes.Length);return Task.FromResult(response);
    }
}

