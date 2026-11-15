using System.Reflection.PortableExecutable;
using System.Security.Cryptography;

namespace Trilex.Installer.Core;

// Profiles and package identities are inside the publisher-signed catalog.
// The default module set remains the legacy fallback for consented unknown EXEs.
public static class GameBuildRouting
{
    public static void Validate(GameManifest game)
    {
        if(game.BuildProfiles is not {Count: >0} profiles)return;
        if(game.Id!="wonderlands" || profiles.Count>8 || profiles.Select(p=>p.Id).Distinct(StringComparer.Ordinal).Count()!=profiles.Count)
            throw new InvalidDataException("Invalid build profile set");
        foreach(var profile in profiles)
        {
            if(string.IsNullOrWhiteSpace(profile.Id) || profile.Id.Length>80 || profile.Probes.Count is <3 or >16 ||
               profile.Probes.Select(p=>p.Rva).Distinct().Count()!=profile.Probes.Count ||
               profile.Probes.Any(p=>p.Rva<1 || p.Length is <16 or >256 || !PlanBuilder.ValidHash(p.Sha256)))
                throw new InvalidDataException("Invalid executable profile probes");
            if(!profile.Modules.Select(m=>m.Id).Order(StringComparer.Ordinal).SequenceEqual(game.Modules.Select(m=>m.Id).Order(StringComparer.Ordinal)))
                throw new InvalidDataException("Build profiles must preserve module choices");
            foreach(var module in profile.Modules)
            {
                var original=game.Modules.Single(m=>m.Id==module.Id);
                if(module.Required!=original.Required || !module.Dependencies.Order(StringComparer.Ordinal).SequenceEqual(original.Dependencies.Order(StringComparer.Ordinal)))
                    throw new InvalidDataException("Build profiles must preserve required modules and dependencies");
            }
            // Validate the complete alternate package set by exactly the same rules.
            PlanBuilder.ValidateManifest(game with{Modules=profile.Modules,BuildProfiles=null});
        }
    }

    public static GameManifest Select(GameManifest game,GameLocation location,string? requestedProfile=null)
    {
        var match=DetectProfile(game,location);
        if(requestedProfile is not null) {
            var requested=game.BuildProfiles?.SingleOrDefault(p=>p.Id==requestedProfile)
                ??throw new InvalidDataException("Requested game version is not in the signed catalog");
            if(match is not null && match.Id!=requested.Id)
                throw new InvalidDataException("Selected game version conflicts with executable code; use automatic detection");
            match=requested;
        }
        return match is null?game:game with{Modules=match.Modules,BuildProfiles=null};
    }

    public static GameBuildProfile? DetectProfile(GameManifest game,GameLocation location)
    {
        if(game.BuildProfiles is not {Count: >0} profiles)return null;
        Validate(game);
        var path=Paths.Safe(location.Root,game.Executable);
        using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        if(!Paths.Same(Convert.ToHexString(SHA256.HashData(stream)),location.ExeHash))
            throw new IOException("Executable changed after review");
        stream.Position=0;
        using var pe=new PEReader(stream,PEStreamOptions.LeaveOpen);
        if(pe.PEHeaders.CoffHeader.Machine!=System.Reflection.PortableExecutable.Machine.Amd64 ||
           pe.PEHeaders.PEHeader?.Magic!=PEMagic.PE32Plus || pe.PEHeaders.CorHeader is not null)
            throw new InvalidDataException("Build selection requires a native x64 executable");
        var matches=new List<GameBuildProfile>();
        var partial=false;
        foreach(var profile in profiles)
        {
            var count=profile.Probes.Count(probe=>Matches(pe,probe));
            if(count==profile.Probes.Count)matches.Add(profile);
            else if(count>0)partial=true;
        }
        if(matches.Count>1 || matches.Count==0&&partial)
            throw new InvalidDataException("Ambiguous or incomplete executable build profile; no package selected");
        return matches.SingleOrDefault();
    }

    private static bool Matches(PEReader pe,ExecutableProbe probe)
    {
        // Only hash raw bytes wholly contained in an executable section, never
        // zero-filled virtual tails or the headers. No code is loaded/executed.
        var section=pe.PEHeaders.SectionHeaders.FirstOrDefault(s=>
            (s.SectionCharacteristics&SectionCharacteristics.MemExecute)!=0 &&
            probe.Rva>=s.VirtualAddress && (long)probe.Rva+probe.Length<=(long)s.VirtualAddress+s.SizeOfRawData);
        if(section.Name is null)return false;
        try
        {
            var bytes=pe.GetSectionData(probe.Rva).GetContent(0,probe.Length);
            return Paths.Same(Convert.ToHexString(SHA256.HashData(bytes.AsSpan())),probe.Sha256);
        }
        catch(Exception e)when(e is BadImageFormatException or ArgumentOutOfRangeException){return false;}
    }
}
