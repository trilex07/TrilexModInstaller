using System.Text.Json;
using System.Text.Json.Serialization;

namespace Trilex.Installer.Core;

public static class Json
{
    public static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true, PropertyNameCaseInsensitive = false, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    public static T Read<T>(string path) => JsonSerializer.Deserialize<T>(File.ReadAllBytes(path), Options) ?? throw new InvalidDataException("Empty document");
    public static void Write<T>(string path, T value) => Paths.AtomicBytes(Path.GetFullPath(path),JsonSerializer.SerializeToUtf8Bytes(value,Options));
}
public sealed record Catalog(int SchemaVersion, string Version, DateTimeOffset ExpiresUtc, List<GameManifest> Games);
public sealed record GameManifest(string Id, string Name, string State, string Adapter, string SteamAppId, string Executable, List<string> ExeSha256, List<ModuleManifest> Modules);
public sealed record ModuleManifest(string Id, Dictionary<string,string> Labels, string Version, string State, bool Required, List<string> Dependencies, long DownloadBytes, string? DownloadUrl, string? ArchiveSha256, long ExpandedBytes, bool RightsVerified, string RightsEvidence, List<PackageFile> Files, string Reason, Dictionary<string,string>? LocalizedReasons = null, PublisherReview? PublisherReview = null)
{
    public string Label(string language) => Labels.GetValueOrDefault(language, Labels.GetValueOrDefault("en", Id));
    public string ReasonFor(string language) => LocalizedReasons?.GetValueOrDefault(language,Reason)??Reason;
}
// A factual publisher decision is distinct from verification of third-party rights.
// Only the pinned publisher can authorize this through a signed catalog.
public sealed record PublisherReview(string Basis,string Evidence,string Notice);
public sealed record PackageFile(string Path, string Entry, string Sha256, string Kind, string Ownership, string? BeforeSha256 = null, Dictionary<string,string>? IniValues = null, List<PatchPart>? Recipe = null, string? SourcePath = null, string? SourceSha256 = null, string? SystemSource = null, string? SourceCodec = null, string? DecodedSha256 = null, long? DecodedBytes = null, long? SourceOffset = null, long? SourceBytes = null, bool UseCurrentSystemVersion = false);
public sealed record PatchPart(string Source, long Offset, int Length, string? Entry = null);
public sealed record SignedEnvelope(string Algorithm, string Payload, string Signature);
public sealed record InstallChoices(string TextLanguage, string VoiceLanguage, string UiLanguage, List<string> SelectedModules);
public sealed record InstallPlan(string GameId, string Version, string Root, string ExeSha256, InstallChoices Choices, List<ModuleManifest> Modules, string Executable = "Binaries/Singularity.exe")
{
    public long DownloadBytes => Modules.Sum(x => x.DownloadBytes);
    public long ExpandedBytes => Modules.Sum(x => x.ExpandedBytes);
}
public sealed record ProgressInfo(string Stage, string Item, long Completed, long Total);
public sealed record GameLocation(string Root, string Source, bool Compatible, string Detail, string? ExeHash);
public sealed record OwnedFile(string Path, string? OriginalHash, string Backup, string InstalledHash, string Kind, Dictionary<string,string>? IniValues, Dictionary<string,string?>? OriginalIniValues, bool CanRestoreWhole = false);
public sealed record InstallReceipt(string Schema, string Root, string GameId, string Version, InstallChoices Choices, List<OwnedFile> Files, string TransactionId);
public sealed record Journal(string Schema, string Root, string Phase, string TransactionId, InstallReceipt? PreviousReceipt, List<JournalFile> Files);
public sealed record JournalFile(string Path, string? BeforeHash, string? AfterHash, string Backup, bool Applied);

