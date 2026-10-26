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
    public static string? ArchiveAt(string root,ModuleManifest module)
    {
        if(!System.Text.RegularExpressions.Regex.IsMatch(module.Id,"^[a-z0-9][a-z0-9-]{0,63}$"))throw new InvalidDataException("Invalid bundle module ID");
        var file=Paths.Safe(root,"packages/"+module.Id+".zip");
        if(!File.Exists(file))return null;
        if(new FileInfo(file).Length!=module.DownloadBytes||!Paths.Same(Paths.Hash(file),module.ArchiveSha256))throw new InvalidDataException("Bundled package changed: "+module.Id);
        return file;
    }
}
