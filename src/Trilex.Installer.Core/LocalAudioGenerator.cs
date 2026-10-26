using System.IO.Compression;

namespace Trilex.Installer.Core;

/// <summary>Fixed optional upstream codec, downloaded only for a selected local-audio recipe.</summary>
public static class LocalAudioTools
{
    public const string DownloadUrl="https://www.gyan.dev/ffmpeg/builds/packages/ffmpeg-9.0.2-essentials_build.zip";
    public const string ArchiveHash="60F467265B1E312373DBCD92200C2618A74850F98D3D078E94296BB3FA2047BA";
    public const string EncoderHash="3256173F3F8BFFD7DF12227C68ADF68025EDB1832273A9530688A7BB1ED8EDEC";
    private const string LabEncoderHash="589E50B766D251AFDF181DD664D40BD94407E200B019989FD7468C7D118A28D0";
    public static string Home=>Environment.GetEnvironmentVariable("TRILEX_HOME")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Trilex","ModInstaller");
    private static string Cache=>Paths.Safe(Home,"tools/ffmpeg-9.0.2");
    public static bool Required(IEnumerable<ModuleManifest> modules)=>modules.SelectMany(m=>m.Files).SelectMany(f=>f.Recipe??[]).Any(p=>p.Source=="audio");
    public static void VerifyEncoder(string file)
    {
        Paths.AssertNoLinks(file);
        var hash=Paths.Hash(file);
        if(!Paths.Same(hash,EncoderHash)&&!Paths.Same(hash,LabEncoderHash))throw new InvalidDataException("Audio encoder identity is not approved");
    }
    public static string Resolve()
    {
        // Explicit offline lab/import override still cannot execute an arbitrary binary.
        var file=Environment.GetEnvironmentVariable("TRILEX_AUDIO_CODEC")??Paths.Safe(Cache,"ffmpeg.exe");
        if(!File.Exists(file))throw new FileNotFoundException("Pinned optional audio codec is not cached; prepare audio tools before installation");
        VerifyEncoder(file);return Path.GetFullPath(file);
    }
    public static async Task EnsureAsync(HttpClient http,bool offline,IProgress<ProgressInfo>? progress,CancellationToken ct)
    {
        if(Environment.GetEnvironmentVariable("TRILEX_AUDIO_CODEC") is not null){Resolve();return;}
        var exe=Paths.Safe(Cache,"ffmpeg.exe");
        if(File.Exists(exe)){VerifyEncoder(exe);return;}
        if(offline)throw new FileNotFoundException("Local audio requires the pinned FFmpeg 9.0.2 offline tool cache");
        Directory.CreateDirectory(Cache);Paths.AssertNoLinks(Cache);
        var archive=Paths.Safe(Cache,"upstream.zip");
        if(!File.Exists(archive)||!Paths.Same(Paths.Hash(archive),ArchiveHash))
        {
            var part=Paths.Safe(Cache,"download-"+Guid.NewGuid().ToString("N")+".tmp");
            try
            {
                // This fixed upstream URL is separate from signed game packages.
                // Redirects are refused rather than accepting a package-specified executable host.
                using var response=await http.GetAsync(DownloadUrl,HttpCompletionOption.ResponseHeadersRead,ct);
                if(response.RequestMessage?.RequestUri?.AbsoluteUri!=DownloadUrl||(int)response.StatusCode is >=300 and <400)throw new InvalidDataException("Audio tool redirect refused");
                response.EnsureSuccessStatusCode();
                const long maximum=140L*1024*1024;
                if(response.Content.Headers.ContentLength is >maximum)throw new InvalidDataException("Audio tool archive exceeds budget");
                await using(var input=await response.Content.ReadAsStreamAsync(ct))
                await using(var output=new FileStream(part,FileMode.CreateNew,FileAccess.Write,FileShare.None,81920,true))
                {
                    var buffer=new byte[81920];long total=0;int n;
                    while((n=await input.ReadAsync(buffer,ct))>0){total+=n;if(total>maximum)throw new InvalidDataException("Audio tool archive exceeds budget");await output.WriteAsync(buffer.AsMemory(0,n),ct);progress?.Report(new("Audio codec","FFmpeg 9.0.2 GPLv3 · upstream download",total,response.Content.Headers.ContentLength??maximum));}
                }
                if(!Paths.Same(Paths.Hash(part),ArchiveHash))throw new InvalidDataException("Audio tool archive identity changed");
                File.Move(part,archive,true);
            }
            finally{if(File.Exists(part))File.Delete(part);}
        }
        using var zip=ZipFile.OpenRead(archive);
        const string prefix="ffmpeg-9.0.2-essentials_build/";
        var entry=zip.GetEntry(prefix+"bin/ffmpeg.exe")??throw new InvalidDataException("Pinned encoder entry absent");
        if(entry.Length is <=0 or >125829120)throw new InvalidDataException("Pinned encoder exceeds budget");
        using var bytes=new MemoryStream();using(var input=entry.Open())input.CopyTo(bytes);
        if(bytes.Length!=entry.Length||!Paths.Same(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes.ToArray())),EncoderHash))throw new InvalidDataException("Downloaded encoder identity changed");
        foreach(var name in new[]{"LICENSE","README.txt"})
        {
            var notice=zip.GetEntry(prefix+name)??throw new InvalidDataException("Upstream codec notices absent");
            if(notice.Length>1024*1024)throw new InvalidDataException("Codec notice exceeds budget");
            using var content=notice.Open();using var memory=new MemoryStream();content.CopyTo(memory);Paths.AtomicBytes(Paths.Safe(Cache,name),memory.ToArray());
        }
        Paths.AtomicBytes(Paths.Safe(Cache,"SOURCE.txt"),System.Text.Encoding.UTF8.GetBytes("FFmpeg 9.0.2 by Gyan Doshi; GPLv3. Downloaded directly from upstream, not included in game packages.\nhttps://www.gyan.dev/ffmpeg/builds/\nhttps://github.com/FFmpeg/FFmpeg/tree/n9.0.2\n"));
        Paths.AtomicBytes(exe,bytes.ToArray());VerifyEncoder(exe);
    }
}

public static class LocalAudioGenerator
{
    public static void Apply(string decoded,string staging,string metadata,Stream output,int expectedLength)
    {
        if(expectedLength is <=0 or >268435456||new FileInfo(metadata).Length>16384)throw new InvalidDataException("Audio recipe budget exceeded");
        var codec=LocalAudioTools.Resolve();
        var folder=Paths.Safe(staging,"_local-audio/"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        var generated=Path.Combine(folder,"output.xxx");
        LocalImageGenerator.Run(staging,generated,["--audio",decoded,generated,metadata,codec],output,expectedLength);
    }
}
