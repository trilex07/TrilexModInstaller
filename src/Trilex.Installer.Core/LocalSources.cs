namespace Trilex.Installer.Core;

public static class LocalSources
{
    public static void Validate(PackageFile file,string root)
    {
        if(file.UseCurrentSystemVersion&&(file.SystemSource!="windows-directinput8-x86"||!Paths.Same(file.Path,"Binaries/dinput8_orig.dll")||file.Kind!="recipe"||file.SourcePath is not null||file.SourceCodec is not null||file.BeforeSha256 is not null||file.Recipe is not {Count:1}||file.Recipe[0] is not {Source:"original",Offset:0,Entry:null}))throw new InvalidDataException("Only the complete Windows x86 DirectInput dependency can bind to the current system version");
        if(file.SourceCodec is not null)
        {
            if(file.SourceCodec is not ("ue3-whole-lzo" or "ue3-logical-lzo" or "ue3-bulk-lzo") || file.Kind!="recipe" || file.Ownership!="local-transform" || file.SystemSource is not null || !PlanBuilder.ValidHash(file.SourceSha256??file.BeforeSha256) || !PlanBuilder.ValidHash(file.DecodedSha256) || file.DecodedBytes is not (>0 and <=536870912))throw new InvalidDataException("Invalid bounded local decoder declaration");
        }
        else if(file.SourceOffset is not null || file.SourceBytes is not null)throw new InvalidDataException("Source ranges require a bulk codec");
        else if(file.DecodedSha256 is not null || file.DecodedBytes is not null)throw new InvalidDataException("Decoded identity requires a codec");
        if(file.SourceCodec=="ue3-bulk-lzo") {if(file.SourceOffset is not (>=0) || file.SourceBytes is not (>=24 and <=536870912))throw new InvalidDataException("Invalid bounded bulk source range");}
        else if(file.SourceOffset is not null || file.SourceBytes is not null)throw new InvalidDataException("Unexpected source range");
        if(file.SourcePath is not null && file.SystemSource is not null)throw new InvalidDataException("Ambiguous original source");
        if(file.SourcePath is not null){Paths.Safe(root,file.SourcePath);Paths.AllowedTarget(file.SourcePath);}
        if(file.SystemSource is not null && file.SystemSource!="windows-directinput8-x86")throw new InvalidDataException("Unknown system source");
        if(file.SourcePath is not null || file.SystemSource is not null)
        {
            if(file.Kind!="recipe" || file.Ownership!="local-transform" || !PlanBuilder.ValidHash(file.SourceSha256))throw new InvalidDataException("Invalid local source declaration");
        }
        else if(file.SourceSha256 is not null)throw new InvalidDataException("Local source hash requires a source identity");
    }
    public static List<ModuleManifest> BindCurrentSystem(List<ModuleManifest> modules)=>modules.Select(module=>module with{Files=module.Files.Select(file=>
    {
        if(!file.UseCurrentSystemVersion)return file;
        Validate(file,Path.GetTempPath());var source=SystemPath(file.SystemSource!);
        // SystemX86 is resolved by Windows, never a catalog path, PATH search or user override.
        // Reject x64/corrupt DLLs and foreign version identity without loading the binary.
        using var input=File.OpenRead(source);using var reader=new BinaryReader(input);
        if(input.Length is <64 or >16*1024*1024||reader.ReadUInt16()!=0x5a4d)throw new InvalidDataException("Invalid Windows DirectInput dependency");
        input.Position=60;var offset=reader.ReadInt32();if(offset<64||offset>input.Length-26)throw new InvalidDataException("Invalid DirectInput PE header");
        input.Position=offset;if(reader.ReadUInt32()!=0x4550||reader.ReadUInt16()!=0x14c)throw new InvalidDataException("Windows x86 DirectInput required");
        input.Position=offset+24;if(reader.ReadUInt16()!=0x10b)throw new InvalidDataException("Windows PE32 DirectInput required");
        var version=System.Diagnostics.FileVersionInfo.GetVersionInfo(source);
        if(version.CompanyName!="Microsoft Corporation"||!Paths.Same(version.OriginalFilename,"DInput8.dll"))throw new InvalidDataException("Unexpected Windows DirectInput version identity");
        var hash=Paths.Hash(source);
        return file with{Sha256=hash,SourceSha256=hash,Recipe=[new("original",0,checked((int)input.Length))]};
    }).ToList()}).ToList();
    public static string SystemPath(string identity)
    {
        if(identity!="windows-directinput8-x86" || !OperatingSystem.IsWindows() || !Environment.Is64BitOperatingSystem)throw new InvalidDataException("Unsupported system source");
        var folder=Environment.GetFolderPath(Environment.SpecialFolder.SystemX86);
        if(string.IsNullOrWhiteSpace(folder))throw new InvalidDataException("Windows x86 system directory unavailable");
        var source=Path.Combine(folder,"dinput8.dll");Paths.AssertNoLinks(source);
        return source;
    }
}

