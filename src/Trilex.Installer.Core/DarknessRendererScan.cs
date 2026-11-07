using System.Reflection.PortableExecutable;

namespace Trilex.Installer.Core;

// These are discovery anchors only. They do not prove the surrounding ABI,
// native ownership, or the runtime's additional camera/data references.
public static class DarknessRendererScan
{
    private static readonly (string Name,string Hex)[] Anchors=[
        ("Present","0080526A006A006A006A00508B410CFFD0C64628005EC204008B461C"),
        ("Present alternate","088B510C6A006A006A006A006A0050FFD2C64628005EC20400CCCCCC"),
        ("Swap chain acquisition","8424840000008B08508B4134FFD03D2708768874243D7C017688741D"),
        ("Source size","8986C8010000660FD6442410FF1578B4EE008B54240C2B5424048B44"),
        ("Swap chain size","2418660FD644241C896C2424FF1578B4EE008B5424182B5424108B44"),
        ("View projection","0D1200F30F10842488040000F30F5905C885F1008D8424D005000050"),
        ("View frustum","0AE8FC88BBFFD99E840100008B078B08E83D010100508D8E90010000")
    ];
    public sealed record Match(string Name,int Count,int? Rva);
    public static IReadOnlyList<Match> Scan(string executable)
    {
        using var file=File.OpenRead(executable);
        if(file.Length is <64 or >64*1024*1024)throw new InvalidDataException("Executable exceeds scan bounds");
        using var pe=new PEReader(file);
        var headers=pe.PEHeaders;
        if(headers.PEHeader?.Magic!=PEMagic.PE32||headers.CoffHeader.Machine!=Machine.I386)
            throw new InvalidDataException("Expected x86 PE32");
        var sections=headers.SectionHeaders.Where(s=>(s.SectionCharacteristics&SectionCharacteristics.MemExecute)!=0)
            .Select(s=>(Rva:s.VirtualAddress,Bytes:pe.GetSectionData(s.VirtualAddress).GetContent())).ToArray();
        if(sections.Length==0)throw new InvalidDataException("No executable sections");
        return Anchors.Select(anchor=>
        {
            var signature=Convert.FromHexString(anchor.Hex);
            int count=0, rva=0;
            foreach(var section in sections)
            {
                var code=section.Bytes.AsSpan();
                for(int offset=0; offset<code.Length && count<2;)
                {
                    var hit=code[offset..].IndexOf(signature);
                    if(hit<0)break;
                    var match=offset+hit;
                    rva=checked(section.Rva+match+12);count++;offset=match+1;
                }
                if(count==2)break;
            }
            return new Match(anchor.Name,count,count==1?rva:null);
        }).ToArray();
    }
}
