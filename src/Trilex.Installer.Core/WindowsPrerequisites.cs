using System.Diagnostics;
namespace Trilex.Installer.Core;

public sealed record PrerequisiteResult(bool Present,string Detail);
public static class WindowsPrerequisites
{
    public const string VisualCppX86Url="https://aka.ms/vc14/vc_redist.x86.exe";
    public const string VisualCppX64Url="https://aka.ms/vc14/vc_redist.x64.exe";
    public static PrerequisiteResult Inspect()=>InspectDirectory(Environment.GetFolderPath(Environment.SpecialFolder.SystemX86));
    public static PrerequisiteResult InspectDirectory(string directory)=>InspectFiles(directory,false,["vcruntime140.dll","msvcp140.dll"]);
    // Exact imports of the accepted PE32+ trigger helper, separate from UE3 x86.
    public static PrerequisiteResult InspectTriggerHelper()=>InspectTriggerDirectory(Environment.GetFolderPath(Environment.SpecialFolder.System));
    public static PrerequisiteResult InspectTriggerDirectory(string directory)=>InspectFiles(directory,true,["vcruntime140.dll","vcruntime140_1.dll"]);
    private static PrerequisiteResult InspectFiles(string directory,bool x64,string[] names)
    {
        var arch=x64?"x64":"x86";
        if(string.IsNullOrWhiteSpace(directory))return new(false,"Windows "+arch+" system directory unavailable");
        var versions=new List<string>();
        foreach(var name in names)
        {
            var file=Paths.Safe(directory,name);
            try
            {
                // Read-only PE inspection. Never LoadLibrary an untrusted or wrong-architecture DLL.
                using var stream=File.OpenRead(file);using var reader=new BinaryReader(stream);
                if(stream.Length<64||reader.ReadUInt16()!=0x5a4d)return new(false,"Invalid "+arch+" runtime: "+name);
                stream.Position=60;var offset=reader.ReadInt32();if(offset<64||offset>stream.Length-26)return new(false,"Invalid "+arch+" runtime header: "+name);
                stream.Position=offset;if(reader.ReadUInt32()!=0x4550||reader.ReadUInt16()!=(x64?0x8664:0x14c))return new(false,"Expected "+arch+" runtime: "+name);
                stream.Position=offset+24;if(reader.ReadUInt16()!=(x64?0x20b:0x10b))return new(false,"Expected "+arch+" PE runtime: "+name);
                var version=FileVersionInfo.GetVersionInfo(file);if(version.FileMajorPart<14)return new(false,"VC++ v14 "+arch+" runtime required: "+name);
                versions.Add(name+" "+version.FileVersion);
            }
            catch(Exception e)when(e is IOException or UnauthorizedAccessException or ArgumentException){return new(false,"Missing/unreadable VC++ v14 "+arch+" runtime: "+name);}
        }
        return new(true,string.Join("; ",versions)+" (presence/architecture verified; gameplay compatibility not inferred)");
    }
}
