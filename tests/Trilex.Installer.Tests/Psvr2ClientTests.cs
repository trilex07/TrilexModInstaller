using System.Text;
using Trilex.Installer.Core;

internal static class Psvr2ClientTests
{
    public static async Task Run(Func<string,Func<Task>,Task> test,string testRoot)
    {
        string Fresh()=>Path.Combine(testRoot,"clients-Żółć-"+Guid.NewGuid().ToString("N"));
        void Check(bool ok){if(!ok)throw new Exception("PSVR2 client assertion failed");}
        Dictionary<string,byte[]> Payload()=>Psvr2ClientRuntime.Files.ToDictionary(n=>n,n=>Encoding.UTF8.GetBytes("verified fixture "+n));
        await test("PSVR2 clients fresh user, Unicode paths, duplicate loader and idempotence",()=>
        {
            var root=Fresh();var data=Payload();Psvr2ClientRuntime.Install(root,data,()=>{});Psvr2ClientRuntime.Install(root,data,()=>{});
            foreach(var (name,bytes) in data)Check(File.ReadAllBytes(Paths.Safe(root,name)).SequenceEqual(bytes));
            Check(Directory.GetDirectories(Paths.Safe(root,"Trilex/PSVR2Toolkit"),"clients-backup-*").Length==1);
            return Task.CompletedTask;
        });
        await test("PSVR2 clients refuse active game and arbitrary payload targets",()=>
        {
            var root=Fresh();var rejected=false;
            try{Psvr2ClientRuntime.Install(root,Payload(),()=>throw new IOException("game active"));}catch(IOException){rejected=true;}
            Check(rejected&&!Directory.Exists(root));var data=Payload();data["unrelated.exe"]=[];rejected=false;
            try{Psvr2ClientRuntime.Install(root,data,()=>{});}catch(InvalidDataException){rejected=true;}
            Check(rejected&&!Directory.Exists(root));return Task.CompletedTask;
        });
        await test("PSVR2 clients retain originals and roll back a late locked-file failure",()=>
        {
            var root=Fresh();var data=Payload();
            foreach(var name in Psvr2ClientRuntime.Files)Paths.AtomicBytes(Paths.Safe(root,name),Encoding.UTF8.GetBytes("original "+name));
            var old=Psvr2ClientRuntime.Files.ToDictionary(n=>n,n=>Paths.Hash(Paths.Safe(root,n)));
            var rejected=false;
            using(var locked=new FileStream(Paths.Safe(root,Psvr2ClientRuntime.DarknessBridge),FileMode.Open,FileAccess.Read,FileShare.Read))
                try{Psvr2ClientRuntime.Install(root,data,()=>{});}catch(Exception e)when(e is IOException or UnauthorizedAccessException){rejected=true;}
            Check(rejected);foreach(var name in old.Keys)Check(Paths.Hash(Paths.Safe(root,name))==old[name]);
            Check(!File.Exists(Paths.Safe(root,"Trilex/PSVR2Toolkit/clients-pending.json")));
            Psvr2ClientRuntime.Install(root,data,()=>{});return Task.CompletedTask;
        });
        await test("PSVR2 clients refuse recovery over an external file change",()=>
        {
            var root=Fresh();Psvr2ClientRuntime.Install(root,Payload(),()=>{});
            var state=Paths.Safe(root,"Trilex/PSVR2Toolkit");var expected=Psvr2ClientRuntime.Files.ToDictionary(n=>n,n=>Paths.Hash(Paths.Safe(root,n)));
            var backup="clients-backup-"+Guid.NewGuid().ToString("N");
            Json.Write(Paths.Safe(state,"clients-pending.json"),new ToolkitJournal(backup,Psvr2ClientRuntime.Files.ToDictionary(n=>n,n=>(string?)null),expected));
            Paths.AtomicBytes(Paths.Safe(root,Psvr2ClientRuntime.Loader),Encoding.UTF8.GetBytes("external update"));
            var rejected=false;try{Psvr2ClientRuntime.Install(root,Payload(),()=>{});}catch(IOException){rejected=true;}
            Check(rejected&&File.ReadAllText(Paths.Safe(root,Psvr2ClientRuntime.Loader))=="external update");return Task.CompletedTask;
        });
        await test("Wonderlands loader binding is user-local and leaves other game manifests intact",()=>
        {
            var root=Fresh();var ini=new PackageFile("OakGame/Binaries/Win64/WonderlandsVR.ini","","","ini","mod-authored",IniValues:new(){["VR/D3D12"]="1"});
            var module=new ModuleManifest("vr-core",new(),"test","stable",true,[],0,null,null,0,true,"test",[ini],"");
            var bound=Psvr2ClientRuntime.Configure("wonderlands",[module],root)[0].Files[0];
            Check(bound.IniValues!["Haptics/ToolkitLoader"]==Psvr2ClientRuntime.LoaderPath(root));
            Check(bound.IniValues["VR/D3D12"]=="1"&&!ini.IniValues!.ContainsKey("Haptics/ToolkitLoader"));
            Check(Psvr2ClientRuntime.Configure("singularity",[module],root)[0]==module);
            Check(Psvr2ClientRuntime.Configure("darkness2",[module],root)[0]==module);return Task.CompletedTask;
        });
    }
}
