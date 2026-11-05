using System.IO.Compression;

namespace Trilex.Installer.Core;

/// <summary>Mutable source archive, never an installable module.</summary>
public sealed class DarknessTextureDownload(HttpClient http)
{
    public const string FileName="Darkness2VR-WeaponsTentacles-2K-PNG.zip";
    private static readonly Uri Source=new("https://drive.usercontent.google.com/download?id=1D_3CC0cAZIZtDKoxoxXm10jJfGH5z7K7&export=download&confirm=t");
    private const long Budget=4L*1024*1024*1024;
    public async Task SaveAsync(string destination,IProgress<ProgressInfo>? progress,CancellationToken ct)
    {
        destination=Path.GetFullPath(destination);
        if(!destination.EndsWith(".zip",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Choose a ZIP destination");
        Paths.AssertNoLinks(destination);
        var temporary=destination+"."+Guid.NewGuid().ToString("N")+".part";
        try
        {
            using var response=await ReleaseTransport.SendAsync(http,Source,0,ct);
            response.EnsureSuccessStatusCode();
            if(response.Content.Headers.ContentType?.MediaType=="text/html" || response.Content.Headers.ContentLength>Budget)
                throw new InvalidDataException("Drive did not return a downloadable texture ZIP within the size limit");
            var buffer=new byte[81920];
            await using(var input=await response.Content.ReadAsStreamAsync(ct))
            await using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None,81920,true))
            {
                long total=0;
                int count;
                while((count=await input.ReadAsync(buffer,ct))>0)
                {
                    total+=count;
                    if(total>Budget)throw new InvalidDataException("Texture archive exceeds the download limit");
                    await output.WriteAsync(buffer.AsMemory(0,count),ct);
                    progress?.Report(new("Download",FileName,total,response.Content.Headers.ContentLength??0));
                }
            }
            using(var zip=ZipFile.OpenRead(temporary))
            {
                if(zip.Entries.Count is 0 or >2000)throw new InvalidDataException("Unexpected texture ZIP entry count");
                var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                byte[] png=[137,80,78,71,13,10,26,10], header=new byte[8];
                long expanded=0;
                foreach(var entry in zip.Entries)
                {
                    ct.ThrowIfCancellationRequested();
                    var name=entry.FullName.TrimEnd('/');
                    Paths.Safe(Path.GetDirectoryName(destination)!,name);
                    if(!seen.Add(name)||((entry.ExternalAttributes>>16)&0xF000)==0xA000||
                        (entry.ExternalAttributes&(int)FileAttributes.ReparsePoint)!=0)
                        throw new InvalidDataException("Unexpected duplicate or link in texture archive");
                    var parts=name.Split('/');
                    if(parts.Length==1 && parts[0] is ("weapons" or "tentacles") && entry.FullName.EndsWith('/') && entry.Length==0)continue;
                    if(parts.Length!=2||parts[0] is not ("weapons" or "tentacles")||!parts[1].EndsWith(".png",StringComparison.OrdinalIgnoreCase)||
                        entry.Length<8||entry.Length>256L*1024*1024||(expanded+=entry.Length)>Budget)
                        throw new InvalidDataException("Expected only PNG textures in weapons/ and tentacles/");
                    using var data=entry.Open();
                    await data.ReadExactlyAsync(header,ct);
                    if(!header.AsSpan().SequenceEqual(png))throw new InvalidDataException("Invalid PNG signature");
                    long remaining=entry.Length-8;
                    int count;
                    while((count=await data.ReadAsync(buffer,ct))>0)
                    {
                        remaining-=count;
                        if(remaining<0)throw new InvalidDataException("Texture entry exceeds ZIP length");
                    }
                    if(remaining!=0)throw new InvalidDataException("Truncated texture entry");
                }
                if(expanded==0)throw new InvalidDataException("No PNG textures in ZIP");
            }
            ct.ThrowIfCancellationRequested();
            Paths.AssertNoLinks(destination);
            File.Move(temporary,destination,true);
        }
        finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
}
