namespace Trilex.Installer.Core;

// Called only after explicit UI approval of these exact existing source bytes.
// Rebind the source identity, not the signed recipe, output identity or archive.
public static class DarknessRecipeConsent
{
    public static PackageFile Bind(string gameId,PackageFile file,string actualSourceHash)
    {
        if(gameId!="darkness2" || !PlanBuilder.ValidHash(actualSourceHash) ||
           !(Paths.DarknessAssetTarget(file.Path)||Paths.DarknessTextTarget(file.Path)) ||
           file.Kind!="recipe" || file.Ownership!="local-transform" ||
           file.SourcePath is not null || file.SystemSource is not null || file.SourceCodec is not null ||
           file.SourceOffset is not null || file.SourceBytes is not null || file.SourceSha256 is not null ||
           file.DecodedSha256 is not null || file.DecodedBytes is not null || file.UseCurrentSystemVersion ||
           file.Recipe is not {Count:>0} || file.Recipe.Any(p=>p.Source!="instructions"))
            throw new InvalidDataException("Unsupported consented source replacement: "+file.Path);
        return file with {BeforeSha256=actualSourceHash};
    }
}
