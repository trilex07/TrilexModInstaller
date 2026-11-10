using Trilex.Installer.Core;
if(args.Length is not (3 or 4)||args.Length==4&&args[3]!="prepare-only")throw new ArgumentException("Usage: FRESH_ARTIFACT_DIRECTORY ORIGINAL_TEXTURE_FOLDER PNG_ZIP [prepare-only]");
var root=Path.GetFullPath(args[0]);
if(!root.StartsWith(Path.GetFullPath("artifacts")+Path.DirectorySeparatorChar)||Directory.Exists(root))throw new ArgumentException("Fresh artifacts directory required");
Directory.CreateDirectory(root);
var originals=Path.GetFullPath(args[1]);
var game=Path.Combine(root,"game");Directory.CreateDirectory(Path.Combine(game,"Cache.Windows"));
foreach(var path in Directory.GetFiles(originals).Where(p=>new[]{"H.Texture.cache","H.Texture.toc","B.Texture.cache","B.Texture.toc","F.Texture.cache","F.Texture.toc"}.Contains(Path.GetFileName(p))))
    File.Copy(path,Path.Combine(game,"Cache.Windows",Path.GetFileName(path)),true);
File.WriteAllText(Path.Combine(game,"DarknessII.exe"),"offline synthetic EXE");
var before=Directory.GetFiles(Path.Combine(game,"Cache.Windows")).ToDictionary(p=>p,Paths.Hash);
var installer=new TransactionInstaller(Path.Combine(root,"state"),new Idle());
var work=Path.Combine(root,"integration-work");Directory.CreateDirectory(work);
var conversions=new List<long>();
var progress=new InlineProgress(p=>{if(p.Stage=="TextureConvert"){conversions.Add(p.Completed);Console.WriteLine($"PROGRESS {p.Completed}/{p.Total}");}});
var prepared=await DarknessTextureInstall.PrepareAsync(game,installer,work,
    Path.GetFullPath(args[2]),default,progress);
Console.WriteLine("PASS embedded worker created six texture targets");
if(!conversions.SequenceEqual(Enumerable.Range(1,64).Select(x=>(long)x)))throw new Exception("Embedded worker progress was not drained in real time");
if(args.Length==4)
{
    if(before.Any(p=>Paths.Hash(p.Key)!=p.Value))throw new Exception("Preparation changed fixture originals");
    Json.Write(Path.Combine(root,"prepare-verification.json"),new{status="PASS",progressEvents=conversions.Count,targets=prepared.Module.Files.Count,gameInputs="UNCHANGED",scope="real embedded worker preparation only; no live game or install"});
    return;
}
var plan=new InstallPlan("darkness2","offline-hd",game,Paths.Hash(Path.Combine(game,"DarknessII.exe")),
    new("en","en","en",["hd-textures"]),[prepared.Module],"DarknessII.exe");
await installer.InstallAsync(plan,new(){{prepared.Module.Id,prepared.Archive}},null,default);
if(installer.Verify(game).Count!=0)throw new Exception("Installed verification failed");
Console.WriteLine("PASS install and verify all six cache files, including >2GB F.Texture");
await installer.InstallAsync(plan with{Modules=[],Choices=plan.Choices with{SelectedModules=[]}},new(),null,default);
if(before.Any(p=>Paths.Hash(p.Key)!=p.Value))throw new Exception("Deselect restoration failed");
Console.WriteLine("PASS deselect restores original texture caches");
installer.Uninstall(game,default);
if(before.Any(p=>Paths.Hash(p.Key)!=p.Value))throw new Exception("Original restoration failed");
Console.WriteLine("PASS byte-exact uninstall restores all six original files");
sealed class Idle:IProcessGuard{public void RequireIdle(string root){}}
sealed class InlineProgress(Action<ProgressInfo> report):IProgress<ProgressInfo>{public void Report(ProgressInfo p)=>report(p);}
