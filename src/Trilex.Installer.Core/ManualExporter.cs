using System.Text;

namespace Trilex.Installer.Core;

public sealed record ManualExportFile(string Path,string Sha256,string? PreviousSha256);
public sealed record ManualExportReceipt(string GameId,string Version,string SourceExeSha256,List<ManualExportFile> Files,List<ReconstructionWarning> Warnings);

// Prepares finished files, never a transaction against the source game. Only the
// fresh export tree is written; recipes share the production bounded decoder.
public static class ManualExporter
{
    public static string Export(InstallPlan plan,Dictionary<string,string> archives,string destination,
        TransactionInstaller originals,IProgress<ProgressInfo>? progress,CancellationToken ct,
        Func<ReconstructionWarning,bool>? confirmMismatch=null)
    {
        var output=Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar);
        var root=Path.GetFullPath(plan.Root).TrimEnd(Path.DirectorySeparatorChar);
        bool Inside(string a,string b)=>Paths.Same(a,b)||a.StartsWith(b+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);
        if(Inside(output,root)||Inside(root,output))throw new IOException("Export folder must be separate from the game folder");
        Paths.AssertNoLinks(output);Paths.AssertNoLinks(root);
        if(File.Exists(output)||Directory.Exists(output))throw new IOException("Choose a new, empty export folder");
        if(!Paths.Same(Paths.Hash(Paths.Safe(root,plan.Executable)),plan.ExeSha256))throw new IOException("Executable changed after review");
        if(plan.Modules.Any(m=>m.State!="stable"||!PlanBuilder.HasDistributionReview(m)))throw new InvalidDataException("Unreviewed export module");
        var partial=output+".partial-"+Guid.NewGuid().ToString("N");
        Paths.AssertNoLinks(partial);Directory.CreateDirectory(partial);
        try
        {
            var files=new Dictionary<string,(PackageFile File,string Stage)>(StringComparer.OrdinalIgnoreCase);
            foreach(var module in plan.Modules)
            {
                ct.ThrowIfCancellationRequested();
                if(!archives.TryGetValue(module.Id,out var archive))throw new FileNotFoundException("Missing export package: "+module.Id);
                var stage=Paths.Safe(partial,"work/"+module.Id);PackageExtractor.Extract(archive,stage,module);
                foreach(var file in module.Files)
                {
                    if(files.TryGetValue(file.Path,out var old))
                    {
                        if(file.Kind!="ini"||old.File.Kind!="ini")throw new InvalidDataException("Duplicate export owner");
                        var values=new Dictionary<string,string>(old.File.IniValues??[],StringComparer.OrdinalIgnoreCase);
                        foreach(var pair in file.IniValues??[])
                        {if(values.TryGetValue(pair.Key,out var value)&&value!=pair.Value)throw new InvalidDataException("Conflicting export INI values");values[pair.Key]=pair.Value;}
                        files[file.Path]=(file with{IniValues=values},stage);
                    }
                    else files.Add(file.Path,(file,stage));
                }
            }
            var previous=originals.Receipt(root);
            string Original(string path)
            {
                var owned=previous?.Files.SingleOrDefault(f=>Paths.Same(f.Path,path));
                if(owned?.OriginalHash is null)return Paths.Safe(root,path);
                var backup=Paths.Safe(originals.StateDirectory(root),owned.Backup);
                if(!Paths.Same(Paths.Hash(backup),owned.OriginalHash))throw new IOException("Original backup checksum mismatch: "+path);
                return backup;
            }
            var rows=new List<ManualExportFile>();var warnings=new List<ReconstructionWarning>();
            foreach(var (path,(file,stage)) in files)
            {
                ct.ThrowIfCancellationRequested();Paths.AllowedTarget(path);
                var target=Paths.Safe(partial,"files/"+path);var live=Paths.Safe(root,path);
                if(file.Kind=="ini")
                {
                    var ini=new IniDocument(File.Exists(live)?File.ReadAllText(live):"");
                    foreach(var (key,value) in file.IniValues??throw new InvalidDataException("Missing INI values"))ini.Set(key,value);
                    Paths.AtomicBytes(target,Encoding.UTF8.GetBytes(ini.ToString()));
                }
                else if(file.Kind=="recipe")
                {
                    var source=file.SystemSource is not null?LocalSources.SystemPath(file.SystemSource):Original(file.SourcePath??path);
                    if(Paths.Same(Paths.HashOrNull(source),file.Sha256))Paths.AtomicCopy(source,target);
                    else PackageExtractor.ReconstructTo(source,stage,file,target,w=>
                    {
                        ct.ThrowIfCancellationRequested();if(confirmMismatch?.Invoke(w)!=true)return false;
                        warnings.Add(w);return true;
                    });
                }
                else Paths.AtomicCopy(Paths.Safe(stage,file.Entry),target);
                var languageConfig=DarknessLanguageSettings.ApplyPrepared(plan,file,target);
                var hash=Paths.Hash(target);
                if(!languageConfig&&file.Kind!="ini"&&!Paths.Same(hash,file.Sha256)&&!warnings.Any(w=>Paths.Same(w.Path,path)&&Paths.Same(w.ActualSha256,hash)))throw new InvalidDataException("Export payload checksum mismatch: "+path);
                rows.Add(new(path,hash,Paths.HashOrNull(live)));progress?.Report(new("Prepare",path,rows.Count,files.Count));
            }
            ct.ThrowIfCancellationRequested();
            Json.Write(Paths.Safe(partial,"manual-manifest.json"),new ManualExportReceipt(plan.GameId,plan.Version,plan.ExeSha256,rows,warnings));
            File.WriteAllLines(Paths.Safe(partial,"SHA256SUMS.txt"),rows.Select(r=>$"{r.Sha256}  files/{r.Path}"));
            File.WriteAllText(Paths.Safe(partial,"README-EN.md"),Readme(plan,rows,false,warnings.Count),new UTF8Encoding(false));
            File.WriteAllText(Paths.Safe(partial,"README-PL.md"),Readme(plan,rows,true,warnings.Count),new UTF8Encoding(false));
            // No extracted recipe/source staging is part of the delivered manual kit.
            var work=Paths.Safe(partial,"work");Paths.AssertNoLinks(work);Directory.Delete(work,true);
            ct.ThrowIfCancellationRequested();Paths.AssertNoLinks(output);Directory.Move(partial,output);
            return output;
        }
        catch
        {
            // This exact tree was created above, outside the game. Never delete a
            // caller-provided pre-existing directory or follow reparse points.
            Paths.AssertNoLinks(partial);if(Directory.Exists(partial))Directory.Delete(partial,true);throw;
        }
    }
    private static string Readme(InstallPlan plan,List<ManualExportFile> rows,bool pl,int warnings)
    {
        var tiny=plan.GameId=="wonderlands";
        var requirements=tiny?"Tiny Tina's Wonderlands: DirectX 12 (-dx12) when WonderlandsVR.ini has [VR] D3D12=1; otherwise DirectX 11 (-dx11). OpenXR runtime (SteamVR). Optional PSVR2 Toolkit for Sense triggers/gaze. F9 or hold L3+R3 to recenter. Log: OakGame/Binaries/Win64/wvr.log.":plan.GameId=="darkness2"?"The Darkness II: x86, OpenXR runtime (SteamVR). Log: %LOCALAPPDATA%/Darkness2VR/d3d9.log.":"Singularity: x86 game plus x64 bridge, OpenXR runtime (SteamVR). Logs: Binaries/view_matrix.log and %LOCALAPPDATA%/SingularityVR/.";
        var text=pl?$"# Trilex — instalacja ręczna: {plan.GameId}\n\nWersja paczki: {plan.Version}. Instalator nie zmienił plików gry.\n\n1. Zamknij grę. Wykonaj osobną kopię zapisów i wszystkich plików docelowych z listy poniżej.\n2. Skopiuj ZAWARTOŚĆ folderu `files` do głównego folderu gry (nie sam folder `files`). Zachowaj strukturę podfolderów.\n3. Zatwierdź podmianę tylko po wykonaniu kopii. Dołączony INI zawiera wybrane ustawienia; porównaj go z własnym, jeśli masz inne mody.\n4. Uruchom SteamVR/OpenXR i grę. Nieznany hash EXE jest ostrzeżeniem, nie potwierdzeniem zgodności.\n5. Aby odinstalować, przywróć kopie oryginałów i usuń tylko pliki dodane z tej paczki. Tryb ręczny nie tworzy instalacji zarządzanej i nie zapewnia automatycznego odinstalowania.\n\n":$"# Trilex — manual installation: {plan.GameId}\n\nPackage version: {plan.Version}. The installer did not modify the game.\n\n1. Close the game. Back up saves separately and back up every existing destination file listed below.\n2. Copy the CONTENTS of `files` into the game ROOT, preserving all subfolders. Do not copy the `files` folder itself.\n3. Replace files only after backup. The INI includes your selected settings; compare it with existing mods/configuration.\n4. Start SteamVR/OpenXR and the game. An unknown EXE hash is a warning, not confirmed compatibility.\n5. To uninstall, restore your original backups and remove only newly added files from this kit. Manual mode creates no managed installation and cannot automatically uninstall it.\n\n";
        text+=$"Game executable relative to root: `{plan.Executable}`\n\n{requirements}\n\n";
        text+="Optional PSVR2 gaze/triggers: also follow psvr2-clients/README.txt. Those shared per-user files are separate from the game files and Toolkit SteamVR driver/calibration.\n\n";
        text+=pl?"Wymagane biblioteki Microsoft VC++ v14: ":"Microsoft VC++ v14 prerequisites: ";
        text+=tiny?"x64 (game); proxy uses static CRT.\n\n":"x86 (game/runtime); Singularity's PSVR2 bridge also needs x64.\n\n";
        text+=pl?"Pliki odtworzono dla tej kopii gry. Nie udostępniaj wygenerowanych danych gry jako uniwersalnej paczki. Nie kopiuj plików EXE gry.\n\n":"Reconstructed files are specific to your game sources. Do not redistribute generated game data as a universal package. No game executable is included.\n\n";
        if(warnings>0)text+=$"WARNING: {warnings} reconstruction output mismatch(es) explicitly accepted. See manual-manifest.json; compatibility unverified.\n\n";
        text+="## Files / Pliki\n\n"+string.Join("\n",rows.Select(r=>"- `"+r.Path+"`"))+"\n\nSHA256SUMS.txt lists the finished payloads; manual-manifest.json records previous and output hashes.\n";
        return text;
    }
}
