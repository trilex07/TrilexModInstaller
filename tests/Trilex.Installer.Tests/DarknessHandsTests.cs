using Trilex.Installer.Core;

internal static class DarknessHandsTests
{
    public static async Task Run(Func<string,Func<Task>,Task> test,string testRoot)
    {
        await test("Darkness hands targets require scoped local reconstruction ownership",()=>
        {
            var hash=new string('a',64);
            var file=new PackageFile("Cache.Windows/B.Misc.cache","",hash,"recipe","local-transform",hash,Recipe:[new("original",0,1)]);
            var module=new ModuleManifest("hands-only",new(){{"en","Hands"}},"test","planned",true,[],0,null,null,0,false,"",[file],"");
            var game=new GameManifest("darkness2","Darkness","preview","Darkness2GameAdapter","67370","DarknessII.exe",[hash],[module]);
            PlanBuilder.ValidateManifest(game);
            foreach(var invalid in new[]{game with{Id="singularity"},game with{Modules=[module with{Id="vr-core"}]},game with{Modules=[module with{Files=[file with{Kind="copy"}]}]}})
            {
                var refused=false;try{PlanBuilder.ValidateManifest(invalid);}catch(InvalidDataException){refused=true;}
                if(!refused)throw new Exception("Unscoped hand resource accepted");
            }
            return Task.CompletedTask;
        });
        if(Environment.GetEnvironmentVariable("TRILEX_DARKNESS_HAND_FIXTURE") is not string fixture)return;
        await test("Darkness real hand reconstruction: install, repair, export, rollback, uninstall",async()=>
        {
            void Check(bool b){if(!b)throw new Exception("Hands lifecycle assertion failed");}
            var root=Path.Combine(testRoot,"hands-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(root,"Cache.Windows"));
            foreach(var name in new[]{"H.Misc.cache","H.Misc.toc","B.Misc.cache","B.Misc.toc"})File.Copy(Path.Combine(fixture,"Cache.Windows",name),Path.Combine(root,"Cache.Windows",name));
            File.Copy(Path.Combine(fixture,"DarknessII.exe"),Path.Combine(root,"DarknessII.exe"));
            var before=new[]{"B.Misc.cache","B.Misc.toc"}.ToDictionary(n=>"Cache.Windows/"+n,n=>Paths.Hash(Path.Combine(root,"Cache.Windows",n)));
            var installer=new TransactionInstaller(root+"-state",new FakeGuard());
            var work=root+"-work";
            var prepared=await DarknessHandsInstall.PrepareAsync(root,installer,work,default);
            var plan=new InstallPlan("darkness2","test-hands",root,Paths.Hash(Path.Combine(root,"DarknessII.exe")),new("en","en","en",["hands-only"]),[prepared.Module],"DarknessII.exe");
            var packages=new Dictionary<string,string>{{prepared.Module.Id,prepared.Archive}};
            await installer.InstallAsync(plan,packages,null,default);Check(installer.Verify(root).Count==0);
            Check(new FileInfo(Path.Combine(root,"Cache.Windows/B.Misc.cache")).Length==new FileInfo(Path.Combine(fixture,"Cache.Windows/B.Misc.cache")).Length+206112);
            var installed=prepared.Module.Files.ToDictionary(f=>f.Path,f=>Paths.Hash(Paths.Safe(root,f.Path)));
            // Regeneration after install must read verified originals, not patch the output twice.
            var again=await DarknessHandsInstall.PrepareAsync(root,installer,root+"-again",default);
            Check(again.Module.Files.All(f=>Paths.Same(f.Sha256,installed[f.Path])));
            var target=Path.Combine(root,"Cache.Windows/B.Misc.cache");using(var file=new FileStream(target,FileMode.Open,FileAccess.Write)){file.Position=file.Length-1;file.WriteByte(0xff);}
            await installer.RepairAsync(plan,packages,null,default);Check(installer.Verify(root).Count==0);
            var exported=ManualExporter.Export(plan,packages,root+"-export",installer,null,default);
            foreach(var pair in installed)Check(Paths.Hash(Paths.Safe(exported,"files/"+pair.Key))==pair.Value);
            installer.Uninstall(root,default);foreach(var pair in before)Check(Paths.Hash(Paths.Safe(root,pair.Key))==pair.Value);
            var failed=false;
            try{await installer.InstallAsync(plan,packages,null,default,n=>{if(n==1)throw new IOException("Injected hand transaction failure");});}catch(IOException){failed=true;}
            Check(failed);foreach(var pair in before)Check(Paths.Hash(Paths.Safe(root,pair.Key))==pair.Value);
            // A clean input remains byte-identical after preparation and manual export.
            foreach(var name in new[]{"H.Misc.cache","H.Misc.toc"})Check(Paths.Hash(Path.Combine(root,"Cache.Windows",name))==Paths.Hash(Path.Combine(fixture,"Cache.Windows",name)));
        });
    }
}
