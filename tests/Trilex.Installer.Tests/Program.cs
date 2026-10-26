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
await Test("manual location and executable mismatch",()=>Sync(()=>{var f=Fixture();var game=f.Game();var adapter=new SingularityGameAdapter();Assert(adapter.Validate(f.Root,game).Compatible);File.AppendAllText(Path.Combine(f.Root,"Binaries","Singularity.exe"),"different build");Assert(!adapter.Validate(f.Root,game).Compatible);Assert(!adapter.Validate(Path.Combine(f.Root,"missing"),game).Compatible);}));
await Test("Darkness II remains unavailable",()=>Sync(()=>{var f=Fixture();Assert(!new Darkness2GameAdapter().Validate(f.Root,f.Game()).Compatible);}));
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
    var f=Fixture();var m=Package(f);var target=Path.Combine(f.Root,"Binaries","trilex-test.dll");File.WriteAllText(target,"OTHER-MOD");await Reject(()=>f.Installer.InstallAsync(f.Plan(m),Archives(f,m),null,default));Assert(File.ReadAllText(target)=="OTHER-MOD");File.Delete(target);await f.Installer.InstallAsync(f.Plan(m),Archives(f,m),null,default);File.WriteAllText(target,"USER-EDIT");await Reject(()=>Sync(()=>f.Installer.Uninstall(f.Root,default)));Assert(File.ReadAllText(target)=="USER-EDIT");
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
    using var child=System.Diagnostics.Process.Start(start)!;await child.WaitForExitAsync();Assert(child.ExitCode==86);Assert(File.Exists(Path.Combine(f.Root,"Binaries","trilex-test.dll")));f.Installer.Recover(f.Root);Assert(!File.Exists(Path.Combine(f.Root,"Binaries","trilex-test.dll")));Assert(f.Installer.Receipt(f.Root) is null);
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
    var f=Fixture();var m=Package(f);var ini=Path.Combine(f.Root,"Binaries/SingularityVR.ini");File.WriteAllText(ini,"; ORIGINAL\r\n[User]\r\nKeep=żółć\r\n",Encoding.Unicode);var before=Paths.Hash(ini);
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
    var f=Fixture();var source=Paths.Safe(f.Root,"RvGame/CookedPC/Textures_P0.tfc");
    byte[] block=[0,248,31,0,0,0,0,0];
    using(var data=new MemoryStream()){using(var w=new BinaryWriter(data,System.Text.Encoding.UTF8,true)){w.Write(new byte[16]);w.Write(0x9e2a83c1u);w.Write(131072u);w.Write(8u);w.Write(8u);w.Write(8u);w.Write(8u);w.Write(block);}File.WriteAllBytes(source,data.ToArray());}
    var stage=Path.Combine(f.Root,"image-stage");Directory.CreateDirectory(stage);Json.Write(Path.Combine(stage,"image.json"),new ImageOperation(4,4,"svrt",1,"DXT1"));
    byte[] expected;using(var m=new MemoryStream()){using(var w=new BinaryWriter(m,System.Text.Encoding.UTF8,true)){w.Write("SVRT"u8.ToArray());w.Write(1);w.Write(4);w.Write(4);for(var i=0;i<16;i++)w.Write(new byte[]{0,0,255,255});}expected=m.ToArray();}
    var targetHash=Convert.ToHexString(SHA256.HashData(expected));
    var file=new PackageFile("Binaries/vr-assets/test/texture.svrt","",targetHash,"recipe","local-transform",Recipe:[new("image",0,expected.Length,"image.json")],SourcePath:"RvGame/CookedPC/Textures_P0.tfc",SourceSha256:Paths.Hash(source),SourceCodec:"ue3-bulk-lzo",DecodedSha256:Convert.ToHexString(SHA256.HashData(block)),DecodedBytes:8,SourceOffset:16,SourceBytes:32);
    Assert(PackageExtractor.Reconstruct(source,stage,file).SequenceEqual(expected));
    var deep=Path.Combine(stage,new string('a',75),new string('b',75),new string('c',75));Directory.CreateDirectory(deep);
    Json.Write(Path.Combine(deep,"image.json"),new ImageOperation(4,4,"svrt",1,"DXT1"));
    Assert(PackageExtractor.Reconstruct(source,deep,file).SequenceEqual(expected),"Full transaction long-path image failed");
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
var report=new{time=DateTimeOffset.UtcNow,passed=results.Count-failed,failed,fixtureRoot=testRoot,steamAccess="NONE",results};
Json.Write(Path.Combine(testRoot,"test-report.json"),report);Console.WriteLine(JsonSerializer.Serialize(new{passed=results.Count-failed,failed,report=Path.Combine(testRoot,"test-report.json")}));Environment.ExitCode=failed==0?0:1;

sealed class FakeGuard : IProcessGuard {public bool Busy;public void RequireIdle(string root){if(Busy)throw new IOException("Synthetic active Steam game");}}
sealed class TestFixture
{
    public string Root{get;}public string State{get;}public FakeGuard Guard{get;}=new();public TransactionInstaller Installer{get;}
    public TestFixture(string root,string state){Root=root;State=state;Directory.CreateDirectory(Path.Combine(root,"Binaries"));Directory.CreateDirectory(Path.Combine(root,"RvGame","CookedPC"));var exe=Path.Combine(root,"Binaries","Singularity.exe");if(!File.Exists(exe))File.WriteAllText(exe,"SYNTHETIC OWNED GAME FIXTURE — NOT A GAME EXECUTABLE");Installer=new(state,Guard);}
    public GameLocation Location()=>new(Root,"Fixture",true,"Synthetic",Paths.Hash(Path.Combine(Root,"Binaries","Singularity.exe")));
    public GameManifest Game(params ModuleManifest[] modules)=>new("singularity","Synthetic Singularity","stable","SingularityGameAdapter","42670","Binaries/Singularity.exe",[Location().ExeHash!],[..modules]);
    public InstallPlan Plan(params ModuleManifest[] modules)=>new("singularity",modules[0].Version,Root,Location().ExeHash!,new("en","en","en",modules.Select(x=>x.Id).ToList()),[..modules]);
}
sealed class FakeHttp(byte[] bytes) : HttpMessageHandler
{
    public int Requests;public long? LastRange;public string? Redirect;public bool FailFirst;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();Requests++;if(FailFirst&&Requests==1)return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));if(Redirect is not null&&Requests==1){var r=new HttpResponseMessage(HttpStatusCode.Redirect);r.Headers.Location=new(Redirect);return Task.FromResult(r);}
        LastRange=request.Headers.Range?.Ranges.Single().From;var offset=(int)(LastRange??0);var response=new HttpResponseMessage(offset>0?HttpStatusCode.PartialContent:HttpStatusCode.OK){Content=new ByteArrayContent(bytes[offset..])};if(offset>0)response.Content.Headers.ContentRange=new(offset,bytes.Length-1,bytes.Length);return Task.FromResult(response);
    }
}

