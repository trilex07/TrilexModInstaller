using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;

namespace Trilex.Installer.Core;

public sealed class PackageDownloader(HttpClient http,string cache,string repository="trilex07/TrilexModInstaller")
{
    public async Task<string> GetAsync(ModuleManifest module,bool offline,IProgress<ProgressInfo>? progress,CancellationToken ct)
    {
        if(!PlanBuilder.ValidHash(module.ArchiveSha256))throw new InvalidDataException("Package SHA required");
        Directory.CreateDirectory(cache);Paths.AssertNoLinks(cache);
        var target=Paths.Safe(cache,module.ArchiveSha256!.ToLowerInvariant()+".zip");
        if(File.Exists(target)) {if(Paths.Same(Paths.Hash(target),module.ArchiveSha256)&&new FileInfo(target).Length==module.DownloadBytes)return target;File.Delete(target);}
        if(offline)throw new FileNotFoundException("Verified package is not in the offline cache: "+module.Id);
        var uri=new Uri(module.DownloadUrl??throw new InvalidDataException("Missing package URL"));CatalogService.ValidateReleaseUrl(uri,repository);
        var partial=target+".part";
        for(var attempt=0;attempt<3;attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var start=File.Exists(partial)?new FileInfo(partial).Length:0;
                if(start>module.DownloadBytes){File.Delete(partial);start=0;}
                if(start==module.DownloadBytes){if(Paths.Same(Paths.Hash(partial),module.ArchiveSha256)){File.Move(partial,target);return target;}File.Delete(partial);start=0;}
                using var response=await ReleaseTransport.SendAsync(http,uri,start,ct);
                response.EnsureSuccessStatusCode();
                var resume=response.StatusCode==HttpStatusCode.PartialContent;
                if(resume && (response.Content.Headers.ContentRange?.From!=start || response.Content.Headers.ContentRange?.Length!=module.DownloadBytes))throw new InvalidDataException("Incorrect download range");
                if(!resume)start=0;
                if(response.Content.Headers.ContentLength is long count && count!=module.DownloadBytes-start)throw new InvalidDataException("Package length mismatch");
                await using(var output=new FileStream(partial,resume?FileMode.Append:FileMode.Create,FileAccess.Write,FileShare.None,81920,true))
                await using(var input=await response.Content.ReadAsStreamAsync(ct))
                {
                    var buffer=new byte[81920];long total=start;int n;
                    while((n=await input.ReadAsync(buffer,ct))>0){total+=n;if(total>module.DownloadBytes)throw new InvalidDataException("Download exceeds signed size");await output.WriteAsync(buffer.AsMemory(0,n),ct);progress?.Report(new("Download",module.Id,total,module.DownloadBytes));}
                    await output.FlushAsync(ct);if(total!=module.DownloadBytes)throw new IOException("Incomplete download");
                }
                if(!Paths.Same(Paths.Hash(partial),module.ArchiveSha256)){File.Delete(partial);throw new InvalidDataException("Package checksum mismatch");}
                File.Move(partial,target);return target;
            }
            catch(Exception ex)when(attempt<2 && ex is HttpRequestException or IOException && ex is not InvalidDataException){await Task.Delay(TimeSpan.FromMilliseconds(300*(attempt+1)),ct);}
        }
        throw new IOException("Download failed");
    }
}
public static class ReleaseTransport
{
    public static async Task<HttpResponseMessage> SendAsync(HttpClient http,Uri uri,long start,CancellationToken ct)
    {
        for(var redirect=0;redirect<5;redirect++)
        {
            using var request=new HttpRequestMessage(HttpMethod.Get,uri);if(start>0)request.Headers.Range=new RangeHeaderValue(start,null);
            var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,ct);
            if((int)response.StatusCode is not (301 or 302 or 303 or 307 or 308))return response;
            var next=response.Headers.Location is { } location?new Uri(uri,location):throw new InvalidDataException("Empty redirect");response.Dispose();
            if(next.Scheme!="https" || !next.IsDefaultPort || !string.IsNullOrEmpty(next.UserInfo) || next.Host is not ("release-assets.githubusercontent.com" or "objects.githubusercontent.com" or "github-releases.githubusercontent.com"))throw new InvalidDataException("Untrusted release redirect");
            uri=next;
        }
        throw new InvalidDataException("Too many release redirects");
    }
}
public static class PackageExtractor
{
    public static void Extract(string archive,string staging,ModuleManifest module)
    {
        if(!PlanBuilder.HasDistributionReview(module) || !Paths.Same(Paths.Hash(archive),module.ArchiveSha256) || new FileInfo(archive).Length!=module.DownloadBytes)throw new InvalidDataException("Package publisher review or checksum rejected");
        var allowed=module.Files.Where(x=>x.Kind=="copy").Select(x=>x.Entry)
            .Concat(module.Files.SelectMany(x=>x.Recipe??[]).Where(x=>x.Source is "payload" or "xor-original" or "instructions" or "image" or "knife" or "audio").Select(x=>x.Entry??""))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        using var zip=ZipFile.OpenRead(archive);long total=0;var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if(zip.Entries.Count>10000)throw new InvalidDataException("Too many archive entries");
        foreach(var entry in zip.Entries)
        {
            var target=Paths.Safe(staging,entry.FullName.TrimEnd('/'));
            if(((entry.ExternalAttributes>>16)&0xF000)==0xA000 || (entry.ExternalAttributes&(int)FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Archive links forbidden");
            if(entry.FullName.EndsWith('/'))continue;
            if(!seen.Add(entry.FullName) || !allowed.Contains(entry.FullName))throw new InvalidDataException("Unlisted/duplicate package entry");
            total=checked(total+entry.Length);if(total>module.ExpandedBytes || entry.Length>int.MaxValue)throw new InvalidDataException("Archive exceeds expanded budget");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);using var input=entry.Open();using var output=new FileStream(target,FileMode.CreateNew);
            var buffer=new byte[81920];long written=0;int n;while((n=input.Read(buffer))>0){written+=n;if(written>entry.Length)throw new InvalidDataException("ZIP length mismatch");output.Write(buffer,0,n);}if(written!=entry.Length)throw new InvalidDataException("Truncated archive entry");
        }
        if(!seen.SetEquals(allowed))throw new InvalidDataException("Missing package entries");
        foreach(var file in module.Files)
        {
            Paths.AllowedTarget(file.Path);
            LocalSources.Validate(file,staging);
            if(file.Ownership is not ("mod-authored" or "licensed" or "local-transform"))throw new InvalidDataException("Unapproved game-content ownership");
            if(file.Kind=="copy" && (!PlanBuilder.ValidHash(file.Sha256) || !Paths.Same(Paths.Hash(Paths.Safe(staging,file.Entry)),file.Sha256)))throw new InvalidDataException("Payload checksum mismatch");
            if(file.Kind is not ("copy" or "ini" or "recipe"))throw new InvalidDataException("Unsupported file operation");
            if(file.Kind=="copy" && file.Path.EndsWith(".xxx",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Original cooked packages must be reconstructed locally, never shipped whole");
        }
    }
    public static byte[] Reconstruct(string original,string staging,PackageFile file)
    {
        using var output=new MemoryStream();WriteReconstruction(original,staging,file,output);var result=output.ToArray();if(!Paths.Same(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(result)),file.Sha256))throw new InvalidDataException("Locally reconstructed output hash mismatch");return result;
    }
    public static void ReconstructTo(string original,string staging,PackageFile file,string target)
    {
        Paths.AssertNoLinks(target);Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        using(var output=new FileStream(target,FileMode.CreateNew,FileAccess.Write,FileShare.None)){WriteReconstruction(original,staging,file,output);output.Flush(true);}
        if(!Paths.Same(Paths.Hash(target),file.Sha256))throw new InvalidDataException("Locally reconstructed output hash mismatch");
    }
    private static void WriteReconstruction(string original,string staging,PackageFile file,Stream output)
    {
        if(file.Kind!="recipe" || file.Ownership!="local-transform" || !Paths.Same(Paths.HashOrNull(original),file.SourceSha256??file.BeforeSha256) || file.Recipe is not {Count:>0})throw new InvalidDataException("Local reconstruction preimage mismatch");
        var ownedSource=original;
        original=LocalDecoder.Decode(original,staging,file);
        foreach(var part in file.Recipe)
        {
            if(part.Length<0 || part.Offset<0 || part.Length>256*1024*1024 || output.Length+part.Length>int.MaxValue)throw new InvalidDataException("Unsafe reconstruction range");
            if(part.Source is "centurion-clip" or "centurion-shell" or "centurion-animation"){if(part.Offset!=0||part.Entry is not null)throw new InvalidDataException("Centurion operation accepts no package code");LocalImageGenerator.ApplyCenturion(original,ownedSource,staging,part.Source[10..],output,part.Length);continue;}
            if(part.Source=="knife"){if(part.Offset!=0)throw new InvalidDataException("Knife offset must be zero");LocalImageGenerator.ApplyKnife(original,staging,Paths.Safe(staging,part.Entry??""),output,part.Length);continue;}
            if(part.Source=="image"){if(part.Offset!=0)throw new InvalidDataException("Image offset must be zero");LocalImageGenerator.Apply(original,staging,Paths.Safe(staging,part.Entry??""),output,part.Length,ownedSource,file);continue;}
            if(part.Source=="audio"){if(part.Offset!=0||file.SourceCodec is not ("ue3-whole-lzo" or "ue3-logical-lzo"))throw new InvalidDataException("Audio requires decoded owned package and zero operation offset");LocalAudioGenerator.Apply(original,staging,Paths.Safe(staging,part.Entry??""),output,part.Length);continue;}
            if(part.Source=="instructions"){if(part.Offset!=0)throw new InvalidDataException("Recipe offset must be zero");BinaryRecipe.Apply(original,Paths.Safe(staging,part.Entry??""),output,part.Length);continue;}
            var source=part.Source switch{"original" or "xor-original"=>original,"payload"=>Paths.Safe(staging,part.Entry??""),_=>throw new InvalidDataException("Unknown reconstruction source")};
            using var input=File.OpenRead(source);if(part.Offset>input.Length-part.Length)throw new InvalidDataException("Reconstruction range exceeds source");input.Position=part.Offset;
            using var mask=part.Source=="xor-original"?File.OpenRead(Paths.Safe(staging,part.Entry??"")):null;
            if(mask is not null){if(part.Offset>mask.Length-part.Length)throw new InvalidDataException("Delta mask exceeds source");mask.Position=part.Offset;}
            var buffer=new byte[Math.Min(part.Length,81920)];var remain=part.Length;
            var maskBuffer=mask is null?null:new byte[buffer.Length];
            while(remain>0)
            {
                var n=input.Read(buffer,0,Math.Min(remain,buffer.Length));if(n==0)throw new EndOfStreamException();
                if(mask is not null){mask.ReadExactly(maskBuffer!,0,n);for(var i=0;i<n;i++)buffer[i]^=maskBuffer![i];}
                output.Write(buffer,0,n);remain-=n;
            }
        }
    }
}


