using System.Security.Cryptography;

namespace Trilex.Installer.Core;

public static class Paths
{
    public static string Hash(string path) { using var file = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(file)); }
    public static bool Same(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    public static string? HashOrNull(string path) => File.Exists(path) ? Hash(path) : null;
    public static string Safe(string root, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || relative.Contains(':') || relative.StartsWith('/') || relative.StartsWith('\\') || Path.IsPathRooted(relative)) throw new InvalidDataException("Unsafe relative path");
        var parts = relative.Replace('\\','/').Split('/');
        foreach (var part in parts)
        {
            if (part is "" or "." or ".." || part.EndsWith('.') || part.EndsWith(' ') || part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || System.Text.RegularExpressions.Regex.IsMatch(part, @"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\.|$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) throw new InvalidDataException("Invalid path component");
        }
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(fullRoot, Path.Combine(parts)));
        if (!full.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Path escapes root");
        AssertNoLinks(fullRoot);
        var walk = fullRoot;
        foreach (var part in parts) { walk = Path.Combine(walk, part); AssertNoLinks(walk); }
        return full;
    }
    public static void AssertNoLinks(string path)
    {
        var current = Path.GetFullPath(path);
        while (!string.IsNullOrEmpty(current))
        {
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new IOException("Reparse point is not allowed in installation paths");
            current = Path.GetDirectoryName(current)!;
        }
    }
    public static void AllowedTarget(string relative)
    {
        var norm = relative.Replace('\\','/');
        // Source-owned, catalog-hash-pinned PSVR2 IPC helper is an explicit
        // runtime dependency. Never permit replacing the game/other EXEs.
        var triggerHelper=string.Equals(norm,"Binaries/SingularityPsvr2Bridge.exe",StringComparison.OrdinalIgnoreCase);
        if (norm.EndsWith(".exe",StringComparison.OrdinalIgnoreCase) && !triggerHelper || norm.StartsWith("saves/",StringComparison.OrdinalIgnoreCase) || !(norm.StartsWith("Binaries/",StringComparison.OrdinalIgnoreCase) || norm.StartsWith("RvGame/",StringComparison.OrdinalIgnoreCase))) throw new InvalidDataException("Target is not a mod installation path");
    }
    public static void AtomicBytes(string target, byte[] bytes)
    {
        AssertNoLinks(target); Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var temp = target + ".trilex-" + Guid.NewGuid().ToString("N") + ".tmp";
        try { using (var f = new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)) { f.Write(bytes); f.Flush(true); } File.Move(temp,target,true); }
        finally { if(File.Exists(temp))File.Delete(temp); }
    }
    public static void AtomicCopy(string source,string target)
    {
        AssertNoLinks(source);AssertNoLinks(target);Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var temp=target+".trilex-"+Guid.NewGuid().ToString("N")+".tmp";
        try{using(var input=File.OpenRead(source))using(var output=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){input.CopyTo(output);output.Flush(true);}File.Move(temp,target,true);}
        finally{if(File.Exists(temp))File.Delete(temp);}
    }
}
