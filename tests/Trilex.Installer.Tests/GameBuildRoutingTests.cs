using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Trilex.Installer.Core;

internal static class GameBuildRoutingTests
{
    public static async Task Run(Func<string,Func<Task>,Task> test,string testRoot)
    {
        const string exe="OakGame/Binaries/Win64/Wonderlands.exe",dll="OakGame/Binaries/Win64/xinput1_3.dll";
        void Check(bool ok){if(!ok)throw new Exception("Build routing assertion failed");}
        string Hash(byte[] b)=>Convert.ToHexString(SHA256.HashData(b));
        byte[] Image(byte tag)
        {
            var bytes=new byte[1024];using var s=new MemoryStream(bytes);using var w=new BinaryWriter(s);
            w.Write((ushort)0x5a4d);s.Position=0x3c;w.Write(0x80);
            s.Position=0x80;w.Write(0x4550);w.Write((ushort)0x8664);w.Write((ushort)1);
            s.Position=0x94;w.Write((ushort)0xf0);w.Write((ushort)0x22);w.Write((ushort)0x20b);
            s.Position=0x98+56;w.Write(0x2000);w.Write(0x200);
            s.Position=0x188;w.Write(Encoding.ASCII.GetBytes(".text\0\0\0"));w.Write(0x200);w.Write(0x1000);w.Write(0x200);w.Write(0x200);
            s.Position=0x188+36;w.Write(0x60000020);
            for(var i=0x200;i<0x400;i++)bytes[i]=(byte)(tag+i%16);
            return bytes;
        }
        List<ExecutableProbe> Probes(byte[] b)=>Enumerable.Range(0,3).Select(i=>new ExecutableProbe(0x1000+i*64,16,Hash(b[(0x200+i*64)..(0x210+i*64)]))).ToList();
        ModuleManifest Payload(string root,string name)
        {
            var data=Encoding.UTF8.GetBytes(name);var archive=Path.Combine(root,name+".zip");
            using(var zip=ZipFile.Open(archive,ZipArchiveMode.Create)){using var output=zip.CreateEntry("mod.dll").Open();output.Write(data);}
            return new("vr-core",new(){{"en",name}},name,"stable",true,[],new FileInfo(archive).Length,
                "https://github.com/trilex07/TrilexModInstaller/releases/download/test/"+name+".zip",Paths.Hash(archive),1024,true,"Authored synthetic routing test",[new(dll,"mod.dll",Hash(data),"copy","mod-authored")],"");
        }
        (string Root,string Exe,GameManifest Game,ModuleManifest Steam,ModuleManifest Epic) Fixture()
        {
            var root=Path.Combine(testRoot,"routing-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(root,"OakGame/Content"));
            var path=Paths.Safe(root,exe);Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllBytes(path,Image(20));
            var steam=Payload(root,"steam");var epic=Payload(root,"epic");
            var game=new GameManifest("wonderlands","VR","preview","WonderlandsGameAdapter","1286680",exe,[Paths.Hash(path)],[steam],
                [new("epic",Probes(Image(40)),[epic])]);
            return(root,path,game,steam,epic);
        }
        await test("signed build profile routes full package and transaction restore without touching EXE",async()=>
        {
            var f=Fixture();File.WriteAllBytes(f.Exe,Image(40));
            var game=f.Game with{ExeSha256=[Paths.Hash(f.Exe)]};
            var location=new WonderlandsGameAdapter().Validate(f.Root,game);
            var plan=PlanBuilder.Build(game,location,new("en","en","en",[]));
            Check(plan.Modules.Single().ArchiveSha256==f.Epic.ArchiveSha256&&plan.Version=="epic");
            var installer=new TransactionInstaller(f.Root+"-state",new FakeGuard());
            await installer.InstallAsync(plan,new(){{"vr-core",Path.Combine(f.Root,"epic.zip")}},null,default);
            Check(File.ReadAllText(Paths.Safe(f.Root,dll))=="epic"&&Paths.Hash(f.Exe)==location.ExeHash);
            installer.Uninstall(f.Root,default);Check(!File.Exists(Paths.Safe(f.Root,dll))&&Paths.Hash(f.Exe)==location.ExeHash);
            var steamLocation=new WonderlandsGameAdapter().Validate(f.Root,f.Game);
            File.WriteAllBytes(f.Exe,Image(20));steamLocation=new WonderlandsGameAdapter().Validate(f.Root,f.Game);
            Check(PlanBuilder.Build(f.Game,steamLocation,new("en","en","en",[])).Version=="steam");
        });
        await test("unknown whole EXE hash preserves consent and selects by code probes",()=>
        {
            var f=Fixture();var b=Image(40);b[0x50]=123;File.WriteAllBytes(f.Exe,b);
            var location=new WonderlandsGameAdapter().Validate(f.Root,f.Game);Check(!location.Compatible);
            var plan=PlanBuilder.Build(f.Game,location,new("en","en","en",[]),new(f.Root,location.ExeHash!));
            Check(plan.Version=="epic"&&plan.ExecutableConsent is not null);
            return Task.CompletedTask;
        });
        await test("mixed and ambiguous profiles refuse instead of selecting wrong addresses",()=>
        {
            var f=Fixture();var b=Image(40);b[0x240]++;
            File.WriteAllBytes(f.Exe,b);var location=new WonderlandsGameAdapter().Validate(f.Root,f.Game);
            void Refuse(Action action){try{action();}catch(InvalidDataException){return;}throw new Exception("Expected profile refusal");}
            Refuse(()=>GameBuildRouting.Select(f.Game,location));
            File.WriteAllBytes(f.Exe,Image(40));location=new WonderlandsGameAdapter().Validate(f.Root,f.Game);
            var duplicate=f.Game.BuildProfiles![0] with{Id="duplicate"};
            Refuse(()=>GameBuildRouting.Select(f.Game with{BuildProfiles=[f.Game.BuildProfiles[0],duplicate]},location));
            Refuse(()=>PlanBuilder.ValidateManifest(f.Game with{BuildProfiles=[duplicate with{Probes=[new(1,300,new string('0',64))]}]}));
            return Task.CompletedTask;
        });
        await test("alternate profile never bypasses signed package file hashes",()=>
        {
            var f=Fixture();File.WriteAllBytes(f.Exe,Image(40));var location=new WonderlandsGameAdapter().Validate(f.Root,f.Game);
            var module=GameBuildRouting.Select(f.Game,location).Modules.Single();
            var stage=f.Root+"-bad";Directory.CreateDirectory(stage);
            try{PackageExtractor.Extract(Path.Combine(f.Root,"steam.zip"),stage,module);}
            catch(InvalidDataException){return Task.CompletedTask;}
            throw new Exception("Wrong build archive accepted");
        });
        await test("manual Epic selection cannot override known Steam code and routes unknown consented builds",()=>
        {
            var f=Fixture();
            var steamProfile=new GameBuildProfile("steam",Probes(Image(20)),[f.Steam]);
            var game=f.Game with{BuildProfiles=[steamProfile,f.Game.BuildProfiles![0]]};
            var location=new WonderlandsGameAdapter().Validate(f.Root,game);
            var refused=false;
            try{GameBuildRouting.Select(game,location,"epic");}catch(InvalidDataException){refused=true;}
            Check(refused);
            File.WriteAllBytes(f.Exe,Image(60));location=new WonderlandsGameAdapter().Validate(f.Root,game);
            var plan=PlanBuilder.Build(game,location,new("en","en","en",[]),new(f.Root,location.ExeHash!),"epic");
            Check(plan.Version=="epic"&&plan.Modules.Single().ArchiveSha256==f.Epic.ArchiveSha256);
            File.WriteAllBytes(f.Exe,Image(40));location=new WonderlandsGameAdapter().Validate(f.Root,game);
            Check(GameBuildRouting.DetectProfile(game,location)?.Id=="epic");
            return Task.CompletedTask;
        });
        await test("offline Wonderlands bundle selects profile hash and retains NVIDIA target bounds",()=>
        {
            var f=Fixture();var dir=Path.Combine(f.Root,"bundle");Directory.CreateDirectory(Path.Combine(dir,"packages/wonderlands"));
            var epic=Path.Combine(dir,"packages/wonderlands",f.Epic.ArchiveSha256!.ToUpperInvariant()+".zip");
            File.Copy(Path.Combine(f.Root,"epic.zip"),epic);
            File.Copy(Path.Combine(f.Root,"steam.zip"),Path.Combine(dir,"packages/wonderlands/vr-core.zip"));
            Check(BundleSource.ArchiveAt(dir,f.Epic,"wonderlands")==Path.GetFullPath(epic));
            foreach(var name in new[]{"nvngx_dlss.dll","nvngx_dlssg.dll","licenses/NVIDIA/LICENSE.txt","licenses/NVIDIA/THIRD-PARTY-NOTICES.txt"})
                Check(Paths.WonderlandsTarget("OakGame/Binaries/Win64/"+name));
            Check(!Paths.WonderlandsTarget("OakGame/Binaries/Win64/_nvngx.dll")&&!Paths.WonderlandsTarget("OakGame/Binaries/Win64/nvngx_dlssd.dll"));
            return Task.CompletedTask;
        });
        await test("Epic manifest discovery checks actual game layout and preserves unknown hash consent",()=>
        {
            var f=Fixture();var manifests=f.Root+"-manifests";Directory.CreateDirectory(manifests);
            void Write(string name,object value)=>File.WriteAllText(Path.Combine(manifests,name+".item"),System.Text.Json.JsonSerializer.Serialize(value));
            Write("real",new{InstallLocation=f.Root,LaunchExecutable="DO-NOT-EXECUTE.exe"});
            Write("duplicate",new{InstallLocation=f.Root});Write("relative",new{InstallLocation="relative/path"});
            Write("foreign",new{InstallLocation=f.Root+"-other"});Write("wrong-type",new{InstallLocation=42});
            File.WriteAllText(Path.Combine(manifests,"malformed.item"),"{");
            File.WriteAllBytes(Path.Combine(manifests,"oversized.item"),new byte[256*1024+1]);
            var adapter=new WonderlandsGameAdapter(manifests);
            var found=adapter.Detect([],f.Game).ToList();Check(found.Count==1&&found[0].Compatible&&found[0].Source=="Epic Games manifest");
            Check(adapter.Detect([],f.Game with{ExeSha256=[new string('0',64)]}).Single().Compatible==false);
            Check(!new WonderlandsGameAdapter(manifests+"-missing").Detect([],f.Game).Any());
            return Task.CompletedTask;
        });
        await test("Borderlands 3 selects by store code and refuses unmatched or conflicting layouts",()=>
        {
            var f=Fixture();
            var path=Paths.Safe(f.Root,Borderlands3Runtime.Executable);
            var steam=Image(20);
            var epic=Image(40);
            var game=new GameManifest(Borderlands3Runtime.GameId,"Borderlands 3 VR","stable",
                nameof(Borderlands3GameAdapter),"397540",Borderlands3Runtime.Executable,[Hash(steam),Hash(epic)],[f.Steam],
                [new("steam",Probes(steam),[f.Steam]),new("epic",Probes(epic),[f.Epic])]);
            var adapter=new Borderlands3GameAdapter();
            void Refuse(Action action){try{action();}catch(InvalidDataException){return;}throw new Exception("Expected profile refusal");}

            File.WriteAllBytes(path,steam);
            var location=adapter.Validate(f.Root,game);
            Check(location.Compatible&&GameBuildRouting.Select(game,location).Modules.Single()==f.Steam);
            Refuse(()=>GameBuildRouting.Select(game,location,"epic"));

            epic[0x50]=123; // Headers differ. The relevant signed code still matches.
            File.WriteAllBytes(path,epic);
            location=adapter.Validate(f.Root,game);
            Check(location.Compatible&&GameBuildRouting.Select(game,location).Modules.Single()==f.Epic);
            Refuse(()=>GameBuildRouting.Select(game,location,"steam"));

            File.WriteAllBytes(path,Image(60));
            location=adapter.Validate(f.Root,game);
            Check(!location.Compatible);
            Refuse(()=>GameBuildRouting.Select(game,location,"epic"));
            return Task.CompletedTask;
        });
    }
}
