using System.Diagnostics;
using System.IO.Compression;

namespace Trilex.Installer.Core;

public static class DarknessTextureInstall
{
    private sealed record Generated(string Path,string Sha256,string BeforeSha256,long Bytes,long SourceBytes=0,string? Append=null,string? Payload=null);
    public static bool IsTarget(string path)=>new[]{"H.Texture","B.Texture","F.Texture"}
        .Any(stem=>Paths.Same(path,"Cache.Windows/"+stem+".cache")||Paths.Same(path,"Cache.Windows/"+stem+".toc"));

    public static async Task<(ModuleManifest Module,string Archive)> PrepareAsync(
        string root,TransactionInstaller installer,string workspace,string pngZip,CancellationToken ct)
    {
        var inputs=Path.Combine(workspace,"originals");Directory.CreateDirectory(inputs);
        var receipt=installer.Receipt(root);
        foreach(var stem in new[]{"H.Texture","B.Texture","F.Texture"})
        foreach(var ext in new[]{".cache",".toc"})
        {
            ct.ThrowIfCancellationRequested();
            var relative="Cache.Windows/"+stem+ext;
            var owner=receipt?.Files.SingleOrDefault(f=>Paths.Same(f.Path,relative));
            var source=owner is not null?Paths.Safe(installer.StateDirectory(root),owner.Backup):Paths.Safe(root,relative);
            if(owner is not null&&!Paths.Same(Paths.Hash(source),owner.OriginalHash))throw new IOException("Texture original backup mismatch: "+relative);
            var target=Path.Combine(inputs,stem+ext);
            await using var input=File.OpenRead(source);
            await using var output=new FileStream(target,FileMode.CreateNew);
            await input.CopyToAsync(output,ct);
        }
        var png=Path.Combine(workspace,"png");Directory.CreateDirectory(png);
        using(var zip=ZipFile.OpenRead(pngZip))
        foreach(var entry in zip.Entries.Where(e=>!e.FullName.EndsWith('/')))
        {
            var path=Paths.Safe(png,entry.FullName);Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var source=entry.Open();using var target=new FileStream(path,FileMode.CreateNew);source.CopyTo(target);
        }
        var worker=Path.Combine(workspace,"Trilex.DarknessTextures.exe");
        using(var resource=typeof(DarknessTextureInstall).Assembly.GetManifestResourceStream("Trilex.DarknessTextures.exe")??throw new IOException("Texture conversion worker is not bundled"))
        using(var output=new FileStream(worker,FileMode.CreateNew))resource.CopyTo(output);
        var generated=Path.Combine(workspace,"generated");
        var info=new ProcessStartInfo(worker){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var argument in new[]{inputs,png,generated})info.ArgumentList.Add(argument);
        using var process=Process.Start(info)??throw new IOException("Cannot start texture conversion");
        var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
        try{await process.WaitForExitAsync(ct);}
        catch{if(!process.HasExited)process.Kill(true);await process.WaitForExitAsync();throw;}
        var detail=await stderr;await stdout;
        if(process.ExitCode!=0)throw new IOException("Texture conversion failed: "+detail[^Math.Min(detail.Length,1600)..]);
        var rows=Json.Read<List<Generated>>(Path.Combine(generated,"result.json"));
        if(rows.Count!=6||rows.Select(x=>x.Path).Distinct().Count()!=6||rows.Any(x=>!IsTarget(x.Path)||!PlanBuilder.ValidHash(x.Sha256)))throw new InvalidDataException("Invalid local texture output");
        var files=new List<PackageFile>();
        var archive=Path.Combine(workspace,"hd-textures.zip");
        long expanded=0;
        using(var zip=ZipFile.Open(archive,ZipArchiveMode.Create))
        foreach(var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            var payload=row.Append??row.Payload??throw new InvalidDataException("Missing texture output");
            var source=Paths.Safe(generated,payload);
            var entry=zip.CreateEntry(payload,CompressionLevel.Fastest);
            using(var input=File.OpenRead(source))using(var output=entry.Open())input.CopyTo(output);
            expanded+=new FileInfo(source).Length;
            if(row.Append is null)
                files.Add(new(row.Path,payload,row.Sha256,"copy","local-transform",row.BeforeSha256));
            else
            {
                var parts=new List<PatchPart>();
                for(long at=0;at<row.SourceBytes;at+=256*1024*1024)
                    parts.Add(new("original",at,(int)Math.Min(256*1024*1024,row.SourceBytes-at)));
                var bytes=new FileInfo(source).Length;
                for(long at=0;at<bytes;at+=256*1024*1024)
                    parts.Add(new("payload",at,(int)Math.Min(256*1024*1024,bytes-at),payload));
                files.Add(new(row.Path,"",row.Sha256,"recipe","local-transform",row.BeforeSha256,Recipe:parts));
            }
        }
        var module=new ModuleManifest("hd-textures",new(){{"en","HD textures"},{"pl","Tekstury HD"}},"local-"+Guid.NewGuid().ToString("N"),"stable",false,["vr-core"],
            new FileInfo(archive).Length,null,Paths.Hash(archive),expanded,false,"",files,"",
            PublisherReview:new("fan-mod-publisher-approved","User-selected texture ZIP from fixed publisher Drive file, converted locally by embedded worker","Fan texture conversion from owned game data."));
        return(module,archive);
    }
}
