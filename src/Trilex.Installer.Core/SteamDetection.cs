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
public abstract class SteamGameAdapter : IGameAdapter
{
    public abstract string Id { get; }
    protected abstract string DataDirectory { get; }
    public virtual IEnumerable<GameLocation> Detect(IEnumerable<string> steamRoots,GameManifest manifest)
    {
        foreach(var library in SteamRoots.Libraries(steamRoots))
        {
            var app=Path.Combine(library,"steamapps","appmanifest_"+manifest.SteamAppId+".acf");if(!File.Exists(app))continue;
            Dictionary<string,object> parsed;try{parsed=Vdf.Parse(File.ReadAllText(app));}catch(InvalidDataException){continue;}
            if(parsed.GetValueOrDefault("AppState") is not Dictionary<string,object> state || state.GetValueOrDefault("appid") as string != manifest.SteamAppId || state.GetValueOrDefault("installdir") is not string dir)continue;
            string root;try{root=Paths.Safe(Path.Combine(library,"steamapps","common"),dir);}catch(Exception e)when(e is IOException or InvalidDataException){continue;}
            yield return Validate(root,manifest) with {Source="Steam appmanifest "+manifest.SteamAppId};
        }
    }
    public virtual GameLocation Validate(string root,GameManifest manifest)
    {
        try
        {
            var exe=Paths.Safe(root,manifest.Executable);
            if(!File.Exists(exe) || !Directory.Exists(Paths.Safe(root,DataDirectory)))return new(root,"Manual",false,"Missing executable or game data directory",null);
            var hash=Paths.Hash(exe);var valid=manifest.ExeSha256.Any(x=>Paths.Same(x,hash));
            return new(Path.GetFullPath(root),"Manual",valid,valid?"Compatible executable fingerprint":"Unsupported executable fingerprint",hash);
        }
        catch(Exception e)when(e is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException){return new(root,"Manual",false,e.Message,null);}
    }
}
public sealed class SingularityGameAdapter : SteamGameAdapter
{
    public override string Id=>"singularity";
    protected override string DataDirectory=>"RvGame/CookedPC";
}
public sealed class Darkness2GameAdapter : SteamGameAdapter
{
    public override string Id=>"darkness2";
    protected override string DataDirectory=>"Cache.Windows";
}

public abstract class SteamEpicGameAdapter(string? epicManifests) : SteamGameAdapter
{
    protected override string DataDirectory=>"OakGame/Content";
    protected virtual bool SupportsEpic(GameManifest manifest)=>true;
    public override IEnumerable<GameLocation> Detect(IEnumerable<string> steamRoots,GameManifest manifest)
    {
        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var found in base.Detect(steamRoots,manifest))if(seen.Add(Path.GetFullPath(found.Root)))yield return found;
        if(!SupportsEpic(manifest))yield break;
        var directory=epicManifests??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"Epic","EpicGamesLauncher","Data","Manifests");
        foreach(var root in EpicGameLocations.ReadRoots(directory))
        {
            var found=Validate(root,manifest);
            if(found.ExeHash is not null&&seen.Add(Path.GetFullPath(found.Root)))yield return found with{Source="Epic Games manifest"};
        }
    }
}

public sealed class WonderlandsGameAdapter(string? epicManifests = null) : SteamEpicGameAdapter(epicManifests)
{
    public override string Id=>"wonderlands";
}

// Everybody's Gone to the Rapture (Steam 417880, CryEngine x64). RaptureVR is a dinput8 proxy in Bin64.
public sealed class RaptureGameAdapter : SteamGameAdapter
{
    public override string Id=>"rapture";
    protected override string DataDirectory=>"rapture";
}

public sealed class Borderlands3GameAdapter(string? epicManifests = null) : SteamEpicGameAdapter(epicManifests)
{
    public override string Id => Borderlands3Runtime.GameId;
    protected override bool SupportsEpic(GameManifest manifest)=>manifest.BuildProfiles?.Any(p=>p.Id=="epic")==true;

    public override GameLocation Validate(string root,GameManifest manifest)
    {
        var location=base.Validate(root,manifest);
        if(location.ExeHash is null || manifest.BuildProfiles is not {Count:>0})return location;
        try
        {
            var profile=GameBuildRouting.DetectProfile(manifest,location);
            return location with {
                Compatible=profile is not null,
                Detail=profile is null?"Unknown Borderlands 3 executable layout":"Compatible "+profile.Id+" executable layout"
            };
        }
        catch(Exception e)when(e is IOException or InvalidDataException or BadImageFormatException or UnauthorizedAccessException)
        {
            return location with {Compatible=false,Detail=e.Message};
        }
    }
}
