namespace Trilex.Installer.Core;

// Preserve unrelated settings/comments; reject duplicate controlled keys rather than silently changing Win32 precedence.
public sealed class IniDocument(string text)
{
    private readonly List<string> lines=text.Replace("\r\n","\n").Split('\n').ToList();
    private Dictionary<string,int> Indices()
    {
        var found=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);var section="";
        for(var i=0;i<lines.Count;i++)
        {
            var value=lines[i].Trim();if(value.StartsWith(';')||value.StartsWith('#'))continue;
            if(value.StartsWith('[')&&value.EndsWith(']')){section=value[1..^1];continue;}
            var eq=value.IndexOf('=');if(eq<=0)continue;
            var key=section+"/"+value[..eq].Trim();if(!found.TryAdd(key,i))throw new InvalidDataException("Duplicate INI key: "+key);
        }
        return found;
    }
    public string? Get(string key){var ids=Indices();return ids.TryGetValue(key,out var i)?lines[i][(lines[i].IndexOf('=')+1)..].Trim():null;}
    public void Set(string key,string? value)
    {
        var parts=key.Split('/');if(parts.Length!=2 || parts.Any(string.IsNullOrWhiteSpace) || (value?.IndexOfAny(['\r','\n'])??-1)>=0)throw new InvalidDataException("Invalid INI key/value");
        var ids=Indices();if(ids.TryGetValue(key,out var i)){if(value is null)lines.RemoveAt(i);else lines[i]=parts[1]+"="+value;return;}
        if(value is null)return;
        var section=lines.FindIndex(x=>x.Trim().Equals('['+parts[0]+']',StringComparison.OrdinalIgnoreCase));
        if(section<0){lines.Add('['+parts[0]+']');lines.Add(parts[1]+"="+value);return;}
        var end=lines.FindIndex(section+1,x=>x.Trim().StartsWith('['));if(end<0)end=lines.Count;
        lines.Insert(end,parts[1]+"="+value);
    }
    public override string ToString()=>string.Join("\r\n",lines);
}
