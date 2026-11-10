using System.Text.Json;

namespace Trilex.Installer.Core;

public static class EpicGameLocations
{
    public static IEnumerable<string> ReadRoots(string directory)
    {
        string[] files;
        try{files=Directory.EnumerateFiles(directory,"*.item",SearchOption.TopDirectoryOnly).Take(2048).ToArray();}
        catch(Exception e)when(e is IOException or UnauthorizedAccessException){yield break;}
        var roots=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var path in files)
        {
            string? root=null;
            try
            {
                using var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
                if(input.Length>256*1024)continue;
                // Bound the read as well as the initial length if the launcher rewrites it.
                var bytes=new byte[256*1024+1];var count=0;int read;
                while(count<bytes.Length&&(read=input.Read(bytes,count,bytes.Length-count))>0)count+=read;
                if(count>256*1024)continue;
                using var doc=JsonDocument.Parse(bytes.AsMemory(0,count));
                if(doc.RootElement.ValueKind==JsonValueKind.Object&&
                   doc.RootElement.TryGetProperty("InstallLocation",out var value)&&value.ValueKind==JsonValueKind.String&&
                   value.GetString() is string location&&Path.IsPathFullyQualified(location))root=Path.GetFullPath(location);
            }
            catch(Exception e)when(e is IOException or UnauthorizedAccessException or JsonException or ArgumentException){continue;}
            // A manifest is a location hint only. The adapter checks the real game
            // EXE/content and the normal signed fingerprint/consent policy next.
            if(root is not null&&roots.Add(root))yield return root;
        }
    }
}
