using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;

namespace Trilex.Installer.Core;

/// <summary>Runs only our embedded standalone GPL decoder, never an executable from a package.</summary>
public static class LocalDecoder
{
    public static string Decode(string source,string staging,PackageFile file)
    {
        if(file.SourceCodec is null)return source;
        LocalSources.Validate(file,staging);
        if(!Paths.Same(Paths.Hash(source),file.SourceSha256??file.BeforeSha256))throw new InvalidDataException("Compressed source hash mismatch");
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException("Local UE3 decoder requires Windows x64");
        var output=Paths.Safe(staging,"_local-decoded/"+file.DecodedSha256+".u");
        if(File.Exists(output)) {Verify(output,file);return output;}
        var assembly=typeof(LocalDecoder).Assembly;
        using var resource=assembly.GetManifestResourceStream("Trilex.Ue3Decode.exe")??throw new InvalidDataException("Bundled local decoder unavailable");
        using var bytes=new MemoryStream();resource.CopyTo(bytes);var code=bytes.ToArray();
        var hash=Convert.ToHexString(SHA256.HashData(code));
        // CreateProcess still rejects long executable paths on supported Windows builds.
        // Keep the verified helper in a short ancestor cache; data paths retain long-path support.
        var toolRoot=Path.GetFullPath(staging);
        while(toolRoot.Length>160)toolRoot=Path.GetDirectoryName(toolRoot)??throw new IOException("Cannot locate a short decoder cache path");
        var helper=Paths.Safe(toolRoot,"_local-tools/"+hash[..16]+"/Trilex.Ue3Decode.exe");
        if(!File.Exists(helper))Paths.AtomicBytes(helper,code);
        if(!Paths.Same(Paths.Hash(helper),hash))throw new InvalidDataException("Bundled decoder modified");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var start=new ProcessStartInfo(Extended(helper)){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=Path.GetPathRoot(helper)!};
        start.ArgumentList.Add(file.SourceCodec);start.ArgumentList.Add(Extended(source));start.ArgumentList.Add(Extended(output));
        if(file.SourceCodec=="ue3-bulk-lzo")
        {
            var size=new FileInfo(source).Length;
            if(file.SourceOffset>size-file.SourceBytes)throw new InvalidDataException("Bulk source range exceeds owned file");
            start.ArgumentList.Add(file.SourceOffset!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            start.ArgumentList.Add(file.SourceBytes!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        using var process=Process.Start(start)??throw new IOException("Cannot start bundled decoder");
        // Decoder receipts are bounded by the bundled implementation (not arbitrary package code).
        var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
        if(!process.WaitForExit(30000)){process.Kill(entireProcessTree:true);process.WaitForExit();throw new IOException("Local decoder timeout");}
        Task.WaitAll(stdout,stderr);
        if(process.ExitCode!=0)throw new InvalidDataException("Local original package decoding refused");
        Verify(output,file);return output;
    }
    private static string Extended(string path)
    {
        var full=Path.GetFullPath(path);return full.StartsWith(@"\\?\",StringComparison.Ordinal)?full:full.StartsWith(@"\\",StringComparison.Ordinal)?@"\\?\UNC\"+full[2..]:@"\\?\"+full;
    }
    private static void Verify(string path,PackageFile file)
    {
        if(new FileInfo(path).Length!=file.DecodedBytes||!Paths.Same(Paths.Hash(path),file.DecodedSha256))throw new InvalidDataException("Decoded original identity mismatch");
    }
}

