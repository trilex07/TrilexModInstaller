namespace Trilex.Installer.Core;

public static class BundleSource
{
    public static Catalog? CatalogAt(string root,CatalogService service)
    {
        var file=Paths.Safe(root,"catalog.signed.json");
        if(!File.Exists(file))return null;
        if(new FileInfo(file).Length>CatalogService.MaximumCatalogBytes)throw new InvalidDataException("Bundle catalog exceeds budget");
        return service.Verify(File.ReadAllBytes(file));
    }
    public static string? ArchiveAt(string root,ModuleManifest module,string? gameId=null,Action<string>? rejected=null)
    {
        if(!System.Text.RegularExpressions.Regex.IsMatch(module.Id,"^[a-z0-9][a-z0-9-]{0,63}$"))throw new InvalidDataException("Invalid bundle module ID");
        var file=Paths.Safe(root,(gameId switch{"darkness2"=>"packages/darkness2/","wonderlands"=>"packages/wonderlands/",_=>"packages/"})+module.Id+".zip");
        if(!File.Exists(file))return null;
        if(new FileInfo(file).Length!=module.DownloadBytes||!Paths.Same(Paths.Hash(file),module.ArchiveSha256))
        {
            var detail="Bundled package changed: "+module.Id;
            if(rejected is null)throw new InvalidDataException(detail);
            // A newer signed catalog can legitimately supersede the portable ZIP.
            // Never install mismatched bytes: let the verified cache/downloader resolve it.
            rejected(detail+"; ignoring local ZIP and resolving signed package from cache/download");
            return null;
        }
        return file;
    }
}
