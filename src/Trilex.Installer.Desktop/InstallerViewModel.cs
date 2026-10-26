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
    public bool Selected {get=>selected;set{selected=value;PropertyChanged?.Invoke(this,new(nameof(Selected)));}}
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
    private readonly SingularityGameAdapter adapter=new();
    private Catalog catalog;
    private GameManifest Game=>catalog.Games.Single(x=>x.Id=="singularity");
    private GameLocation? location;
    private InstallPlan? plan;
    private CancellationTokenSource? cancellation;
    private string ui="en",text="en",voice="en",root="",activity="",detection="",review="",trust="";
    private bool busy,offline,showReview;
    private double percent;
    public string UiLanguage{get=>ui;set{if(value is not("en"or"pl"))return;ui=value;L.Set(value);RefreshModules();Changed();Activity=L["Initial"];SavePreferences();}}
    public string TextLanguage{get=>text;set{text=value;Invalidate();Changed();SavePreferences();}}
    public string VoiceLanguage{get=>voice;set{voice=value;Invalidate();Changed();SavePreferences();}}
    public string GameRoot{get=>root;set{root=value;location=null;Invalidate();Changed();Changed(nameof(CanManage));Changed(nameof(CanLaunch));}}
    public string Activity{get=>activity;private set{activity=value;Changed();}}
    public string DetectionStatus{get=>detection;private set{detection=value;Changed();}}
    public string ReviewText{get=>review;private set{review=value;Changed();}}
    public string TrustStatus{get=>trust;private set{trust=value;Changed();}}
    public bool Offline{get=>offline;set{offline=value;Changed();}}
    public bool Busy{get=>busy;private set{busy=value;Changed();Changed(nameof(NotBusy));Changed(nameof(CanManage));Changed(nameof(CanLaunch));Changed(nameof(CanInstall));}}
    public bool NotBusy=>!Busy;
    public bool CanInstall=>plan is not null&&!Busy;
    public bool CanManage{get{if(Busy||string.IsNullOrWhiteSpace(root))return false;try{return installer.Receipt(root) is not null;}catch(Exception e)when(e is ArgumentException or IOException or InvalidDataException or JsonException or UnauthorizedAccessException){return false;}}}
    public bool CanLaunch=>CanManage&&location?.Compatible==true;
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
        installer=new(Path.Combine(Home,"installations"),new WindowsProcessGuard());
        if(File.Exists(Path.Combine(Home,"preferences.json")))
        {
            try{var saved=Json.Read<Dictionary<string,string>>(Path.Combine(Home,"preferences.json"));ui=saved.GetValueOrDefault("ui","en");text=saved.GetValueOrDefault("text","en");voice=saved.GetValueOrDefault("voice","en");}catch(Exception e)when(e is IOException or JsonException){Log(e.Message);}
        }
        L.Set(ui);RefreshModules();Activity=catalog.ExpiresUtc<=DateTimeOffset.UtcNow?"Catalog expired. Update catalog before installation; recovery remains available.":L["Initial"];TrustStatus=L["Trust"]+" · catalog "+catalog.Version;
    }
    private void SavePreferences(){if(string.IsNullOrEmpty(Home))return;Json.Write(Path.Combine(Home,"preferences.json"),new Dictionary<string,string>{{"ui",ui},{"text",text},{"voice",voice}});}
    private void RefreshModules(){var selected=Modules.Where(x=>x.Selected).Select(x=>x.Manifest.Id).ToHashSet();Modules.Clear();foreach(var module in Game.Modules){var vm=new ModuleViewModel(module,L);if(vm.Selectable&&selected.Contains(module.Id))vm.Selected=true;vm.PropertyChanged+=(s,e)=>Invalidate();Modules.Add(vm);}}
    private void Invalidate(){plan=null;showReview=false;Changed(nameof(CanInstall));Changed(nameof(ReviewVisibility));}
    private void Log(string message)=>File.AppendAllText(Path.Combine(Home,"installer.log"),DateTimeOffset.Now.ToString("o")+" "+message.Replace(Home,"%INSTALLER_DATA%",StringComparison.OrdinalIgnoreCase).Replace(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"%USERPROFILE%",StringComparison.OrdinalIgnoreCase)+Environment.NewLine);
    public async Task Run(Func<CancellationToken,Task> action)
    {
        if(Busy)return;Busy=true;cancellation=new();Percent=0;
        try{await action(cancellation.Token);}
        catch(OperationCanceledException){Activity=L["Cancelled"];Log("Cancelled");}
        catch(Exception e){Activity=e.Message;Log(e.GetType().Name+": "+e.Message);}
        finally{Busy=false;cancellation.Dispose();cancellation=null;}
    }
    public void Cancel()=>cancellation?.Cancel();
    public async Task Detect(CancellationToken ct)
    {
        var locations=await Task.Run(()=>adapter.Detect(SteamRoots.Discover(),Game).ToList(),ct);
        var best=locations.FirstOrDefault(x=>x.Compatible)??locations.FirstOrDefault();
        if(best is null){DetectionStatus=L["NoGame"];return;}GameRoot=best.Root;location=best;DetectionStatus=L[best.Compatible?"Compatible":"Wrong"];Changed(nameof(CanManage));Changed(nameof(CanLaunch));
    }
    public async Task Validate(CancellationToken ct)
    {
        location=await Task.Run(()=>adapter.Validate(root,Game),ct);DetectionStatus=L[location.Compatible?"Compatible":"Wrong"]+" "+location.Detail;Changed(nameof(CanManage));Changed(nameof(CanLaunch));
    }
    public async Task Review(CancellationToken ct)
    {
        if(catalog.ExpiresUtc<=DateTimeOffset.UtcNow)throw new InvalidDataException("Catalog expired; fetch a current signed catalog");
        await Validate(ct);
        var choices=new InstallChoices(text,voice,ui,Modules.Where(x=>x.Selected).Select(x=>x.Manifest.Id).ToList());
        plan=PlanBuilder.Build(Game,location!,choices);
        ReviewText=$"{plan.Root}\n\nv{plan.Version} · {plan.DownloadBytes/1048576.0:F1} MB download · {plan.ExpandedBytes/1048576.0:F1} MB payload\nText: {text} / Voice: {voice}\n\n"+string.Join("\n",plan.Modules.Select(x=>$"{x.Label(ui)} · {x.Version} · [{string.Join(", ",x.Dependencies)}]"));
        showReview=true;Changed(nameof(ReviewVisibility));Changed(nameof(CanInstall));Activity=L["ReviewHint"];
    }
    public async Task Execute(CancellationToken ct,bool repair=false)
    {
        if(catalog.ExpiresUtc<=DateTimeOffset.UtcNow)throw new InvalidDataException("Catalog expired; review again");
        if(plan is null)await Review(ct);var chosen=plan!;
        var progress=new Progress<ProgressInfo>(p=>{Activity=$"{p.Stage} · {p.Item}";Percent=p.Total==0?0:100.0*p.Completed/p.Total;});
        var downloader=new PackageDownloader(http,Path.Combine(Home,"cache"));var packages=new Dictionary<string,string>();
        foreach(var module in chosen.Modules)packages[module.Id]=await downloader.GetAsync(module,offline,progress,ct);
        var result=await Task.Run(async()=>repair?await installer.RepairAsync(chosen,packages,progress,ct):await installer.InstallAsync(chosen,packages,progress,ct),ct);
        var failures=await Task.Run(()=>installer.Verify(chosen.Root),ct);if(failures.Count>0)throw new IOException("Post-install verification failed: "+string.Join(", ",failures));
        Activity=L["Done"]+" v"+result.Version;Percent=100;Log("Install verified "+result.Version);
    }
    public async Task Verify(CancellationToken ct){var failures=await Task.Run(()=>installer.Verify(root),ct);Activity=failures.Count==0?L["Verified"]:"Repair required: "+string.Join(", ",failures);}
    public async Task Recover(CancellationToken ct){await Task.Run(()=>installer.Recover(root),ct);Activity=L["Restored"];}
    public async Task Uninstall(CancellationToken ct){await Task.Run(()=>installer.Uninstall(root,ct),ct);Activity=L["Restored"];plan=null;Changed(nameof(CanInstall));}
    public async Task Update(CancellationToken ct)
    {
        catalog=await catalogs.FetchLatestAsync(ct);Paths.AtomicBytes(Path.Combine(Home,"catalog.signed.json"),catalogs.LastVerifiedEnvelope!);
        RefreshModules();Invalidate();TrustStatus=L["Trust"]+" · "+catalog.Version;Activity=L["ReviewHint"];
    }
    public void ImportCatalog(string path){catalog=catalogs.Verify(File.ReadAllBytes(path));Paths.AtomicBytes(Path.Combine(Home,"catalog.signed.json"),catalogs.LastVerifiedEnvelope!);RefreshModules();Invalidate();TrustStatus=L["Trust"]+" · "+catalog.Version;Activity=L["ReviewHint"];}
    public void ImportPackages(IEnumerable<string> files)
    {
        var cache=Path.Combine(Home,"cache");Directory.CreateDirectory(cache);
        foreach(var file in files)
        {
            var hash=Paths.Hash(file);var module=Game.Modules.SingleOrDefault(x=>Paths.Same(x.ArchiveSha256,hash))??throw new InvalidDataException("Archive is not in the signed catalog");
            if(module.State!="stable" || new FileInfo(file).Length!=module.DownloadBytes)throw new InvalidDataException("Package is unavailable or has wrong size");
            Paths.AtomicCopy(file,Paths.Safe(cache,hash.ToLowerInvariant()+".zip"));
        }
        Offline=true;Activity=L["Ready"];
    }
    public void Launch(){if(!CanLaunch)throw new InvalidOperationException("Verify a managed installation first");Process.Start(new ProcessStartInfo("steam://rungameid/"+Game.SteamAppId){UseShellExecute=true});}
    public void OpenLogs()=>Process.Start(new ProcessStartInfo(Home){UseShellExecute=true});
    public void Dispose(){cancellation?.Cancel();http.Dispose();}
}
