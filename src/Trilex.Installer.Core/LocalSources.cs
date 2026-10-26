namespace Trilex.Installer.Core;

public static class LocalSources
{
    public static void Validate(PackageFile file,string root)
    {
        if(file.SourcePath is not null && file.SystemSource is not null)throw new InvalidDataException("Ambiguous original source");
        if(file.SourcePath is not null){Paths.Safe(root,file.SourcePath);Paths.AllowedTarget(file.SourcePath);}
        if(file.SystemSource is not null && file.SystemSource!="windows-directinput8-x86")throw new InvalidDataException("Unknown system source");
        if(file.SourcePath is not null || file.SystemSource is not null)
        {
            if(file.Kind!="recipe" || file.Ownership!="local-transform" || !PlanBuilder.ValidHash(file.SourceSha256))throw new InvalidDataException("Invalid local source declaration");
        }
        else if(file.SourceSha256 is not null)throw new InvalidDataException("Local source hash requires a source identity");
    }
    public static string SystemPath(string identity)
    {
        if(identity!="windows-directinput8-x86" || !OperatingSystem.IsWindows() || !Environment.Is64BitOperatingSystem)throw new InvalidDataException("Unsupported system source");
        var folder=Environment.GetFolderPath(Environment.SpecialFolder.SystemX86);
        if(string.IsNullOrWhiteSpace(folder))throw new InvalidDataException("Windows x86 system directory unavailable");
        var source=Path.Combine(folder,"dinput8.dll");Paths.AssertNoLinks(source);
        return source;
    }
}
