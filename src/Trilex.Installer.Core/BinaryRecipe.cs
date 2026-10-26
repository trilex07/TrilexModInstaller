namespace Trilex.Installer.Core;

/// <summary>Data-only copy/literal bytecode, not native code or a scripting language.</summary>
public static class BinaryRecipe
{
    public static void Apply(string original,string recipe,Stream output,long expectedLength)
    {
        using var file=File.OpenRead(recipe);using var r=new BinaryReader(file);
        if(file.Length<20||file.Length>512L*1024*1024||!r.ReadBytes(4).SequenceEqual("SVRP"u8.ToArray())||r.ReadUInt32()!=1||r.ReadUInt64()!=(ulong)expectedLength)throw new InvalidDataException("Invalid reconstruction recipe");
        var count=r.ReadUInt32();if(count>1000000)throw new InvalidDataException("Too many reconstruction instructions");
        using var source=File.OpenRead(original);var start=output.Position;var buffer=new byte[81920];
        for(uint i=0;i<count;i++)
        {
            var opcode=r.ReadByte();ulong offset=0;if(opcode==0)offset=r.ReadUInt64();else if(opcode!=1)throw new InvalidDataException("Unsupported reconstruction opcode");
            var length=r.ReadUInt32();if(length>256*1024*1024||length>(ulong)(expectedLength-(output.Position-start)))throw new InvalidDataException("Reconstruction exceeds output budget");
            Stream input;
            if(opcode==0){if(offset>(ulong)source.Length||length>(ulong)source.Length-offset)throw new InvalidDataException("Copy exceeds owned original");source.Position=(long)offset;input=source;}
            else {if(length>file.Length-file.Position)throw new InvalidDataException("Truncated reconstruction literal");input=file;}
            long remaining=length;while(remaining>0){var n=input.Read(buffer,0,(int)Math.Min(remaining,buffer.Length));if(n==0)throw new EndOfStreamException();output.Write(buffer,0,n);remaining-=n;}
        }
        if(file.Position!=file.Length||output.Position-start!=expectedLength)throw new InvalidDataException("Reconstruction length or trailing instructions mismatch");
    }
}
