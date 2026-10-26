using System.Diagnostics;
namespace Trilex.Installer.Core;

public sealed record PrerequisiteResult(bool Present,string Detail);
public static class WindowsPrerequisites
{
    public const string VisualCppX86Url="https://aka.ms/vc14/vc_redist.x86.exe";
    public static PrerequisiteResult Inspect()=>InspectDirectory(Environment.GetFolderPath(Environment.SpecialFolder.SystemX86));
    public static PrerequisiteResult InspectDirectory(string directory)
    {
        if(string.IsNullOrWhiteSpace(directory))return new(false,"Windows x86 system directory unavailable");
        var versions=new List<string>();
        foreach(var name in new[]{"vcruntime140.dll","msvcp140.dll"})
        {
            var file=Paths.Safe(directory,name);
            try
            {
                // Read-only PE inspection. Never LoadLibrary an untrusted or wrong-architecture DLL.
                using var stream=File.OpenRead(file);using var reader=new BinaryReader(stream);
                if(stream.Length<64||reader.ReadUInt16()!=0x5a4d)return new(false,"Invalid x86 runtime: "+name);
                stream.Position=60;var offset=reader.ReadInt32();if(offset<64||offset>stream.Length-26)return new(false,"Invalid x86 runtime header: "+name);
                stream.Position=offset;if(reader.ReadUInt32()!=0x4550||reader.ReadUInt16()!=0x14c)return new(false,"Expected PE32 x86 runtime: "+name);
                stream.Position=offset+24;if(reader.ReadUInt16()!=0x10b)return new(false,"Expected PE32 runtime: "+name);
                var version=FileVersionInfo.GetVersionInfo(file);if(version.FileMajorPart<14)return new(false,"VC++ v14 x86 runtime required: "+name);
                versions.Add(name+" "+version.FileVersion);
            }
            catch(Exception e)when(e is IOException or UnauthorizedAccessException or ArgumentException){return new(false,"Missing/unreadable VC++ v14 x86 runtime: "+name);}
        }
        return new(true,string.Join("; ",versions)+" (presence/architecture verified; gameplay compatibility not inferred)");
    }
}
