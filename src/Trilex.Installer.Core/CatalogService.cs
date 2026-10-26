using System.Net;
using System.Security.Cryptography;
using System.Text.Json;

namespace Trilex.Installer.Core;

public sealed class CatalogService(HttpClient http, string pinnedPublicKey, string repository = "trilex07/TrilexModInstaller")
{
    public byte[]? LastVerifiedEnvelope{get;private set;}
    public const int MaximumCatalogBytes = 4 * 1024 * 1024;
    public Catalog Verify(byte[] envelopeBytes, DateTimeOffset? now = null)
    {
        if (envelopeBytes.Length > MaximumCatalogBytes) throw new InvalidDataException("Catalog too large");
        var envelope = JsonSerializer.Deserialize<SignedEnvelope>(envelopeBytes,Json.Options) ?? throw new InvalidDataException("Empty catalog");
        if (envelope.Algorithm != "ECDSA-P256-SHA256-P1363") throw new CryptographicException("Unsupported catalog signature");
        var payload = Convert.FromBase64String(envelope.Payload);
        using var key = ECDsa.Create(); key.ImportFromPem(pinnedPublicKey);
        if(key.KeySize != 256 || !key.VerifyData(payload,Convert.FromBase64String(envelope.Signature),HashAlgorithmName.SHA256,DSASignatureFormat.IeeeP1363FixedFieldConcatenation)) throw new CryptographicException("Catalog signature does not match pinned publisher");
        var catalog = JsonSerializer.Deserialize<Catalog>(payload,Json.Options) ?? throw new InvalidDataException("Empty payload");
        if(catalog.SchemaVersion != 1 || catalog.ExpiresUtc <= (now ?? DateTimeOffset.UtcNow)) throw new InvalidDataException("Expired/unsupported catalog");
        if(catalog.Games.Select(x=>x.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=catalog.Games.Count) throw new InvalidDataException("Duplicate game ID");
        foreach(var game in catalog.Games) PlanBuilder.ValidateManifest(game);
        LastVerifiedEnvelope=envelopeBytes.ToArray();return catalog;
    }
    public async Task<Catalog> FetchLatestAsync(CancellationToken ct)
    {
        using var response=await http.GetAsync("https://api.github.com/repos/"+repository+"/releases/latest",HttpCompletionOption.ResponseHeadersRead,ct);response.EnsureSuccessStatusCode();
        await using var input=await response.Content.ReadAsStreamAsync(ct);using var buffer=new MemoryStream();var block=new byte[8192];int count;
        while((count=await input.ReadAsync(block,ct))>0){if(buffer.Length+count>MaximumCatalogBytes)throw new InvalidDataException("Release metadata too large");buffer.Write(block,0,count);}
        using var doc=JsonDocument.Parse(buffer.ToArray());var assets=doc.RootElement.GetProperty("assets");
        var link=assets.EnumerateArray().Single(x=>x.GetProperty("name").GetString()=="catalog.signed.json").GetProperty("browser_download_url").GetString()??throw new InvalidDataException("Signed catalog not published");
        return await FetchAsync(new Uri(link),ct); // Release metadata locates, but never authorizes the package. Pinned signature does.
    }
    public async Task<Catalog> FetchAsync(Uri url,CancellationToken ct)
    {
        ValidateReleaseUrl(url,repository);
        using var response = await ReleaseTransport.SendAsync(http,url,0,ct);
        response.EnsureSuccessStatusCode();
        if(response.Content.Headers.ContentLength > MaximumCatalogBytes) throw new InvalidDataException("Catalog too large");
        await using var stream=await response.Content.ReadAsStreamAsync(ct);using var buffer=new MemoryStream();
        var block=new byte[8192];int n;while((n=await stream.ReadAsync(block,ct))>0){if(buffer.Length+n>MaximumCatalogBytes)throw new InvalidDataException("Catalog too large");buffer.Write(block,0,n);}
        return Verify(buffer.ToArray());
    }
    public static void ValidateReleaseUrl(Uri url,string repository)
    {
        if(url.Scheme!="https" || url.Host!="github.com" || !url.IsDefaultPort || !string.IsNullOrEmpty(url.UserInfo) || !string.IsNullOrEmpty(url.Query) || !string.IsNullOrEmpty(url.Fragment) || !url.AbsolutePath.StartsWith('/'+repository+"/releases/download/",StringComparison.Ordinal)) throw new InvalidDataException("Only immutable assets in the pinned GitHub repository are allowed");
        var suffix=url.AbsolutePath[('/'+repository+"/releases/download/").Length..];
        if(suffix.Split('/').Length!=2 || suffix.Contains("latest",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Explicit release tag and asset name required");
    }
    public static HttpClient CreateHttpClient() { var client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false,AutomaticDecompression=DecompressionMethods.None}){Timeout=TimeSpan.FromSeconds(45)};client.DefaultRequestHeaders.UserAgent.ParseAdd("TrilexModInstaller/0.1");return client; }
}
public static class PlanBuilder
{
    public static void ValidateManifest(GameManifest game)
    {
        if(game.Modules.Select(x=>x.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=game.Modules.Count)throw new InvalidDataException("Duplicate module ID");
        foreach(var m in game.Modules)
        {
            if(m.DownloadBytes<0 || m.DownloadBytes >= 2L*1024*1024*1024 || m.ExpandedBytes<0 || m.ExpandedBytes>16L*1024*1024*1024)throw new InvalidDataException("Invalid package size");
            foreach(var f in m.Files)
            {
                Paths.Safe(Path.GetTempPath(),f.Path);Paths.AllowedTarget(f.Path);
                LocalSources.Validate(f,Path.GetTempPath());
            }
            if(m.State=="stable" && (!m.RightsVerified || string.IsNullOrWhiteSpace(m.RightsEvidence) || m.Files.Count==0 || !ValidHash(m.ArchiveSha256) || m.DownloadBytes==0 || m.DownloadUrl is null))throw new InvalidDataException("Stable module lacks rights, files or exact distribution identity");
        }
    }
    public static bool ValidHash(string? hash)=>hash is not null && System.Text.RegularExpressions.Regex.IsMatch(hash,"^[0-9A-Fa-f]{64}$");
    public static InstallPlan Build(GameManifest game,GameLocation location,InstallChoices choices)
    {
        ValidateManifest(game);
        if(game.State is not ("stable" or "preview") || !location.Compatible || location.ExeHash is null || !game.ExeSha256.Any(x=>Paths.Same(x,location.ExeHash)))throw new InvalidOperationException("Game is not compatible or not released");
        if(choices.TextLanguage is not ("en" or "pl") || choices.VoiceLanguage is not ("en" or "pl") || choices.UiLanguage is not ("en" or "pl"))throw new InvalidDataException("Unsupported language");
        var ids=choices.SelectedModules.Concat(game.Modules.Where(x=>x.Required).Select(x=>x.Id)).ToHashSet(StringComparer.Ordinal);
        if(choices.TextLanguage=="pl")ids.Add("polish-text");
        if(choices.VoiceLanguage=="pl")ids.Add("polish-dubbing");
        if(choices.TextLanguage=="en" && ids.Contains("polish-text") || choices.VoiceLanguage=="en" && ids.Contains("polish-dubbing"))throw new InvalidOperationException("Language choices conflict with selected packs");
        var result=new List<ModuleManifest>();var visiting=new HashSet<string>();var done=new HashSet<string>();
        void Visit(string id)
        {
            if(done.Contains(id))return;if(!visiting.Add(id))throw new InvalidDataException("Dependency cycle");
            var module=game.Modules.SingleOrDefault(x=>x.Id==id)??throw new InvalidDataException("Missing dependency: "+id);
            if(module.State!="stable")throw new InvalidOperationException(module.Label(choices.UiLanguage)+": "+module.ReasonFor(choices.UiLanguage));
            foreach(var dep in module.Dependencies)Visit(dep);
            visiting.Remove(id);done.Add(id);result.Add(module);
        }
        foreach(var id in ids.Order())Visit(id);
        var paths=result.SelectMany(x=>x.Files).Where(x=>x.Kind!="ini").Select(x=>x.Path).ToList();
        if(paths.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=paths.Count)throw new InvalidDataException("Two modules own the same target");
        return new(game.Id,game.Modules.First(x=>x.Required).Version,location.Root,location.ExeHash,choices,result,game.Executable);
    }
}
