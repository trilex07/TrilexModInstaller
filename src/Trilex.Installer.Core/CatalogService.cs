using System.Net;
using System.Security.Cryptography;
using System.Text.Json;

namespace Trilex.Installer.Core;

public sealed class CatalogService(HttpClient http, string pinnedPublicKey, string repository = "trilex07/TrilexModInstaller")
{
    public byte[]? LastVerifiedEnvelope{get;private set;}
    public string? LatestReleaseTag{get;private set;}
    public string? LatestReleasePage{get;private set;}
    public static Version? ParseReleaseVersion(string? tag)
    {
        var match=tag is null?null:System.Text.RegularExpressions.Regex.Match(tag,@"(\d+)\.(\d+)\.(\d+)");
        return match is {Success:true}?new Version(int.Parse(match.Groups[1].Value),int.Parse(match.Groups[2].Value),int.Parse(match.Groups[3].Value)):null;
    }
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
        LatestReleaseTag=doc.RootElement.TryGetProperty("tag_name",out var tag)?tag.GetString():null;
        LatestReleasePage=doc.RootElement.TryGetProperty("html_url",out var page)&&page.GetString() is string html&&html.StartsWith("https://github.com/"+repository+"/releases/",StringComparison.Ordinal)?html:null;
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
    public static bool HasDistributionReview(ModuleManifest module)=>
        module.RightsVerified&&!string.IsNullOrWhiteSpace(module.RightsEvidence)||
        module.PublisherReview is {Basis:"fan-mod-publisher-approved"} review&&
        !string.IsNullOrWhiteSpace(review.Evidence)&&!string.IsNullOrWhiteSpace(review.Notice);
    public static void ValidateManifest(GameManifest game)
    {
        if(game.Modules.Select(x=>x.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=game.Modules.Count)throw new InvalidDataException("Duplicate module ID");
        if(game.Id=="darkness2" && game.State is ("stable" or "preview") &&
            (game.Adapter!="Darkness2GameAdapter" || game.SteamAppId!="67370" || game.Executable!="DarknessII.exe" ||
             game.ExeSha256.Count==0 || game.ExeSha256.Any(h=>!ValidHash(h)) ||
             game.Modules.Count(m=>m.Required)!=1 || game.Modules.Any(m=>m.Id=="polish-dubbing")))
            throw new InvalidDataException("Invalid Darkness II release manifest");
        if(game.Id=="wonderlands" && (game.Adapter!="WonderlandsGameAdapter" || game.SteamAppId!="1286680" || game.Executable!="OakGame/Binaries/Win64/Wonderlands.exe" || game.Modules.Count(m=>m.Required)!=1 || game.ExeSha256.Count==0 || game.ExeSha256.Any(h=>!ValidHash(h))))throw new InvalidDataException("Invalid Wonderlands manifest");
        foreach(var m in game.Modules)
        {
            if(m.DownloadBytes<0 || m.DownloadBytes >= 2L*1024*1024*1024 || m.ExpandedBytes<0 || m.ExpandedBytes>16L*1024*1024*1024)throw new InvalidDataException("Invalid package size");
            foreach(var f in m.Files)
            {
                Paths.Safe(Path.GetTempPath(),f.Path);Paths.AllowedTarget(f.Path);
                LocalSources.Validate(f,Path.GetTempPath());
                var target=f.Path.Replace('\\','/');
                var darknessTarget=Paths.Same(target,"d3d9.dll")||Paths.Same(target,"Darkness2VR.ini") || Paths.Same(target,"openxr_loader.dll")||Paths.Same(target,"Darkness2VR-diagnostic.ini")||Paths.DarknessAssetTarget(target)||Paths.DarknessTextTarget(target)||DarknessTextureInstall.IsTarget(target);
                darknessTarget|=DarknessHandsInstall.IsTarget(target);
                if(DarknessHandsInstall.IsTarget(target)&&(m.Id!="hands-only"||f.Kind!="recipe"||f.Ownership!="local-transform"))throw new InvalidDataException("Darkness II hands require a local reconstruction");
                if((game.Id=="wonderlands")!=Paths.WonderlandsTarget(target))throw new InvalidDataException("Target belongs to another game");
                if((game.Id=="darkness2")!=darknessTarget)throw new InvalidDataException("Target belongs to another game");
                if(game.Id=="darkness2" && Paths.DarknessTextTarget(target) && (m.Id is not ("polish-text" or "darkness-menu") || f.Kind!="recipe"))
                    throw new InvalidDataException("Darkness II language cache requires a local recipe");
                if(game.Id=="darkness2" && Paths.DarknessAssetTarget(target) && (m.Id!=(Paths.DarknessFontTarget(target)?"polish-text":"darkness-assets") || f.Kind!="recipe"))
                    throw new InvalidDataException("Darkness II DLC repairs require a local recipe");
            }
            if(m.State=="stable" && (!HasDistributionReview(m) || m.Files.Count==0 || !ValidHash(m.ArchiveSha256) || m.DownloadBytes==0 || m.DownloadUrl is null))throw new InvalidDataException("Stable module lacks explicit distribution review, files or exact distribution identity");
        }
    }
    public static bool ValidHash(string? hash)=>hash is not null && System.Text.RegularExpressions.Regex.IsMatch(hash,"^[0-9A-Fa-f]{64}$");
    public static InstallPlan Build(GameManifest game,GameLocation location,InstallChoices choices,ExecutableConsent? consent=null)
    {
        ValidateManifest(game);
        var verified=location.Compatible&&game.ExeSha256.Any(x=>Paths.Same(x,location.ExeHash));
        var acknowledged=location.ExeHash is not null&&ExecutableCompatibility.Matches(consent,location.Root,location.ExeHash)&&ExecutableCompatibility.CanTry(game,location);
        if(game.State is not ("stable" or "preview") || location.ExeHash is null || (!verified&&!acknowledged))throw new InvalidOperationException("Game is not compatible or not released; unverified builds require explicit review consent");
        if(choices.TextLanguage is not ("en" or "pl") || choices.VoiceLanguage is not ("en" or "pl") || choices.UiLanguage is not ("en" or "pl"))throw new InvalidDataException("Unsupported language");
        var ids=choices.SelectedModules.Concat(game.Modules.Where(x=>x.Required).Select(x=>x.Id)).ToHashSet(StringComparer.Ordinal);
        if(choices.TextLanguage=="pl")
        {
            ids.Add("polish-text");
            if(game.Id=="darkness2")ids.Remove("darkness-menu");
        }
        if(game.Id=="darkness2" && choices.TextLanguage=="en" && game.Modules.Any(m=>m.Id=="darkness-menu"))ids.Add("darkness-menu");
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
        if(game.Id=="darkness2" && choices.TextLanguage!="pl" && result.Any(m=>m.Id=="polish-text"))
            throw new InvalidDataException("Darkness II Polish resources require Polish game text");
        if(game.Adapter=="SingularityGameAdapter")result=SingularityV7Settings.Apply(result,choices);
        result=LocalSources.BindCurrentSystem(result);
        var paths=result.SelectMany(x=>x.Files).Where(x=>x.Kind!="ini").Select(x=>x.Path).ToList();
        if(paths.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=paths.Count)throw new InvalidDataException("Two modules own the same target");
        return new(game.Id,game.Modules.First(x=>x.Required).Version,location.Root,location.ExeHash,choices,result,game.Executable,verified?null:consent);
    }
}
