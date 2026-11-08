using System.Text;

namespace Trilex.Installer.Core;

// Choice-derived config is applied only after signed package verification and
// before transaction hashes are recorded. It never changes the staged package.
public static class DarknessLanguageSettings
{
    public static bool ApplyPrepared(InstallPlan plan,PackageFile file,string prepared)
    {
        if(plan.GameId!="darkness2" || file.Kind!="copy" || !Paths.Same(file.Path,"Darkness2VR.ini"))return false;
        if(!Paths.Same(Paths.Hash(prepared),file.Sha256))throw new InvalidDataException("Darkness config payload checksum mismatch");
        if(plan.Choices.TextLanguage is not ("en" or "pl") || plan.Choices.VoiceLanguage is not ("en" or "pl"))throw new InvalidDataException("Unsupported Darkness language");
        var polish=plan.Choices.TextLanguage=="pl";
        if(polish!=plan.Modules.Any(m=>m.Id=="polish-text"))throw new InvalidDataException("Darkness text choice does not match installed resources");
        // Current reviewed catalog has no Polish dubbing module. Never claim
        // that checked Polish voices exist when only English files are installed.
        if(plan.Choices.VoiceLanguage!="en")throw new InvalidDataException("Darkness Polish voice resources are unavailable in this catalog");
        var ini=new IniDocument(File.ReadAllText(prepared));
        foreach(var key in new[]{"PolishText","AppliedPolishText"})ini.Set("VRSettings/"+key,polish?"1":"0");
        foreach(var key in new[]{"PolishVoice","AppliedPolishVoice","LanguageError"})ini.Set("VRSettings/"+key,"0");
        Paths.AtomicBytes(prepared,Encoding.UTF8.GetBytes(ini.ToString()));return true;
    }
}
