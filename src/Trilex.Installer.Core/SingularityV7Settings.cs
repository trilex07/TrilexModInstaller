namespace Trilex.Installer.Core;

/// <summary>Choice-dependent overrides of the reviewed V7 preset, not arbitrary package commands.</summary>
public static class SingularityV7Settings
{
    public static List<ModuleManifest> Apply(List<ModuleManifest> modules,InstallChoices choices)
    {
        if(!modules.Any(m=>m.Id=="portable-settings"))return modules;
        // Accepted V7 uses the text enable flag in BinkOpen's Polish narration gate.
        // Do not advertise English text / Polish movies as independent until that runtime is reviewed.
        if(choices.TextLanguage=="en"&&choices.VoiceLanguage=="pl")throw new InvalidOperationException(choices.UiLanguage=="pl"?"V7 łączy polskie ścieżki filmów z językiem tekstu. Angielski tekst z pełnym polskim dubbingiem wymaga poprawki runtime; nic nie zmieniono.":"V7 couples Polish movie tracks to text language. English text with complete Polish voice requires a reviewed runtime fix; no files changed.");
        return modules.Select(m=>m.Id!="portable-settings"?m:m with{Files=m.Files.Select(f=>
        {
            if(f.Kind!="ini"||!Paths.Same(f.Path,"Binaries/SingularityVR.ini"))return f;
            var values=new Dictionary<string,string>(f.IniValues??[],StringComparer.OrdinalIgnoreCase)
            {
                ["Localization/PolishLanguage"]=choices.TextLanguage=="pl"?"1":"0",
                ["Localization/ShowPolishLabels"]=choices.TextLanguage=="pl"?"1":"0",
                ["Localization/NativePolishSpeech"]=choices.VoiceLanguage=="pl"?"1":"0",
                ["Localization/PolishIntroAudioFix"]=choices.VoiceLanguage=="pl"?"1":"0"
            };
            return f with{IniValues=values};
        }).ToList()}).ToList();
    }
}
