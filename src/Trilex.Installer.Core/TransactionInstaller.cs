using System.Security.Cryptography;
using System.Text;

namespace Trilex.Installer.Core;

public interface IProcessGuard { void RequireIdle(string root); }
public sealed class TransactionInstaller(string stateRoot, IProcessGuard processes)
{
    public string StateDirectory(string root)=>Paths.Safe(stateRoot,Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(root).ToUpperInvariant()))));
    private static string ReceiptPath(string state)=>Path.Combine(state,"installed.json");
    public InstallReceipt? Receipt(string root) { var path=ReceiptPath(StateDirectory(root));return File.Exists(path)?Json.Read<InstallReceipt>(path):null; }
    private FileStream Lock(string state)
    {
        Paths.AssertNoLinks(state);Directory.CreateDirectory(state);return new FileStream(Path.Combine(state,"transaction.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
    }
    public void Recover(string root)
    {
        var state=StateDirectory(root);using var gate=Lock(state);RecoverLocked(root,state);
    }
    private void RecoverLocked(string root,string state)
    {
        processes.RequireIdle(root);var path=Path.Combine(state,"journal.json");if(!File.Exists(path))return;
        var journal=Json.Read<Journal>(path);if(!Paths.Same(journal.Root,Path.GetFullPath(root)))throw new IOException("Journal belongs to another installation");
        if(journal.Phase=="committed")return;
        foreach(var file in journal.Files.AsEnumerable().Reverse())
        {
            processes.RequireIdle(root);var target=Paths.Safe(root,file.Path);var hash=Paths.HashOrNull(target);
            if(Paths.Same(hash,file.BeforeHash))continue;
            if(!Paths.Same(hash,file.AfterHash))throw new IOException("Rollback conflict: externally changed file "+file.Path);
            if(file.BeforeHash is null){if(File.Exists(target))File.Delete(target);}
            else {var backup=Paths.Safe(state,file.Backup);if(!Paths.Same(Paths.Hash(backup),file.BeforeHash))throw new IOException("Corrupt rollback backup");Paths.AtomicCopy(backup,target);}
        }
        var installed=ReceiptPath(state);
        if(journal.PreviousReceipt is null){if(File.Exists(installed))File.Delete(installed);}else Json.Write(installed,journal.PreviousReceipt);
        Json.Write(path,journal with {Phase="committed"});
    }
    public async Task<InstallReceipt> InstallAsync(InstallPlan plan,Dictionary<string,string> archives,IProgress<ProgressInfo>? progress,CancellationToken ct,Action<int>? faultInjection=null,bool repair=false)
    {
        // Caller constructs this plan through signed catalog validation; repeat ownership, game and package checks here.
        processes.RequireIdle(plan.Root);Paths.AssertNoLinks(plan.Root);
        if(!Paths.Same(Paths.Hash(Paths.Safe(plan.Root,plan.Executable)),plan.ExeSha256))throw new InvalidDataException("Executable changed after review");
        if(plan.Modules.Any(x=>x.State!="stable" || !x.RightsVerified))throw new InvalidDataException("Only approved stable modules can install");
        var state=StateDirectory(plan.Root);using var gate=Lock(state);RecoverLocked(plan.Root,state);
        var previous=Receipt(plan.Root);if(previous is not null && previous.GameId!=plan.GameId)throw new IOException("Adapter ownership mismatch");
        var id=Guid.NewGuid().ToString("N");var txn=Path.Combine(state,"transactions",id);Directory.CreateDirectory(txn);
        var existingBytes=plan.Modules.SelectMany(m=>m.Files).Select(f=>f.Path).Concat(previous?.Files.Select(f=>f.Path)??[]).Distinct(StringComparer.OrdinalIgnoreCase).Sum(p=>{var target=Paths.Safe(plan.Root,p);return File.Exists(target)?new FileInfo(target).Length:0;});
        var budgets=new Dictionary<string,long>(StringComparer.OrdinalIgnoreCase);
        void Reserve(string folder,long bytes){var drive=Path.GetPathRoot(Path.GetFullPath(folder))!;budgets[drive]=checked(budgets.GetValueOrDefault(drive)+bytes+16*1024*1024);}
        var decodedBytes=plan.Modules.Sum(m=>m.Files.Where(f=>f.SourceCodec is not null).GroupBy(f=>f.DecodedSha256).Sum(g=>g.Max(f=>f.DecodedBytes??0)));
        Reserve(plan.Root,checked(plan.ExpandedBytes+existingBytes));Reserve(state,checked(plan.ExpandedBytes*2+existingBytes*3+decodedBytes+plan.Modules.Count*2L*1024*1024));
        foreach(var (drive,bytes) in budgets)if(new DriveInfo(drive).AvailableFreeSpace<bytes)throw new IOException("Insufficient staging/backup space on "+drive);
        var files=new Dictionary<string,(PackageFile File,string Stage)>(StringComparer.OrdinalIgnoreCase);
        foreach(var module in plan.Modules)
        {
            ct.ThrowIfCancellationRequested();
            if(!archives.TryGetValue(module.Id,out var archive))throw new FileNotFoundException("Package missing: "+module.Id);
            var stage=Paths.Safe(txn,"packages/"+module.Id);PackageExtractor.Extract(archive,stage,module);
            foreach(var file in module.Files)
            {
                if(files.TryGetValue(file.Path,out var existing))
                {
                    if(file.Kind!="ini" || existing.File.Kind!="ini")throw new InvalidDataException("Duplicate file owner");
                    var values=new Dictionary<string,string>(existing.File.IniValues??[] ,StringComparer.OrdinalIgnoreCase);
                    foreach(var pair in file.IniValues??[])if(values.TryGetValue(pair.Key,out var v)&&v!=pair.Value)throw new InvalidDataException("Conflicting INI modules");else values[pair.Key]=pair.Value;
                    files[file.Path]=(file with {IniValues=values},stage);
                }
                else files.Add(file.Path,(file,stage));
            }
        }
        var edits=new List<(string Path,string? Staged)>();var ownership=new List<OwnedFile>();
        foreach(var (path,(file,stage)) in files)
        {
            ct.ThrowIfCancellationRequested();Paths.AllowedTarget(path);var target=Paths.Safe(plan.Root,path);var live=Paths.HashOrNull(target);
            var old=previous?.Files.SingleOrDefault(x=>Paths.Same(x.Path,path));
            var original=old is not null?old.OriginalHash:live;var originalBackup=old?.Backup??("originals/"+id+"/"+path);
            if(old is null && live is not null && file.Kind!="ini" && !Paths.Same(live,file.BeforeSha256) && !Paths.Same(live,file.Sha256))throw new IOException("Unowned file conflict: "+path);
            if(!repair && old is not null && file.Kind!="ini" && !Paths.Same(live,old.InstalledHash))throw new IOException("Modified installed file: "+path+"; use Repair explicitly");
            if(old is null && live is not null){var backup=Paths.Safe(state,originalBackup);Directory.CreateDirectory(Path.GetDirectoryName(backup)!);Paths.AtomicCopy(target,backup);}
            var prepared=Paths.Safe(txn,"prepared/"+path);Dictionary<string,string>? installedIni=null;Dictionary<string,string?>? originalIni=null;
            if(file.Kind=="ini")
            {
                var ini=new IniDocument(File.Exists(target)?File.ReadAllText(target):"");installedIni=[];originalIni=old?.OriginalIniValues is null?[]:new(old.OriginalIniValues);
                foreach(var (key,value) in file.IniValues??throw new InvalidDataException("INI values missing"))
                {
                    var current=ini.Get(key);
                    if(old?.IniValues?.TryGetValue(key,out var prior)==true && current!=prior){originalIni.Remove(key);continue;}
                    if(!originalIni.ContainsKey(key))originalIni[key]=current;
                    ini.Set(key,value);installedIni[key]=value;
                }
                // Remove deselected settings with three-way preservation.
                if(old?.IniValues is not null)foreach(var (key,value) in old.IniValues)if(!file.IniValues.ContainsKey(key) && ini.Get(key)==value){ini.Set(key,old.OriginalIniValues?.GetValueOrDefault(key));originalIni.Remove(key);}
                Paths.AtomicBytes(prepared,Encoding.UTF8.GetBytes(ini.ToString()));
            }
            else if(file.Kind=="recipe")
            {
                var source=original is null?target:Paths.Safe(state,originalBackup);
                if(file.SourcePath is not null)
                {
                    Paths.AllowedTarget(file.SourcePath);
                    // A prior managed replacement must use its retained original, never the new proxy.
                    var sourceOwner=previous?.Files.SingleOrDefault(x=>Paths.Same(x.Path,file.SourcePath));
                    source=sourceOwner?.OriginalHash is not null?Paths.Safe(state,sourceOwner.Backup):Paths.Safe(plan.Root,file.SourcePath);
                }
                if(file.SystemSource is not null)source=LocalSources.SystemPath(file.SystemSource);
                PackageExtractor.ReconstructTo(source,stage,file,prepared);
            }
            else Paths.AtomicCopy(Paths.Safe(stage,file.Entry),prepared);
            var after=Paths.Hash(prepared);var pristine=old is null || old.CanRestoreWhole&&Paths.Same(live,old.InstalledHash);
            ownership.Add(new(path,original,originalBackup,after,file.Kind,installedIni,originalIni,pristine));edits.Add((path,prepared));
        }
        // Deselecting a package restores the user's originals, rather than leaving orphaned files.
        if(previous is not null)foreach(var old in previous.Files.Where(x=>!files.ContainsKey(x.Path)))edits.Add((old.Path,RestoreSource(plan.Root,state,old,txn)));
        var receipt=new InstallReceipt("trilex-install-v1",Path.GetFullPath(plan.Root),plan.GameId,plan.Version,plan.Choices,ownership,id);
        Apply(plan.Root,state,edits,previous,receipt,id,progress,ct,faultInjection);
        await Task.CompletedTask;return receipt;
    }
    private string? RestoreSource(string root,string state,OwnedFile file,string txn)
    {
        var target=Paths.Safe(root,file.Path);
        if(file.Kind=="ini")
        {
            // An untouched managed INI can restore its exact original encoding/comments/bytes.
            if(file.CanRestoreWhole&&Paths.Same(Paths.HashOrNull(target),file.InstalledHash))
            {
                if(file.OriginalHash is null)return null;
                var original=Paths.Safe(state,file.Backup);if(!Paths.Same(Paths.Hash(original),file.OriginalHash))throw new IOException("Original INI backup checksum mismatch");return original;
            }
            var ini=new IniDocument(File.Exists(target)?File.ReadAllText(target):"");
            foreach(var (key,value) in file.IniValues??[])if(ini.Get(key)==value)ini.Set(key,file.OriginalIniValues?.GetValueOrDefault(key));
            if(file.OriginalHash is null && string.IsNullOrWhiteSpace(ini.ToString()))return null;
            var prepared=Paths.Safe(txn,"restore/"+file.Path);Paths.AtomicBytes(prepared,Encoding.UTF8.GetBytes(ini.ToString()));return prepared;
        }
        if(!Paths.Same(Paths.HashOrNull(target),file.InstalledHash))throw new IOException("Uninstall conflict; preserving externally modified file: "+file.Path);
        if(file.OriginalHash is null)return null;
        var backup=Paths.Safe(state,file.Backup);if(!Paths.Same(Paths.Hash(backup),file.OriginalHash))throw new IOException("Original backup checksum mismatch");return backup;
    }
    private void Apply(string root,string state,List<(string Path,string? Staged)> edits,InstallReceipt? previous,InstallReceipt? next,string id,IProgress<ProgressInfo>? progress,CancellationToken ct,Action<int>? inject)
    {
        processes.RequireIdle(root);var rows=new List<JournalFile>();
        foreach(var (path,staged) in edits)
        {
            Paths.AllowedTarget(path);var target=Paths.Safe(root,path);var before=Paths.HashOrNull(target);var backup="transactions/"+id+"/rollback/"+path;
            if(before is not null){var save=Paths.Safe(state,backup);Directory.CreateDirectory(Path.GetDirectoryName(save)!);Paths.AtomicCopy(target,save);}
            rows.Add(new(path,before,staged is null?null:Paths.Hash(staged),backup,false));
        }
        var journal=new Journal("trilex-journal-v1",Path.GetFullPath(root),"applying",id,previous,rows);var journalPath=Path.Combine(state,"journal.json");Json.Write(journalPath,journal);
        try
        {
            for(var i=0;i<edits.Count;i++)
            {
                ct.ThrowIfCancellationRequested();processes.RequireIdle(root);
                var target=Paths.Safe(root,edits[i].Path);if(!Paths.Same(Paths.HashOrNull(target),rows[i].BeforeHash))throw new IOException("File changed during transaction");
                if(edits[i].Staged is null){if(File.Exists(target))File.Delete(target);}else Paths.AtomicCopy(edits[i].Staged!,target);
                if(!Paths.Same(Paths.HashOrNull(target),rows[i].AfterHash))throw new IOException("Post-write checksum mismatch");
                rows[i]=rows[i] with {Applied=true};Json.Write(journalPath,journal);inject?.Invoke(i);progress?.Report(new("Install",edits[i].Path,i+1,edits.Count));
            }
            if(next is null){if(File.Exists(ReceiptPath(state)))File.Delete(ReceiptPath(state));}else Json.Write(ReceiptPath(state),next);
            Json.Write(journalPath,journal with {Phase="committed"});
        }
        catch
        {
            // If a game was started mid-transaction, leave the durable journal for recovery after exit.
            processes.RequireIdle(root);RecoverLocked(root,state);throw;
        }
    }
    public List<string> Verify(string root)
    {
        var receipt=Receipt(root)??throw new InvalidOperationException("No managed installation");var failures=new List<string>();
        foreach(var file in receipt.Files)
        {
            var target=Paths.Safe(root,file.Path);
            if(file.Kind=="ini")
            {
                if(!File.Exists(target)){failures.Add(file.Path);continue;}
                var ini=new IniDocument(File.ReadAllText(target));if((file.IniValues??[]).Any(p=>ini.Get(p.Key)!=p.Value))failures.Add(file.Path);
            }
            else if(!Paths.Same(Paths.HashOrNull(target),file.InstalledHash))failures.Add(file.Path);
        }
        return failures;
    }
    public void Uninstall(string root,CancellationToken ct)
    {
        var state=StateDirectory(root);using var gate=Lock(state);RecoverLocked(root,state);var previous=Receipt(root)??throw new InvalidOperationException("No managed installation");
        var id=Guid.NewGuid().ToString("N");var txn=Paths.Safe(state,"transactions/"+id);
        var edits=previous.Files.Select(f=>(f.Path,RestoreSource(root,state,f,txn))).ToList();Apply(root,state,edits,previous,null,id,null,ct,null);
    }
    public async Task<InstallReceipt> RepairAsync(InstallPlan plan,Dictionary<string,string> archives,IProgress<ProgressInfo>? progress,CancellationToken ct)
    {
        // Explicit repair acknowledges drift, but preserves the original uninstall backups.
        var state=StateDirectory(plan.Root);var damaged=Verify(plan.Root);
        if(damaged.Count==0)return Receipt(plan.Root)!;
        var previous=Receipt(plan.Root)!;
        foreach(var path in damaged)
        {
            var old=previous.Files.Single(x=>Paths.Same(x.Path,path));if(old.Kind=="ini")continue;
            if(!plan.Modules.SelectMany(x=>x.Files).Any(x=>Paths.Same(x.Path,path)))throw new IOException("Repair plan does not own damaged file");
        }
        // The same durable transaction snapshots damaged bytes; original uninstall backups stay unchanged.
        return await InstallAsync(plan,archives,progress,ct,repair:true);
    }
}

