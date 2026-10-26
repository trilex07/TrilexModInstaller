using System.Text.RegularExpressions;

namespace Trilex.Installer.Core;

// Small strict KeyValues parser, including nested library folders and escaped paths.
public static class Vdf
{
    public static Dictionary<string,object> Parse(string text)
    {
        var tokens=Regex.Matches(Regex.Replace(text,@"(?m)^\s*//.*$",""),"\"(?:\\\\.|[^\"\\\\])*\"|[{}]").Select(x=>x.Value).ToArray();int i=0;
        string Value(string s)=>s[1..^1].Replace("\\\\","\\").Replace("\\\"","\"");
        Dictionary<string,object> Block(bool nested)
        {
            var map=new Dictionary<string,object>(StringComparer.OrdinalIgnoreCase);
            while(i<tokens.Length && tokens[i]!="}")
            {
                var key=tokens[i++];if(!key.StartsWith('"') || i>=tokens.Length)throw new InvalidDataException("Malformed Steam VDF");
                var next=tokens[i++];object value;if(next=="{")value=Block(true);else if(next.StartsWith('"'))value=Value(next);else throw new InvalidDataException("Malformed value");
                if(!map.TryAdd(Value(key),value))throw new InvalidDataException("Duplicate Steam VDF key");
            }
            if(nested){if(i>=tokens.Length||tokens[i++]!="}")throw new InvalidDataException("Unclosed Steam VDF");}
            return map;
        }
        var result=Block(false);if(i!=tokens.Length)throw new InvalidDataException("Unexpected Steam VDF token");return result;
    }
}
public interface IGameAdapter
{
    string Id { get; }
    IEnumerable<GameLocation> Detect(IEnumerable<string> steamRoots,GameManifest manifest);
    GameLocation Validate(string root,GameManifest manifest);
}
public sealed class SingularityGameAdapter : IGameAdapter
{
    public string Id=>"singularity";
    public IEnumerable<GameLocation> Detect(IEnumerable<string> steamRoots,GameManifest manifest)
    {
        var libraries=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var root in steamRoots.Where(Directory.Exists))
        {
            libraries.Add(Path.GetFullPath(root));
            var vdf=Path.Combine(root,"steamapps","libraryfolders.vdf");if(!File.Exists(vdf))continue;
            Dictionary<string,object> parsed;try{parsed=Vdf.Parse(File.ReadAllText(vdf));}catch(InvalidDataException){continue;}
            if(parsed.GetValueOrDefault("libraryfolders") is Dictionary<string,object> entries)
                foreach(var (key,value) in entries)if(int.TryParse(key,out _)){if(value is Dictionary<string,object> child && child.GetValueOrDefault("path") is string path)libraries.Add(path);else if(value is string old)libraries.Add(old);}
        }
        foreach(var library in libraries)
        {
            var app=Path.Combine(library,"steamapps","appmanifest_"+manifest.SteamAppId+".acf");if(!File.Exists(app))continue;
            Dictionary<string,object> parsed;try{parsed=Vdf.Parse(File.ReadAllText(app));}catch(InvalidDataException){continue;}
            if(parsed.GetValueOrDefault("AppState") is not Dictionary<string,object> state || state.GetValueOrDefault("appid") as string != manifest.SteamAppId || state.GetValueOrDefault("installdir") is not string dir)continue;
            string root;try{root=Paths.Safe(Path.Combine(library,"steamapps","common"),dir);}catch(Exception e)when(e is IOException or InvalidDataException){continue;}
            yield return Validate(root,manifest) with {Source="Steam appmanifest "+manifest.SteamAppId};
        }
    }
    public GameLocation Validate(string root,GameManifest manifest)
    {
        try
        {
            var exe=Paths.Safe(root,manifest.Executable);
            if(!File.Exists(exe) || !Directory.Exists(Paths.Safe(root,"RvGame/CookedPC")))return new(root,"Manual",false,"Missing executable or CookedPC directory",null);
            var hash=Paths.Hash(exe);var valid=manifest.ExeSha256.Any(x=>Paths.Same(x,hash));
            return new(Path.GetFullPath(root),"Manual",valid,valid?"Compatible executable fingerprint":"Unsupported executable fingerprint",hash);
        }
        catch(Exception e)when(e is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException){return new(root,"Manual",false,e.Message,null);}
    }
}
public sealed class Darkness2GameAdapter : IGameAdapter
{
    public string Id=>"darkness2";
    public IEnumerable<GameLocation> Detect(IEnumerable<string> roots,GameManifest manifest)=>[];
    public GameLocation Validate(string root,GameManifest manifest)=>new(root,"Manual",false,"Coming Soon — adapter not enabled",null);
}
