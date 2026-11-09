using System.Text.RegularExpressions;

namespace Trilex.Installer.Core;

// Per-user clients only. This does not install/replace Sony or SteamVR drivers.
// Shared by the three mods; deliberately retained when one game is uninstalled.
public static class Psvr2ClientRuntime
{
    public const string Loader = "Trilex/PSVR2Toolkit/capi/psvr2_toolkit_capi_loader.dll";
    public const string DarknessLoader = "Darkness2VR/runtime/psvr2/psvr2_toolkit_capi_loader.dll";
    public const string DarknessBridge = "Darkness2VR/runtime/psvr2/Darkness2Psvr2Bridge.exe";
    public const string Notice = "Trilex/PSVR2Toolkit/THIRD-PARTY-PSVR2.txt";
    public static readonly string[] Files = [Loader, DarknessLoader, DarknessBridge, Notice];
    private const string State = "Trilex/PSVR2Toolkit";

    public static string LoaderPath(string localAppData) => Paths.Safe(localAppData, Loader);

    public static List<ModuleManifest> Configure(string gameId,List<ModuleManifest> modules,string localAppData)
    {
        if(gameId!="wonderlands")return modules;
        return modules.Select(m=>m with {Files=m.Files.Select(f=>f.Kind=="ini" &&
            Paths.Same(f.Path,"OakGame/Binaries/Win64/WonderlandsVR.ini")
                ? f with {IniValues=new Dictionary<string,string>(f.IniValues??[],StringComparer.OrdinalIgnoreCase)
                    {["Haptics/ToolkitLoader"]=LoaderPath(localAppData)}} : f).ToList()}).ToList();
    }

    public static void Install(string localAppData,IReadOnlyDictionary<string,byte[]> payload,Action requireIdle)
    {
        if(!Files.ToHashSet(StringComparer.Ordinal).SetEquals(payload.Keys))throw new InvalidDataException("Unexpected PSVR2 client payload");
        requireIdle();
        var state=Paths.Safe(localAppData,State);Directory.CreateDirectory(state);
        using var gate=new FileStream(Paths.Safe(state,"clients.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        var journal=Paths.Safe(state,"clients-pending.json");
        if(File.Exists(journal))Restore(localAppData,state,journal,requireIdle);
        var expected=payload.ToDictionary(p=>p.Key,p=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(p.Value)));
        if(Files.All(n=>Paths.Same(Paths.HashOrNull(Paths.Safe(localAppData,n)),expected[n])))return;
        var backupName="clients-backup-"+Guid.NewGuid().ToString("N");
        var backup=Paths.Safe(state,backupName);Directory.CreateDirectory(backup);
        var previous=Files.ToDictionary(n=>n,n=>Paths.HashOrNull(Paths.Safe(localAppData,n)));
        foreach(var (name,hash) in previous)if(hash is not null)
        {
            Paths.AtomicCopy(Paths.Safe(localAppData,name),Paths.Safe(backup,name));
            if(!Paths.Same(Paths.Hash(Paths.Safe(backup,name)),hash))throw new IOException("PSVR2 client backup verification failed");
        }
        Json.Write(journal,new ToolkitJournal(backupName,previous,expected));
        try
        {
            requireIdle();
            foreach(var name in Files)
            {
                Paths.AtomicBytes(Paths.Safe(localAppData,name),payload[name]);
                if(!Paths.Same(Paths.Hash(Paths.Safe(localAppData,name)),expected[name]))throw new IOException("PSVR2 client verification failed: "+name);
            }
            Json.Write(Paths.Safe(state,"clients-installed.json"),expected);
            File.Delete(journal);
        }
        catch { Restore(localAppData,state,journal,requireIdle);throw; }
    }

    private static void Restore(string root,string state,string journal,Action requireIdle)
    {
        requireIdle();var saved=Json.Read<ToolkitJournal>(journal);
        if(!Regex.IsMatch(saved.Backup,"^clients-backup-[a-f0-9]{32}$") ||
            !Files.ToHashSet().SetEquals(saved.Previous.Keys) || !Files.ToHashSet().SetEquals(saved.Expected.Keys))
            throw new InvalidDataException("Invalid PSVR2 client recovery journal");
        var backup=Paths.Safe(state,saved.Backup);
        foreach(var name in Files)
        {
            var old=saved.Previous[name];var current=Paths.HashOrNull(Paths.Safe(root,name));
            if(old is not null && !Paths.Same(Paths.Hash(Paths.Safe(backup,name)),old))throw new IOException("PSVR2 client recovery backup changed");
            if(current is not null && !Paths.Same(current,old) && !Paths.Same(current,saved.Expected[name]))
                throw new IOException("PSVR2 client recovery refused external change: "+name);
        }
        foreach(var name in Files)
        {
            var target=Paths.Safe(root,name);
            if(Paths.Same(Paths.HashOrNull(target),saved.Previous[name]))continue;
            if(saved.Previous[name] is string hash)
            {
                Paths.AtomicCopy(Paths.Safe(backup,name),target);
                if(!Paths.Same(Paths.Hash(target),hash))throw new IOException("PSVR2 client rollback verification failed");
            }
            else if(File.Exists(target))File.Delete(target);
        }
        File.Delete(journal);
    }
}
