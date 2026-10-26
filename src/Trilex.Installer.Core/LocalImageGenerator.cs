using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace Trilex.Installer.Core;

public sealed record ImageBulkSlice(long Offset,long Bytes,string Sha256);
public sealed record ImageOperation(int Width,int Height,string Role,int Factor,string Fourcc,ImageBulkSlice? Diffuse=null,long? RawOffset=null);

/// <summary>Fixed embedded image generator; package metadata cannot supply executable code.</summary>
public static class LocalImageGenerator
{
    public static void Apply(string raw,string staging,string metadata,Stream output,int expectedLength,string? ownedSource=null,PackageFile? sourceFile=null)
    {
        var op=Json.Read<ImageOperation>(metadata);
        if(op.Width<4||op.Height<4||op.Width%4!=0||op.Height%4!=0||op.Width>4096||op.Height>4096||op.Factor is not (1 or 2 or 4)||op.Role is not ("diffuse" or "normal" or "svrt" or "weapon-diffuse-v2" or "weapon-normal-v2" or "weapon-normal-procedural-v2")||op.Fourcc is not ("DXT1" or "DXT5")||checked((long)op.Width*op.Height*op.Factor*op.Factor)>16777216)throw new InvalidDataException("Unsupported bounded image recipe");
        var rawLength=checked((long)(op.Width/4)*(op.Height/4)*(op.Fourcc=="DXT1"?8:16));
        if(op.RawOffset is { } rawOffset)
        {
            if(op.Role!="svrt"||rawOffset<0||rawOffset>new FileInfo(raw).Length-rawLength)throw new InvalidDataException("Invalid inline original texture range");
            var inlineSlice=Paths.Safe(staging,"_local-images/"+Guid.NewGuid().ToString("N")+".raw");Directory.CreateDirectory(Path.GetDirectoryName(inlineSlice)!);
            using(var inlineInput=File.OpenRead(raw))using(var target=new FileStream(inlineSlice,FileMode.CreateNew)){inlineInput.Position=rawOffset;var block=new byte[checked((int)rawLength)];inlineInput.ReadExactly(block);target.Write(block);}raw=inlineSlice;
        }
        if(new FileInfo(raw).Length!=rawLength)throw new InvalidDataException("Original image block length mismatch");
        if(op.Role=="normal"&&op.Factor!=2||op.Role=="svrt"&&op.Factor!=1)throw new InvalidDataException("Unsupported image factor");
        if(op.Role.StartsWith("weapon-",StringComparison.Ordinal)&&op.Factor!=2)throw new InvalidDataException("Weapon image factor must be two");
        if((op.Role=="weapon-normal-v2")!=(op.Diffuse is not null))throw new InvalidDataException("Paired diffuse declaration mismatch");
        var folder=Paths.Safe(staging,"_local-images/"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        var input=Path.Combine(folder,"input.dds");var generated=Path.Combine(folder,"output.bin");
        WriteDds(raw,input,op.Width,op.Height,op.Fourcc);
        string? paired=null;
        if(op.Diffuse is { } slice)
        {
            if(ownedSource is null||sourceFile?.SourceCodec!="ue3-bulk-lzo")throw new InvalidDataException("Paired image requires verified owned bulk source");
            var pairedFile=sourceFile with {SourceOffset=slice.Offset,SourceBytes=slice.Bytes,DecodedSha256=slice.Sha256,DecodedBytes=checked((long)op.Width*op.Height)};
            var pairedRaw=LocalDecoder.Decode(ownedSource,staging,pairedFile);
            paired=Path.Combine(folder,"diffuse.dds");WriteDds(pairedRaw,paired,op.Width,op.Height,"DXT5");
        }
        var arguments=new List<string>{input,generated,op.Role,op.Factor.ToString(System.Globalization.CultureInfo.InvariantCulture),op.Fourcc};
        if(paired is not null){arguments.Add("--diffuse");arguments.Add(paired);}
        Run(staging,generated,arguments,output,expectedLength);
    }
    public static void ApplyKnife(string source,string staging,string metadata,Stream output,int expectedLength)
    {
        var op=Json.Read<KnifeOperation>(metadata);
        if(op.Kind is not ("svrk" or "svnl")||op.Positions.Length!=662||op.Uvs.Length!=662)throw new InvalidDataException("Unsupported knife geometry");
        var length=new FileInfo(source).Length;
        if(op.Positions.Any(x=>x<0||x>length-12)||op.Uvs.Any(x=>x<0||x>length-4)||op.Indices<0||op.Indices>length-6060)throw new InvalidDataException("Knife geometry reference exceeds owned source");
        var expected=16+662*(op.Kind=="svrk"?24:48)+6060;
        if(expectedLength!=expected)throw new InvalidDataException("Knife output length mismatch");
        var folder=Paths.Safe(staging,"_local-images/"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);var generated=Path.Combine(folder,"knife.bin");
        Run(staging,generated,["--knife",source,generated,metadata],output,expectedLength);
    }
    private sealed record KnifeOperation(string Kind,long[] Positions,long[] Uvs,long Indices);
    public static void ApplyCenturion(string decoded,string ownedSource,string staging,string mode,Stream output,int expectedLength)
    {
        var length=mode switch{"clip"=>33136,"shell"=>3880,"animation"=>3208,_=>throw new InvalidDataException("Unknown Centurion operation")};
        if(expectedLength!=length)throw new InvalidDataException("Centurion output contract mismatch");
        var exports=LocalCenturionGenerator.Export(ownedSource,staging);
        var folder=Paths.Safe(staging,"_local-images/"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);var generated=Path.Combine(folder,"centurion.bin");
        Run(staging,generated,["--centurion",exports,generated,mode,decoded],output,expectedLength);
    }
    internal static void Run(string staging,string generated,IEnumerable<string> arguments,Stream output,int expectedLength)
    {
        using var resource=typeof(LocalImageGenerator).Assembly.GetManifestResourceStream("Trilex.AssetWorker.exe")??throw new InvalidDataException("Bundled image worker unavailable");
        using var memory=new MemoryStream();resource.CopyTo(memory);var code=memory.ToArray();var hash=Convert.ToHexString(SHA256.HashData(code));
        var root=Path.GetFullPath(staging);while(root.Length>150)root=Path.GetDirectoryName(root)??throw new IOException("Short worker cache unavailable");
        var helper=Paths.Safe(root,"_local-tools/"+hash[..16]+"/Trilex.AssetWorker.exe");
        if(!File.Exists(helper))Paths.AtomicBytes(helper,code);
        if(!Paths.Same(Paths.Hash(helper),hash))throw new InvalidDataException("Bundled image worker modified");
        var temp=Paths.Safe(root,"_local-image-temp");Directory.CreateDirectory(temp);
        var start=new ProcessStartInfo(helper){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=Path.GetPathRoot(helper)!};
        start.Environment["TEMP"]=temp;start.Environment["TMP"]=temp;
        // The frozen Python bootloader uses the Win32 path API. A full transaction
        // adds several directories beyond the shorter standalone producer fixtures.
        // Prefix absolute data paths; never prefix switches or numeric metadata.
        foreach(var arg in arguments)start.ArgumentList.Add(OperatingSystem.IsWindows()&&Path.IsPathFullyQualified(arg)?Extended(arg):arg);
        using var process=Process.Start(start)??throw new IOException("Cannot start image worker");
        var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
        if(!process.WaitForExit(120000)){process.Kill(true);process.WaitForExit();throw new IOException("Local image generation timeout");}
        Task.WaitAll(stdout,stderr);
        if(process.ExitCode!=0||!File.Exists(generated)||new FileInfo(generated).Length!=expectedLength)
        {
            var detail=stderr.Result; if(detail.Length>1600)detail=detail[^1600..];
            throw new InvalidDataException("Local image generation refused (exit "+process.ExitCode+", expected "+expectedLength+", actual "+(File.Exists(generated)?new FileInfo(generated).Length:-1)+"): "+detail);
        }
        using var result=File.OpenRead(generated);result.CopyTo(output);
    }
    private static void WriteDds(string raw,string path,int width,int height,string fourcc)
    {
        using var f=new FileStream(path,FileMode.CreateNew);
        var header=new byte[128];Encoding.ASCII.GetBytes("DDS ").CopyTo(header,0);
        void U(int at,int value)=>BitConverter.GetBytes(value).CopyTo(header,at);
        U(4,124);U(8,0x81007);U(12,height);U(16,width);U(20,checked((int)new FileInfo(raw).Length));U(28,1);U(76,32);U(80,4);Encoding.ASCII.GetBytes(fourcc).CopyTo(header,84);U(108,0x1000);
        f.Write(header);using var data=File.OpenRead(raw);data.CopyTo(f);
    }
    private static string Extended(string path)
    {
        var full=Path.GetFullPath(path);return full.StartsWith(@"\\?\",StringComparison.Ordinal)?full:full.StartsWith(@"\\",StringComparison.Ordinal)?@"\\?\UNC\"+full[2..]:@"\\?\"+full;
    }

}
