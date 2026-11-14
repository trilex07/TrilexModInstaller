using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Trilex.Installer.Core;

// Export only explicitly parsed fields. Runtime and INI text may contain paths
// or other user input, so do not put their raw lines into the report.
public static class DarknessStartupDiagnostics
{
    private const int MaxLogBytes = 2 * 1024 * 1024;
    private const int MaxIniBytes = 128 * 1024;
    private const int MaxFingerprintBytes = 128 * 1024 * 1024;
    private const int MaxEvents = 64;

    private static readonly Regex Startup = new(
        @"^\[Startup\] stage=(?<value>[0-9]{1,2}) ", RegexOptions.CultureInvariant);
    private static readonly Regex Result = new(
        @"^\[XRStartup\] (?<stage>xrGetSystem|graphicsRequirementsProc|graphicsRequirements|xrCreateSession|xrCreateReferenceSpace LOCAL|xrBeginSession|xrWaitFrame|xrBeginFrame|first xrEndFrame|first game layer submitted) result=(?<value>-?[0-9]{1,10})(?= |$)", RegexOptions.CultureInvariant);
    private static readonly Regex Fields = new(
        @" (?<key>boundaryRequest|policy|submit|stereo|disabled|processBits|loaderLoaded|nativeGuard|ready|inPresent|noFunction|shouldRender|layers|gameLayers|sessionState|propertiesResult)=(?<value>-?[0-9]{1,10})(?= |$)", RegexOptions.CultureInvariant);
    private static readonly Regex BoundaryReason = new(
        @"^\[XRStartup\] readyPresent boundaryRequest=[01] reason=(?<reason>SwapChainPresent|trace-only|reset-active|none)(?= |$)", RegexOptions.CultureInvariant);
    private static readonly Regex Profile = new(
        @"^\[EffectiveConfig\] RenderPipelineTrace=(?<renderPipelineTrace>[0-9]+) FrameBoundaryPolicy=(?<frameBoundaryPolicy>[0-9]+) DisableOpenXR=(?<disableOpenXR>[0-9]+) SubmitMode=(?<submitMode>[0-9]+) StereoMode=(?<stereoMode>[0-9]+) StartupMode=(?<startupMode>[0-9]+) HookMask=0x[0-9a-fA-F]+ PresentHookLevel=(?<presentHookLevel>[0-9]+)$", RegexOptions.CultureInvariant);
    private static readonly Regex Present = new(
        @"^\[ImagePresent\] raw-present returned entry=[0-9]+ target=[0-9a-fA-F]+ sc=[0-9a-fA-F]+ caller=[0-9a-fA-F]+ callerQualifies=(?<callerQualifies>[01]) created=(?<created>[01]) qualified=(?<qualified>[01]) readyBefore=(?<readyBefore>[01]) readyAfter=(?<readyAfter>[01]) hr=(?<hr>[0-9a-fA-F]{8}) ", RegexOptions.CultureInvariant);
    private static readonly Regex Instance = new(
        @"^\[XRStartup\] instance result=(?<value>-?[0-9]{1,10}) runtime=", RegexOptions.CultureInvariant);
    private static readonly Regex InstanceFailure = new(
        @"^xrCreateInstance failed \((?<value>-?[0-9]{1,10})\)", RegexOptions.CultureInvariant);
    private static readonly Regex SessionState = new(
        @"^\[XRSession\] state=(?<value>[0-9]{1,2})$", RegexOptions.CultureInvariant);
    private static readonly Regex Graphics = new(
        @"^\[XRStartup\] (?<stage>CreateDXGIFactory1|D3D11CreateDevice) hr=(?<value>[0-9a-fA-F]{8})$", RegexOptions.CultureInvariant);
    private static readonly string[] MainSettings = ["Render/StartupMode","Render/DisableOpenXR","Render/SubmitMode","Render/StereoMode","Render/FrameBoundaryPolicy","VRSettings/PolishText"];
    private static readonly string[] DiagnosticSettings = ["Diagnostics/ImagePreservingPresent","Diagnostics/BlockNativeImagePatches","Diagnostics/PresentReadiness","Simulator/Enabled"];

    public static void Export(string gameRoot, string localAppData, string destination,
        IReadOnlyList<string> knownExeHashes, CancellationToken ct)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(gameRoot));
        var fullDestination = Path.GetFullPath(destination);
        if (Paths.Same(fullDestination,root) ||
            fullDestination.StartsWith(Path.EndsInDirectorySeparator(root)?root:root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Save VR diagnostics outside the game folder.");
        if (!Paths.Same(Path.GetExtension(fullDestination),".json"))
            throw new InvalidDataException("Save VR diagnostics as a .json report.");

        Paths.AssertNoLinks(fullDestination);
        var exeHash = Fingerprint("DarknessII.exe");
        var logPath = Paths.Safe(localAppData,"Darkness2VR/d3d9.log");
        var log = ReadPrefix(logPath,MaxLogBytes);
        var events = log.Text is null ? [] : ParseEvents(log.Text,log.Truncated,ct);
        var report = new
        {
            schema = "darkness2vr-startup-v1",
            // File identities describe this selected folder now, not the process that wrote the log.
            selectedFolder = new {
                fingerprintKnown = exeHash is null ? (bool?)null : knownExeHashes.Any(h=>Paths.Same(h,exeHash)),
                exeSha256 = exeHash, proxySha256 = Fingerprint("d3d9.dll"), loaderSha256 = Fingerprint("openxr_loader.dll"),
                mainIniSha256 = Fingerprint("Darkness2VR.ini"), diagnosticIniSha256 = Fingerprint("Darkness2VR-diagnostic.ini"),
                mainIni = ReadSettings(Paths.Safe(root,"Darkness2VR.ini"),MainSettings),
                diagnosticIni = ReadSettings(Paths.Safe(root,"Darkness2VR-diagnostic.ini"),DiagnosticSettings)
            },
            localAppDataFallbackIni = ReadSettings(Paths.Safe(localAppData,"Darkness2VR/Darkness2VR.ini"),MainSettings),
            runtimeLog = new { status = log.Status, lastWriteUtc = log.LastWriteUtc, scanTruncated = log.Truncated, eventLimitReached = events.Count == MaxEvents, events }
        };
        ct.ThrowIfCancellationRequested();
        Paths.AtomicBytes(fullDestination,JsonSerializer.SerializeToUtf8Bytes(report,Json.Options));

        string? Fingerprint(string name)
        {
            ct.ThrowIfCancellationRequested();
            var file = Paths.Safe(root,name);
            try
            {
                using var input = File.OpenRead(file);
                return input.Length>MaxFingerprintBytes ? null : Convert.ToHexString(SHA256.HashData(input));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
        }
    }

    private static (string? Text, string Status, bool Truncated, DateTime? LastWriteUtc) ReadPrefix(string path,int maxBytes)
    {
        if(!File.Exists(path))return (null,"missing",false,null);
        try
        {
            using var input = new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
            var bytes = new byte[Math.Min(input.Length,maxBytes)];
            var count = input.ReadAtLeast(bytes,bytes.Length,false);
            var truncated = input.ReadByte()!=-1;
            using var text = new StreamReader(new MemoryStream(bytes,0,count),Encoding.UTF8);
            return (text.ReadToEnd(),"read",truncated,File.GetLastWriteTimeUtc(path));
        }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException){return (null,"unavailable",false,null);}
    }

    private static object ReadSettings(string path,IEnumerable<string> keys)
    {
        var source = ReadPrefix(path,MaxIniBytes);
        var values = new Dictionary<string,int>();
        var status = source.Status;
        if(source.Truncated)status="tooLarge";
        else if(source.Text is not null)
        {
            try
            {
                var ini = new IniDocument(source.Text);
                foreach(var key in keys)
                    if(int.TryParse(ini.Get(key),NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out var value))
                        values.Add(key,value);
            }
            catch(InvalidDataException){status="invalid";values.Clear();}
        }
        return new {status,values};
    }

    private sealed record Event(string Stage, int? Value = null, Dictionary<string,int>? Fields = null, string? Runtime = null, string? HResult = null, string? Reason = null);

    private static List<Event> ParseEvents(string text,bool truncated,CancellationToken ct)
    {
        List<Event> events = [];
        // A snapshot ending mid-line can describe an incomplete runtime write.
        // Allow every complete line within the scan, even in a long log.
        if(truncated || !text.EndsWith('\n'))text=text[..(text.LastIndexOf('\n')+1)];
        using var reader = new StringReader(text);
        while(events.Count<MaxEvents && reader.ReadLine() is string line)
        {
            ct.ThrowIfCancellationRequested();
            if(line.Length>512)continue;
            var match = Startup.Match(line);
            if(Value(match) is int stage){events.Add(new("proxyStartup",stage));continue;}
            match = Profile.Match(line);
            if(match.Success){events.Add(new("effectiveConfig",Fields:ReadGroups(match,Profile)));continue;}
            match = Present.Match(line);
            if(match.Success){events.Add(new("nativePresent",Fields:ReadGroups(match,Present),HResult:match.Groups["hr"].Value));continue;}
            match = InstanceFailure.Match(line);
            if(Value(match) is int failure){events.Add(new("xrCreateInstanceFailed",failure));continue;}
            match = SessionState.Match(line);
            if(Value(match) is int state){events.Add(new("sessionState",state));continue;}
            match = Graphics.Match(line);
            if(match.Success){events.Add(new(match.Groups["stage"].Value,HResult:match.Groups["value"].Value));continue;}
            match = Result.Match(line);
            if(Value(match) is int result){events.Add(new(match.Groups["stage"].Value,result,ReadFields(line)));continue;}
            match = Instance.Match(line);
            if(Value(match) is int instance){events.Add(new("xrCreateInstance",instance,ReadFields(line),Runtime(line)));continue;}
            foreach(var gate in new[]{"readyPresent","initAttempt","initRejected"})
                if(line.StartsWith("[XRStartup] "+gate+" ",StringComparison.Ordinal))
                {
                    var reason = gate=="readyPresent" ? BoundaryReason.Match(line).Groups["reason"].Value : "";
                    events.Add(new(gate,Fields:ReadFields(line),Reason:reason.Length==0?null:reason));
                    break;
                }
        }
        return events;

        static int? Value(Match m)=>int.TryParse(m.Groups["value"].Value, NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out var i)?i:null;
        static Dictionary<string,int> ReadGroups(Match match,Regex pattern)
        {
            var fields = new Dictionary<string,int>();
            foreach(var name in pattern.GetGroupNames().Skip(1))
                if(name!="hr" && int.TryParse(match.Groups[name].Value,NumberStyles.None,CultureInfo.InvariantCulture,out var value))
                    fields.Add(name,value);
            return fields;
        }
        static Dictionary<string,int> ReadFields(string line)
        {
            var fields = new Dictionary<string,int>();
            foreach(Match match in Fields.Matches(line))
                if(Value(match) is int value)fields.TryAdd(match.Groups["key"].Value,value);
            return fields;
        }
        static string Runtime(string line)
        {
            const string marker = " runtime=";
            var begin = line.IndexOf(marker,StringComparison.Ordinal)+marker.Length;
            var end = line.LastIndexOf(" propertiesResult=",StringComparison.Ordinal);
            if (end<=begin) return "unrecognized";
            var name = line[begin..end].Trim();
            // Only this fixed vocabulary leaves the machine; no raw runtime names.
            if(name.Equals("SteamVR/OpenXR",StringComparison.OrdinalIgnoreCase) ||
               name.Equals("SteamVR",StringComparison.OrdinalIgnoreCase))return "SteamVR";
            if(name.Equals("VirtualDesktopXR",StringComparison.OrdinalIgnoreCase) ||
               name.Equals("Virtual Desktop",StringComparison.OrdinalIgnoreCase) ||
               name.Equals("VDXR",StringComparison.OrdinalIgnoreCase))return "VirtualDesktopXR";
            if(name.Equals("Oculus",StringComparison.OrdinalIgnoreCase) ||
               name.Equals("Oculus OpenXR",StringComparison.OrdinalIgnoreCase) ||
               name.Equals("Meta OpenXR",StringComparison.OrdinalIgnoreCase))return "Meta";
            if(name.Equals("unknown",StringComparison.OrdinalIgnoreCase))return "unknown";
            return "unrecognized";
        }
    }
}
