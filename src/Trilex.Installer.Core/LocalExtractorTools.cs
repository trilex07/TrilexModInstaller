using System.IO.Compression;

namespace Trilex.Installer.Core;

/// <summary>Optional upstream exporter. No exporter executable is bundled in the installer.</summary>
public static class LocalExtractorTools
{
    public const string DownloadUrl="https://www.gildor.org/down/47/umodel/umodel_win32.zip";
    public const string ArchiveHash="2CEAD261B360DBCC3E703F091837B415F868250DD6A73A154F4232D09E226EF8";
    public static readonly IReadOnlyDictionary<string,string> Files=new Dictionary<string,string>{
        ["umodel.exe"]="13502E5A4D8F6B5F32252AFEBD6360F7302CCFACCF6B8DDA65BEFF0BE2D364A0",
        ["SDL2.dll"]="FFBF5AA7D13FED5D12BA68BA3AF930A15AA5D0FF97CFB50A5965A498A941A6DD"};
    private static string Cache=>Paths.Safe(LocalAudioTools.Home,"tools/ueviewer-2022-pinned");
    public static bool Required(IEnumerable<ModuleManifest> modules)=>modules.SelectMany(m=>m.Files).SelectMany(f=>f.Recipe??[]).Any(p=>p.Source is "centurion-clip" or "centurion-shell" or "centurion-animation");
    public static string Resolve()
    {
        foreach(var pair in Files){var file=Paths.Safe(Cache,pair.Key);if(!File.Exists(file)||!Paths.Same(Paths.Hash(file),pair.Value))throw new InvalidDataException("Pinned UE Viewer cache missing or modified; prepare extractor tools first");}
        foreach(var name in new[]{"LICENSE.txt","readme.txt"})if(!File.Exists(Paths.Safe(Cache,name)))throw new InvalidDataException("UE Viewer notices missing");
        return Paths.Safe(Cache,"umodel.exe");
    }
    public static void Import(string archive)
    {
        Paths.AssertNoLinks(archive);
        if(!Paths.Same(Paths.Hash(archive),ArchiveHash))throw new InvalidDataException("Unreviewed UE Viewer archive");
        using var zip=ZipFile.OpenRead(archive);
        var names=Files.Keys.Concat(new[]{"LICENSE.txt","readme.txt","umodel_64.exe","SDL2_64.dll"}).ToHashSet(StringComparer.Ordinal);
        if(zip.Entries.Count!=names.Count||zip.Entries.Any(e=>!names.Remove(e.FullName))||names.Count!=0)throw new InvalidDataException("Unexpected UE Viewer archive entries");
        var checkedFiles=new Dictionary<string,byte[]>();
        foreach(var entry in zip.Entries)
        {
            if(entry.FullName is "umodel_64.exe" or "SDL2_64.dll")continue; // pinned archive contains both architectures; only the reviewed x86 pair executes
            if(entry.Length is <=0 or >32*1024*1024)throw new InvalidDataException("UE Viewer entry budget exceeded");
            using var bytes=new MemoryStream();using(var input=entry.Open())input.CopyTo(bytes);
            var data=bytes.ToArray();
            if(Files.TryGetValue(entry.FullName,out var hash)&&!Paths.Same(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(data)),hash))throw new InvalidDataException("UE Viewer executable identity changed");
            checkedFiles.Add(entry.FullName,data);
        }
        // Validate all entries before publishing any runnable file. Resolve rechecks both binaries.
        foreach(var pair in checkedFiles.OrderBy(p=>p.Key=="umodel.exe"?1:0))Paths.AtomicBytes(Paths.Safe(Cache,pair.Key),pair.Value);
        Resolve();
    }
    public static async Task EnsureAsync(HttpClient http,bool offline,IProgress<ProgressInfo>? progress,CancellationToken ct)
    {
        try{Resolve();return;}catch(InvalidDataException){}
        var imported=Environment.GetEnvironmentVariable("TRILEX_UEVIEWER_ARCHIVE");
        if(imported is not null){Import(imported);return;}
        if(offline)throw new FileNotFoundException("Pinned UE Viewer offline cache absent; import its original archive before installation");
        Directory.CreateDirectory(Cache);var part=Paths.Safe(Cache,"download-"+Guid.NewGuid().ToString("N")+".tmp");
        try
        {
            using var request=new HttpRequestMessage(HttpMethod.Get,DownloadUrl);request.Headers.Referrer=new("https://www.gildor.org/en/projects/umodel");
            using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,ct);
            if(response.RequestMessage?.RequestUri?.AbsoluteUri!=DownloadUrl||(int)response.StatusCode is >=300 and <400)throw new InvalidDataException("UE Viewer redirect refused");
            response.EnsureSuccessStatusCode();const long maximum=32L*1024*1024;
            if(response.Content.Headers.ContentLength is >maximum)throw new InvalidDataException("UE Viewer archive exceeds budget");
            await using(var input=await response.Content.ReadAsStreamAsync(ct))
            await using(var output=new FileStream(part,FileMode.CreateNew,FileAccess.Write,FileShare.None,81920,true))
            {
                byte[] buffer=new byte[81920];long total=0;int n;
                while((n=await input.ReadAsync(buffer,ct))>0){total+=n;if(total>maximum)throw new InvalidDataException("UE Viewer archive exceeds budget");await output.WriteAsync(buffer.AsMemory(0,n),ct);progress?.Report(new("Local extractor","UE Viewer · upstream download",total,response.Content.Headers.ContentLength??maximum));}
            }
            Import(part);
        }
        finally{if(File.Exists(part))File.Delete(part);}
    }
}
