using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using Trilex.Installer.Core;

namespace Trilex.Installer.Desktop;
public sealed record LanguageOption(string Id,string Name);
public sealed class ModuleViewModel(ModuleManifest manifest,Localization l) : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public ModuleManifest Manifest=>manifest;
    private bool selected=manifest.Required;
    public bool Selected {get=>selected;set{if(selected==value)return;selected=value;PropertyChanged?.Invoke(this,new(nameof(Selected)));}}
    public bool Selectable=>manifest.State=="stable"&&!manifest.Required;
    public string Name=>manifest.Label(l.Language);
    public string Badge=>manifest.State!="stable"?l["Unavailable"]:manifest.Required?l["Required"]:l["Ready"];
    public string Detail=>manifest.State=="stable"?$"v{manifest.Version} · {manifest.DownloadBytes/1048576.0:F1} MB · {string.Join(", ",manifest.Dependencies)}":manifest.ReasonFor(l.Language);
}
public sealed class InstallerViewModel : INotifyPropertyChanged, IDisposable
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName]string? name=null)=>PropertyChanged?.Invoke(this,new(name));
    public Localization L{get;}=new();
    public List<LanguageOption> Languages{get;}=[new("en","English"),new("pl","Polski")];
    public ObservableCollection<ModuleViewModel> Modules{get;}=[];
    public string Home{get;}
    private readonly HttpClient http=CatalogService.CreateHttpClient();
    private readonly CatalogService catalogs;
    private readonly TransactionInstaller installer;
    private IGameAdapter adapter=>IsSingularity ? new SingularityGameAdapter() : new Darkness2GameAdapter();
    private Catalog catalog;
    private string gameId="singularity";
    private GameManifest Game=>catalog.Games.Single(x=>x.Id==gameId);
    public bool IsSingularity=>gameId=="singularity";
    private bool hdTextures;
    public bool HdTextures {get=>hdTextures;set{if(Busy||hdTextures==value)return;hdTextures=value;Invalidate();Changed();}}
    public Visibility DarknessOptions=>IsSingularity?Visibility.Collapsed:Visibility.Visible;
    public Visibility SingularityOptions=>IsSingularity?Visibility.Visible:Visibility.Collapsed;
    public void SelectGame(string id)
    {
        if(Busy||id==gameId||!catalog.Games.Any(x=>x.Id==id))return;
        gameId=id;HdTextures=false; GameRoot=""; DetectionStatus=Game.State=="coming_soon"?L["Blocked"]:""; Modules.Clear();
        RefreshModules(); Changed(nameof(SingularityOptions));Changed(nameof(DarknessOptions));Changed(nameof(LaunchLabel));
    }
    private GameLocation? location;
    private InstallPlan? plan;
    private CancellationTokenSource? cancellation;
    private string ui="en",text="en",voice="en",root="",activity="",detection="",review="",trust="";
    private bool busy,offline,showReview;
    private double percent;
    public string UiLanguage{get=>ui;set{if(Busy||value==ui||value is not("en"or"pl"))return;ui=value;L.Set(value);RefreshModules();Changed();Changed(nameof(LaunchLabel));Activity=L["Initial"];SavePreferences();}}
    public string TextLanguage{get=>text;set{if(value is not("en"or"pl"))return;text=value;SyncLanguageModule("polish-text",value=="pl");Invalidate();Changed();SavePreferences();}}
    public string VoiceLanguage{get=>voice;set{if(value is not("en"or"pl"))return;voice=value;SyncLanguageModule("polish-dubbing",value=="pl");Invalidate();Changed();SavePreferences();}}
    public string GameRoot{get=>root;set{
        if(!Paths.Same(root,value)&&!IsSingularity){
            try{hdTextures=!string.IsNullOrWhiteSpace(value)&&installer.Receipt(value)?.Choices.SelectedModules.Contains("hd-textures")==true;}
            catch(Exception e)when(e is IOException or ArgumentException or InvalidDataException or UnauthorizedAccessException or JsonException){hdTextures=false;}
            Changed(nameof(HdTextures));
        }
        root=value;location=null;Invalidate();Changed();Changed(nameof(CanManage));Changed(nameof(CanLaunch));}}
    public string Activity{get=>activity;private set{activity=value;Changed();}}
    public string DetectionStatus{get=>detection;private set{detection=value;Changed();}}
    public string ReviewText{get=>review;private set{review=value;Changed();}}
    public string TrustStatus{get=>trust;private set{trust=value;Changed();}}
    public bool Offline{get=>offline;set{offline=value;Changed();}}
    public bool Busy{get=>busy;private set{busy=value;Changed();Changed(nameof(NotBusy));Changed(nameof(CanManage));Changed(nameof(CanLaunch));Changed(nameof(CanInstall));}}
    public bool NotBusy=>!Busy;
    public bool CanInstall=>plan is not null&&!Busy;
    public bool CanManage{get{if(Busy||string.IsNullOrWhiteSpace(root))return false;try{return installer.Receipt(root)?.GameId==gameId;}catch(Exception e)when(e is ArgumentException or IOException or InvalidDataException or JsonException or UnauthorizedAccessException){return false;}}}
    public bool CanLaunch=>CanManage&&GameLauncher.CanLaunch(Game,location,installer.Receipt(root));
    public string LaunchLabel=>L["LaunchGame"];
    public Visibility ReviewVisibility=>showReview?Visibility.Visible:Visibility.Collapsed;
    public double Percent{get=>percent;private set{percent=value;Changed();}}
    public InstallerViewModel()
    {
        Home=Environment.GetEnvironmentVariable("TRILEX_HOME")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Trilex","ModInstaller");
        Directory.CreateDirectory(Home);
        var assembly=Assembly.GetExecutingAssembly();
        string Resource(string suffix){using var stream=assembly.GetManifestResourceStream(assembly.GetManifestResourceNames().Single(x=>x.EndsWith(suffix,StringComparison.Ordinal)))!;using var reader=new StreamReader(stream);return reader.ReadToEnd();}
        catalogs=new(http,Resource("trusted-publisher.pem"));
        // Authenticated expired metadata may populate recovery UI; Review/Execute still refuse installation.
        catalog=catalogs.Verify(System.Text.Encoding.UTF8.GetBytes(Resource("bootstrap.json")),DateTimeOffset.MinValue);
        if(File.Exists(Path.Combine(Home,"catalog.signed.json")))
        {
            try{catalog=catalogs.Verify(File.ReadAllBytes(Path.Combine(Home,"catalog.signed.json")));}catch(Exception e)when(e is IOException or InvalidDataException or CryptographicException or JsonException){Log("Cached catalog refused: "+e.Message);}
        }
        // A complete portable bundle carries its signed catalog beside the EXE.
        // Invalid sidecars fail visibly; never silently fall back to a different release.
        var bundled=BundleSource.CatalogAt(AppContext.BaseDirectory,catalogs);
        if(bundled is not null)catalog=bundled;
        installer=new(Path.Combine(Home,"installations"),new WindowsProcessGuard());
        if(File.Exists(Path.Combine(Home,"preferences.json")))
        {
            try{var saved=Json.Read<Dictionary<string,string>>(Path.Combine(Home,"preferences.json"));ui=saved.GetValueOrDefault("ui","en");text=saved.GetValueOrDefault("text","en");voice=saved.GetValueOrDefault("voice","en");}catch(Exception e)when(e is IOException or JsonException){Log(e.Message);}
        }
        L.Set(ui);RefreshModules();Activity=catalog.ExpiresUtc<=DateTimeOffset.UtcNow?"Catalog expired. Update catalog before installation; recovery remains available.":L["Initial"];TrustStatus=L["Trust"]+" · catalog "+catalog.Version;
    }
    private void SavePreferences(){if(string.IsNullOrEmpty(Home))return;Json.Write(Path.Combine(Home,"preferences.json"),new Dictionary<string,string>{{"ui",ui},{"text",text},{"voice",voice}});}
    public void SelectAcceptedPreset()
    {
        if(!Modules.Any(m=>m.Manifest.Id=="portable-settings"&&m.Manifest.State=="stable"))throw new InvalidOperationException("The complete V7 catalog is not loaded");
        TextLanguage="pl";VoiceLanguage="pl";
        foreach(var module in Modules.Where(m=>m.Manifest.State=="stable"))module.Selected=true;
        Invalidate();Activity=L["ReviewHint"];
    }
    private void SyncLanguageModule(string id,bool selected){var module=Modules.SingleOrDefault(m=>m.Manifest.Id==id);if(module?.Selectable==true)module.Selected=selected;}
    private void RefreshModules()
    {
        var previousIds=Modules.Select(x=>x.Manifest.Id).ToHashSet();
        var selected=Modules.Where(x=>x.Selected).Select(x=>x.Manifest.Id).ToHashSet();Modules.Clear();
        foreach(var module in Game.Modules)
        {
            var vm=new ModuleViewModel(module,L);
            if(vm.Selectable)vm.Selected=module.Id switch{"polish-text"=>IsSingularity?text=="pl":selected.Contains(module.Id),"polish-dubbing"=>voice=="pl","darkness-menu" or "darkness-assets"=>!previousIds.Contains(module.Id)||selected.Contains(module.Id),_=>selected.Contains(module.Id)};
            vm.PropertyChanged+=(s,e)=>{if(e.PropertyName==nameof(ModuleViewModel.Selected)){if(IsSingularity&&module.Id=="polish-text")TextLanguage=vm.Selected?"pl":"en";if(module.Id=="polish-dubbing")VoiceLanguage=vm.Selected?"pl":"en";}Invalidate();};
            Modules.Add(vm);
        }
    }
    private void Invalidate(){plan=null;showReview=false;Changed(nameof(CanInstall));Changed(nameof(ReviewVisibility));}
    private void Log(string message)=>File.AppendAllText(Path.Combine(Home,"installer.log"),DateTimeOffset.Now.ToString("o")+" "+message.Replace(Home,"%INSTALLER_DATA%",StringComparison.OrdinalIgnoreCase).Replace(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"%USERPROFILE%",StringComparison.OrdinalIgnoreCase)+Environment.NewLine);
    public async Task Run(Func<CancellationToken,Task> action)
    {
        if(Busy)return;Busy=true;cancellation=new();Percent=0;
        try{await action(cancellation.Token);}
        catch(OperationCanceledException){Activity=L["Cancelled"];Log("Cancelled");}
        catch(Exception e){Activity=L["Error"]+": "+e.Message;Log(e.GetType().Name+": "+e.Message);}
        finally{Busy=false;cancellation.Dispose();cancellation=null;}
    }
    public void Cancel()=>cancellation?.Cancel();
    public async Task Detect(CancellationToken ct)
    {
        var locations=await Task.Run(()=>adapter.Detect(SteamRoots.Discover(),Game).ToList(),ct);
        var best=locations.FirstOrDefault(x=>x.Compatible)??locations.FirstOrDefault();
        if(best is null){DetectionStatus=L["NoGame"];return;}GameRoot=best.Root;location=best;DetectionStatus=L[best.Compatible?"Compatible":ExecutableCompatibility.CanTry(Game,best)?"UnverifiedBuild":"Wrong"];Changed(nameof(CanManage));Changed(nameof(CanLaunch));
    }
    public async Task Validate(CancellationToken ct)
    {
        location=await Task.Run(()=>adapter.Validate(root,Game),ct);DetectionStatus=L[location.Compatible?"Compatible":ExecutableCompatibility.CanTry(Game,location)?"UnverifiedBuild":"Wrong"]+" "+location.Detail;Changed(nameof(CanManage));Changed(nameof(CanLaunch));
    }
    public async Task Review(CancellationToken ct)
    {
        Invalidate();
        if(catalog.ExpiresUtc<=DateTimeOffset.UtcNow)throw new InvalidDataException("Catalog expired; fetch a current signed catalog");
        await Validate(ct);
        ExecutableConsent? consent=null;
        if(location?.Compatible!=true&&location is not null&&ExecutableCompatibility.CanTry(Game,location))
        {
            ct.ThrowIfCancellationRequested();
            var accepted=MessageBox.Show(Application.Current.MainWindow,UnverifiedExecutableText(location),L["UnverifiedTitle"],MessageBoxButton.YesNo,MessageBoxImage.Warning,MessageBoxResult.No)==MessageBoxResult.Yes;
            Log((accepted?"USER ACCEPTED":"USER DECLINED")+" unverified executable: "+location.Root+" SHA256="+location.ExeHash);
            if(!accepted)throw new OperationCanceledException(ct);
            consent=new(location.Root,location.ExeHash!);
        }
        var prerequisites=WindowsPrerequisites.Inspect();
        if(!prerequisites.Present)throw new InvalidOperationException(prerequisites.Detail+"\nMicrosoft Visual C++ v14 x86: "+WindowsPrerequisites.VisualCppX86Url+(ui=="pl"?"\nZainstaluj wymagany pakiet Microsoft i ponów sprawdzanie.":"\nInstall the Microsoft prerequisite, then review again."));
        var selected=Modules.Where(x=>x.Selected).Select(x=>x.Manifest.Id).ToList();
        var choices=new InstallChoices(IsSingularity?text:selected.Contains("polish-text")?"pl":"en",IsSingularity?voice:"en",ui,selected);
        var reviewed=PlanBuilder.Build(Game,location!,choices,consent);
        if(!IsSingularity)
        {
            var receipt=installer.Receipt(reviewed.Root);
            string? Original(PackageFile file)
            {
                var path=file.SourcePath??file.Path;
                var owned=receipt?.Files.SingleOrDefault(f=>Paths.Same(f.Path,path));
                return owned is null?Paths.HashOrNull(Paths.Safe(reviewed.Root,path)):owned.OriginalHash;
            }
            // Missing sources cannot be reconstructed. Existing different sources can be
            // tried after explicit consent bound to their actual hashes; keep pairs together.
            while(true)
            {
                var incompatible=reviewed.Modules.FirstOrDefault(m=>!m.Required&&m.Files.Any(f=>f.Kind=="recipe"&&
                    !Paths.Same(Original(f),f.SourceSha256??f.BeforeSha256)&&!Paths.Same(Original(f),f.Sha256)));
                if(incompatible is null)break;
                var mismatches=incompatible.Files.Where(f=>f.Kind=="recipe"&&!Paths.Same(Original(f),f.SourceSha256??f.BeforeSha256)&&!Paths.Same(Original(f),f.Sha256)).ToList();
                var sources=mismatches.ToDictionary(f=>f.Path,f=>Original(f));
                var detail=incompatible.Label(ui)+"\n"+string.Join("\n",mismatches.Select(f=>f.Path+"\nSHA256: "+(sources[f.Path]??"MISSING")));
                if(sources.Values.All(hash=>hash is not null))
                {
                    if(!WarnDarkness(detail,L["TryDifferentSources"]))throw new OperationCanceledException(ct);
                    reviewed=reviewed with {Modules=reviewed.Modules.Select(m=>m.Id!=incompatible.Id?m:m with {
                        Files=m.Files.Select(f=>sources.TryGetValue(f.Path,out var hash)?DarknessRecipeConsent.Bind(reviewed.GameId,f,hash!):f).ToList()
                    }).ToList()};
                    foreach(var pair in sources)Log("USER ACCEPTED Darkness source replacement: "+pair.Key+" SHA256="+pair.Value);
                    continue;
                }
                if(!WarnDarkness(detail,L["SkipIncompatible"]))throw new OperationCanceledException(ct);
                choices=choices with {
                    TextLanguage=incompatible.Id=="polish-text"?"en":choices.TextLanguage,
                    SelectedModules=choices.SelectedModules.Where(id=>id!=incompatible.Id).ToList()
                };
                reviewed=PlanBuilder.Build(Game,location!,choices,consent);
            }
            var modules=reviewed.Modules.Select(m=>m with {Files=m.Files.Select(f=>
            {
                if(f.Kind!="copy"||receipt?.Files.Any(x=>Paths.Same(x.Path,f.Path))==true)return f;
                var hash=Original(f);
                if(hash is null||Paths.Same(hash,f.Sha256)||Paths.Same(hash,f.BeforeSha256))return f;
                if(!WarnDarkness(f.Path+"\nSHA256: "+hash,L["ReplaceUnmanaged"]))throw new OperationCanceledException(ct);
                return f with {BeforeSha256=hash};
            }).ToList()}).ToList();
            reviewed=reviewed with {Modules=modules};
        }
        if(reviewed.Modules.SelectMany(m=>m.Files).Any(f=>Paths.Same(f.Path,"Binaries/SingularityPsvr2Bridge.exe")))
        {
            var triggerPrerequisite=WindowsPrerequisites.InspectTriggerHelper();
            if(!triggerPrerequisite.Present)throw new InvalidOperationException(triggerPrerequisite.Detail+"\nMicrosoft Visual C++ v14 x64: "+WindowsPrerequisites.VisualCppX64Url+(ui=="pl"?"\nZainstaluj wymagany pakiet Microsoft i ponów sprawdzanie.":"\nInstall the Microsoft prerequisite, then review again."));
        }
        plan=reviewed;
        ReviewText=$"{plan.Root}\n\nv{plan.Version} · {plan.DownloadBytes/1048576.0:F1} MB download · {plan.ExpandedBytes/1048576.0:F1} MB payload\nText: {choices.TextLanguage} / Voice: {choices.VoiceLanguage}\n\n"+string.Join("\n",plan.Modules.Select(x=>$"{x.Label(ui)} · {x.Version} · [{string.Join(", ",x.Dependencies)}]"));
        if(!IsSingularity&&HdTextures)ReviewText+="\n\n"+L["HdTextureHint"];
        if(plan.ExecutableConsent is not null)ReviewText+="\n\n"+L["UnverifiedInstalled"]+"\nSHA256: "+plan.ExeSha256;
        if(LocalAudioTools.Required(plan.Modules))ReviewText+="\n\n"+(ui=="pl"?"Remaster audio: przetwarzanie lokalne z własnej kopii gry. Dodatkowy kodek FFmpeg 9.0.2 (GPLv3), około 109 MB z gyan.dev; bez pobierania nagrań.":"Audio remaster: generated locally from your own game. Additional FFmpeg 9.0.2 codec (GPLv3), approximately 109 MB from gyan.dev; no recording download.");
        if(LocalExtractorTools.Required(plan.Modules))ReviewText+="\n\n"+(ui=="pl"?"Zasoby Centuriona powstaną z własnej kopii gry. Pełny ZIP zawiera zweryfikowany UE Viewer w folderze tools; nie wymaga pobierania z gildor.org ani pobierania modeli gry.":"Centurion assets are generated from your own game. The complete ZIP includes a verified UE Viewer in its tools folder; no gildor.org download or game-model download is needed.");
        var fanContent=plan.Modules.Where(m=>!m.RightsVerified&&m.PublisherReview is not null).ToList();
        if(fanContent.Count>0)ReviewText+="\n\n"+(ui=="pl"?"Fanowskie dodatki zatwierdzone do paczki przez Trilexa. Prawa osób trzecich pozostają zastrzeżone; to nie jest potwierdzenie licencji ani poparcia wydawcy gry.":"Fan-made additions approved for this package by Trilex. Third-party rights remain reserved; this is not verification of licenses or endorsement by the game publisher.")+"\n"+string.Join("\n",fanContent.Select(m=>m.Label(ui)+": "+m.PublisherReview!.Notice));
        showReview=true;Changed(nameof(ReviewVisibility));Changed(nameof(CanInstall));Activity=L["ReviewHint"];
    }
    public async Task Execute(CancellationToken ct,bool repair=false)
    {
        if(catalog.ExpiresUtc<=DateTimeOffset.UtcNow)throw new InvalidDataException("Catalog expired; review again");
        if(plan is null)await Review(ct);var chosen=plan!;
        var progress=BeginInstallProgress(chosen.Version);
        if(LocalAudioTools.Required(chosen.Modules))await LocalAudioTools.EnsureAsync(http,offline,progress,ct);
        if(LocalExtractorTools.Required(chosen.Modules))await LocalExtractorTools.EnsureAsync(http,offline,progress,ct);
        var downloader=new PackageDownloader(http,Path.Combine(Home,"cache"));var packages=new Dictionary<string,string>();
        foreach(var module in chosen.Modules.ToArray())
        {
            try{packages[module.Id]=BundleSource.ArchiveAt(AppContext.BaseDirectory,module,chosen.GameId)??await downloader.GetAsync(module,offline,progress,ct);}
            catch(Exception e) when(chosen.GameId=="darkness2"&&!repair&&!module.Required&&
                (e is FileNotFoundException or HttpRequestException)&&!chosen.Modules.Any(m=>m.Dependencies.Contains(module.Id)))
            {
                if(!WarnDarkness(module.Label(ui)+"\n"+e.Message,L["SkipUnavailable"]))throw new OperationCanceledException(ct);
                chosen=chosen with {
                    Modules=chosen.Modules.Where(m=>m.Id!=module.Id).ToList(),
                    Choices=chosen.Choices with {
                        TextLanguage=module.Id=="polish-text"?"en":chosen.Choices.TextLanguage,
                        SelectedModules=chosen.Choices.SelectedModules.Where(id=>id!=module.Id).ToList()
                    }
                };
            }
        }
        string? textureWork=null;
        try
        {
        if(chosen.GameId=="darkness2"&&HdTextures)
        {
            if(offline)throw new IOException(L["TexturesOnline"]);
            textureWork=Path.Combine(Home,"texture-work",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(textureWork);
            Activity=L["TexturesDownloading"];
            var pngZip=Path.Combine(textureWork,"source.zip");
            try
            {
            await new DarknessTextureDownload(http).SaveAsync(pngZip,progress,ct);
            Activity=L["TexturesConverting"];
            var prepared=await Task.Run(()=>DarknessTextureInstall.PrepareAsync(chosen.Root,installer,textureWork,pngZip,ct),ct);
            chosen=chosen with {Modules=[..chosen.Modules,prepared.Module],
                Choices=chosen.Choices with {SelectedModules=[..chosen.Choices.SelectedModules,"hd-textures"]}};
            packages[prepared.Module.Id]=prepared.Archive;
            }
            catch(Exception e) when(e is IOException or InvalidDataException or HttpRequestException)
            {
                if(repair||!WarnDarkness(e.Message,L["SkipHdTextures"]))throw;
                chosen=chosen with {Choices=chosen.Choices with {SelectedModules=chosen.Choices.SelectedModules.Where(id=>id!="hd-textures").ToList()}};
            }
        }
        bool Confirm(ReconstructionWarning warning)=>ConfirmReconstructionMismatch(warning,ct);
        var result=await Task.Run(async()=>repair?await installer.RepairAsync(chosen,packages,progress,ct,Confirm):await installer.InstallAsync(chosen,packages,progress,ct,confirmMismatch:Confirm),ct);
        Log("Verify installed files");var failures=await Task.Run(()=>installer.Verify(chosen.Root),ct);if(failures.Count>0)throw new IOException("Post-install verification failed: "+string.Join(", ",failures));
        var exceptions=result.ReconstructionWarnings??[];
        Activity=CompletionText(result)+(result.ExecutableConsent is null?"":"\n"+L["UnverifiedInstalled"]);
        Percent=100;Log(exceptions.Count==0?"Install verified "+result.Version:"Install completed with user-approved reconstruction exceptions: "+exceptions.Count);
        }
        finally{if(textureWork is not null&&Directory.Exists(textureWork))Directory.Delete(textureWork,true);}
    }
    private bool ConfirmReconstructionMismatch(ReconstructionWarning warning,CancellationToken ct)=>Application.Current.Dispatcher.Invoke(()=>
    {
        ct.ThrowIfCancellationRequested();Log("Reconstruction mismatch: "+warning.Describe());
        var accepted=MessageBox.Show(Application.Current.MainWindow,MismatchText(warning),L["MismatchTitle"],MessageBoxButton.YesNo,MessageBoxImage.Warning,MessageBoxResult.No)==MessageBoxResult.Yes;
        Log((accepted?"USER ACCEPTED":"USER DECLINED")+" reconstruction mismatch: "+warning.Describe());
        ct.ThrowIfCancellationRequested();return accepted;
    });
    internal string MismatchText(ReconstructionWarning warning)=>L["MismatchWarning"]+"\n\n"+warning.Path+"\n\nExpected SHA256: "+warning.ExpectedSha256+"\nActual SHA256: "+warning.ActualSha256;
    internal string UnverifiedExecutableText(GameLocation build)=>L["UnverifiedWarning"]+"\n\n"+build.Root+"\nSHA256: "+build.ExeHash;
    internal string CompletionText(InstallReceipt receipt)=>(receipt.ReconstructionWarnings?.Count??0)==0?L["Done"]+" v"+receipt.Version:L["InstalledExceptions"]+"\n"+string.Join("\n",receipt.ReconstructionWarnings!.Select(w=>w.Path));
    internal string VerificationText(List<string> failures,InstallReceipt? receipt)=>failures.Count>0?"Repair required: "+string.Join(", ",failures):(receipt?.ReconstructionWarnings?.Count??0)==0?L["Verified"]:L["VerifiedExceptions"]+"\n"+string.Join("\n",receipt!.ReconstructionWarnings!.Select(w=>w.Path));
    internal IProgress<ProgressInfo> BeginInstallProgress(string version)
    {
        Activity=L["Installing"];Log("Install started "+version);
        string? lastProgress=null;
        // Preparation can dominate the runtime; phase percentages are not overall progress.
        // Keep one stable user-facing message and record details only on phase/item changes.
        return new Progress<ProgressInfo>(p=>{var detail=p.Stage+" · "+p.Item;if(detail!=lastProgress){lastProgress=detail;Log(detail);}});
    }
    public async Task Verify(CancellationToken ct)
    {
        var failures=await Task.Run(()=>installer.Verify(root),ct);
        var receipt=installer.Receipt(root);
        Activity=VerificationText(failures,receipt)+(receipt?.ExecutableConsent is null?"":"\n"+L["UnverifiedInstalled"]);
    }
    public async Task Recover(CancellationToken ct){await Task.Run(()=>installer.Recover(root),ct);Activity=L["Restored"];}
    public async Task Uninstall(CancellationToken ct){await Task.Run(()=>installer.Uninstall(root,ct),ct);Activity=L["Restored"];plan=null;Changed(nameof(CanInstall));}
    public async Task Update(CancellationToken ct)
    {
        catalog=await catalogs.FetchLatestAsync(ct);Paths.AtomicBytes(Path.Combine(Home,"catalog.signed.json"),catalogs.LastVerifiedEnvelope!);
        if(!catalog.Games.Any(x=>x.Id==gameId)){gameId="singularity";GameRoot="";DetectionStatus="";Modules.Clear();Changed(nameof(SingularityOptions));Changed(nameof(DarknessOptions));Changed(nameof(LaunchLabel));}
        location=null;Changed(nameof(CanLaunch));RefreshModules();Invalidate();TrustStatus=L["Trust"]+" · "+catalog.Version;Activity=L["ReviewHint"];
    }
    public void ImportCatalog(string path){catalog=catalogs.Verify(File.ReadAllBytes(path));Paths.AtomicBytes(Path.Combine(Home,"catalog.signed.json"),catalogs.LastVerifiedEnvelope!);if(!catalog.Games.Any(x=>x.Id==gameId)){gameId="singularity";GameRoot="";DetectionStatus="";Modules.Clear();Changed(nameof(SingularityOptions));Changed(nameof(DarknessOptions));Changed(nameof(LaunchLabel));}
        location=null;Changed(nameof(CanLaunch));RefreshModules();Invalidate();TrustStatus=L["Trust"]+" · "+catalog.Version;Activity=L["ReviewHint"];}
    public void ImportPackages(IEnumerable<string> files)
    {
        var cache=Path.Combine(Home,"cache");Directory.CreateDirectory(cache);
        foreach(var file in files)
        {
            var hash=Paths.Hash(file);var modules=Game.Modules.Where(x=>Paths.Same(x.ArchiveSha256,hash)).ToList();
            // Empty metadata/INI-only ZIPs can legitimately have the same digest.
            if(modules.Count==0||!modules.Any(m=>m.State=="stable"&&new FileInfo(file).Length==m.DownloadBytes))throw new InvalidDataException("Package is unavailable or has wrong size");
            Paths.AtomicCopy(file,Paths.Safe(cache,hash.ToLowerInvariant()+".zip"));
        }
        Offline=true;Activity=L["Ready"];
    }
    private bool WarnDarkness(string detail,string warning)
    {
        var accepted=MessageBox.Show(Application.Current.MainWindow,warning+"\n\n"+detail,L["UnverifiedTitle"],
            MessageBoxButton.YesNo,MessageBoxImage.Warning,MessageBoxResult.No)==MessageBoxResult.Yes;
        Log((accepted?"USER ACCEPTED":"USER DECLINED")+" Darkness II: "+detail);
        return accepted;
    }
    public void Launch(){if(!CanLaunch)throw new InvalidOperationException("Verify a managed installation first");
        Process.Start(GameLauncher.CreateStartInfo(Game,location!,installer.Receipt(root)!));}
    public void OpenLogs()=>Process.Start(new ProcessStartInfo(Home){UseShellExecute=true});
    public void Dispose(){cancellation?.Cancel();http.Dispose();}
}
