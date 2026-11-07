namespace Trilex.Installer.Core;

// Consent is scoped to one reviewed directory and exact executable bytes.
// It is not a declaration that a retail/GOG build works with native hooks.
public sealed record ExecutableConsent(string Root,string Sha256);
public static class ExecutableCompatibility
{
    public static bool CanTry(GameManifest game,GameLocation location)
    {
        IGameAdapter? adapter=(game.Id,game.Adapter) switch
        {
            ("singularity","SingularityGameAdapter")=>new SingularityGameAdapter(),
            ("darkness2","Darkness2GameAdapter")=>new Darkness2GameAdapter(),
            ("wonderlands","WonderlandsGameAdapter")=>new WonderlandsGameAdapter(),
            _=>null
        };
        if(adapter is null||location.Compatible||!PlanBuilder.ValidHash(location.ExeHash))return false;
        try
        {
            var actual=adapter.Validate(location.Root,game);
            if(actual.Compatible||!Paths.Same(actual.ExeHash,location.ExeHash))return false;
            using var input=File.OpenRead(Paths.Safe(location.Root,game.Executable));using var reader=new BinaryReader(input);
            if(input.Length<64||reader.ReadUInt16()!=0x5A4D)return false;
            input.Position=0x3C;var pe=reader.ReadInt32();if(pe<64||pe>input.Length-26)return false;
            input.Position=pe;if(reader.ReadUInt32()!=0x4550||reader.ReadUInt16()!=(game.Id=="wonderlands"?0x8664:0x14C))return false;
            input.Position=pe+24;return reader.ReadUInt16()==(game.Id=="wonderlands"?0x20B:0x10B);
        }
        catch(Exception e)when(e is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException){return false;}
    }
    public static bool Matches(ExecutableConsent? consent,string root,string hash)=>consent is not null&&
        Paths.Same(Path.GetFullPath(consent.Root),Path.GetFullPath(root))&&Paths.Same(consent.Sha256,hash);
}
