using System.Diagnostics;
using System.IO.Compression;

namespace Trilex.Installer.Core;

public static class DarknessHandsInstall
{
    private sealed record Generated(string Path,string Sha256,string BeforeSha256,long SourceBytes=0,string? Append=null,string? Payload=null);
    public static bool IsTarget(string path)=>Paths.Same(path,"Cache.Windows/B.Misc.cache")||Paths.Same(path,"Cache.Windows/B.Misc.toc");

    public static async Task<(ModuleManifest Module,string Archive)> PrepareAsync(string root,TransactionInstaller installer,string workspace,CancellationToken ct)
    {
        var receipt=installer.Receipt(root);
        if(receipt is not null&&receipt.GameId!="darkness2")throw new IOException("Hand originals belong to another game");
        Directory.CreateDirectory(workspace);
        var inputs=new Dictionary<string,string>();
        foreach(var stem in new[]{"H.Misc","B.Misc"})foreach(var ext in new[]{".cache",".toc"})
        {
            ct.ThrowIfCancellationRequested();
            var relative="Cache.Windows/"+stem+ext;
            var owner=receipt?.Files.SingleOrDefault(f=>Paths.Same(f.Path,relative));
            var source=owner is not null?Paths.Safe(installer.StateDirectory(root),owner.Backup):Paths.Safe(root,relative);
            if(!File.Exists(source)||owner is not null&&!Paths.Same(Paths.Hash(source),owner.OriginalHash))throw new IOException("Hand original missing or backup mismatch: "+relative);
            inputs.Add(stem+ext,source);
        }
        var inputJson=Path.Combine(workspace,"inputs.json");Json.Write(inputJson,inputs);
        var worker=Path.Combine(workspace,"Trilex.DarknessTextures.exe");
        using(var resource=typeof(DarknessHandsInstall).Assembly.GetManifestResourceStream("Trilex.DarknessTextures.exe")??throw new IOException("Hand reconstruction worker is not bundled"))
        using(var output=new FileStream(worker,FileMode.CreateNew))resource.CopyTo(output);
        var generated=Path.Combine(workspace,"generated");
        var info=new ProcessStartInfo(worker){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var arg in new[]{"--hands",inputJson,generated})info.ArgumentList.Add(arg);
        using var process=Process.Start(info)??throw new IOException("Cannot start hand reconstruction");
        var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
        try{await process.WaitForExitAsync(ct);}
        catch{if(!process.HasExited)process.Kill(true);await process.WaitForExitAsync();throw;}
        var detail=await stderr;await stdout;
        if(process.ExitCode!=0)throw new IOException("Closed-wrist hand reconstruction failed: "+detail[^Math.Min(detail.Length,1600)..]);
        var rows=Json.Read<List<Generated>>(Path.Combine(generated,"result.json"));
        if(rows.Count!=2||rows.Select(x=>x.Path).Distinct().Count()!=2||rows.Any(x=>!IsTarget(x.Path)||!PlanBuilder.ValidHash(x.Sha256)||!PlanBuilder.ValidHash(x.BeforeSha256)))throw new InvalidDataException("Invalid local hand output");
        var files=new List<PackageFile>();var archive=Path.Combine(workspace,"hands-only.zip");long expanded=0;
        using(var zip=ZipFile.Open(archive,ZipArchiveMode.Create))foreach(var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            var name=row.Append??row.Payload??throw new InvalidDataException("Missing hand output");
            var payload=Paths.Safe(generated,name);var count=new FileInfo(payload).Length;
            if(count>8*1024*1024||row.Append is not null&&count is not (0 or 206112))throw new InvalidDataException("Invalid hand output size");
            var entry=zip.CreateEntry(name,CompressionLevel.Fastest);
            using(var input=File.OpenRead(payload))using(var output=entry.Open())input.CopyTo(output);
            var parts=new List<PatchPart>();
            if(row.Append is not null)
            {
                if(row.SourceBytes!=new FileInfo(inputs["B.Misc.cache"]).Length)throw new InvalidDataException("Hand source length changed");
                for(long at=0;at<row.SourceBytes;at+=256*1024*1024)parts.Add(new("original",at,(int)Math.Min(256*1024*1024,row.SourceBytes-at)));
            }
            parts.Add(new("payload",0,(int)count,name));
            files.Add(new(row.Path,"",row.Sha256,"recipe","local-transform",row.BeforeSha256,Recipe:parts));
            expanded+=row.SourceBytes+count;
        }
        var module=new ModuleManifest("hands-only",new(){{"en","Closed-wrist VR hands"},{"pl","Dłonie VR bez przedramion"}},"closed-wrist-1","stable",false,["vr-core"],
            new FileInfo(archive).Length,null,Paths.Hash(archive),expanded,false,"",files,"",
            PublisherReview:new("fan-mod-publisher-approved","User requested the existing locally accepted closed-wrist hands; source is reconstructed locally, never downloaded","Game model bytes remain on the player's machine; original third-party rights remain reserved."));
        return(module,archive);
    }
}
