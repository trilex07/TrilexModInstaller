using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Trilex.Installer.Core;

namespace Trilex.Installer.Desktop;

// Own-app offscreen layout and bindings test. Does not inspect/control other windows or launch Steam.
internal static class UiSmoke
{
    public static void Run(string output)
    {
        Directory.CreateDirectory(output);var errors=new BindingErrors();PresentationTraceSources.DataBindingSource.Listeners.Add(errors);
        var rows=new List<object>();
        foreach(var language in new[]{"en","pl"})foreach(var (width,height,dpi) in new[]{(1440,960,96),(1024,740,96),(1200,900,144),(1200,900,192)})
        {
            var window=new MainWindow();var vm=(InstallerViewModel)window.DataContext;vm.UiLanguage=language;
            CheckReconstructionWarning(vm);
            Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.Background);
            var root=(FrameworkElement)window.Content;
            root.Measure(new Size(width,height));root.Arrange(new Rect(0,0,width,height));root.UpdateLayout();
            var labels=Descendants(root).OfType<TextBlock>().Select(x=>x.Text).ToList();
            if(!labels.Contains(vm.L["Options"]) || !labels.Contains(vm.L["Modules"]))throw new InvalidDataException("Localized module content missing");
            if(vm.Modules.Any(m=>m.Manifest.Id=="portable-settings"&&m.Manifest.State=="stable"))
            {
                vm.SelectAcceptedPreset();
                if(vm.Modules.Count!=12||vm.Modules.Any(m=>m.Manifest.State!="stable"||!m.Selected)||vm.TextLanguage!="pl"||vm.VoiceLanguage!="pl")throw new InvalidDataException("Complete preset incomplete");
                vm.TextLanguage="en";vm.VoiceLanguage="en";
                if(vm.Modules.Any(m=>(m.Manifest.Id is "polish-text" or "polish-dubbing")&&m.Selected))throw new InvalidDataException("Language selectors retained contradictory packs");
                vm.Modules.Single(m=>m.Manifest.Id=="polish-dubbing").Selected=true;
                if(vm.VoiceLanguage!="pl")throw new InvalidDataException("Dubbing checkbox failed to synchronize selector");
                vm.SelectAcceptedPreset();
            }
            else if(vm.Modules.Count!=7 || vm.Modules.Any(x=>x.Selectable))throw new InvalidDataException("Unapproved packs must remain unavailable");
            if(Descendants(root).OfType<Button>().Any(b=>b.Content?.ToString()==vm.LaunchLabel&&b.IsEnabled))throw new InvalidDataException("Launch enabled without managed installation");
            var bmp=new RenderTargetBitmap((int)(width*dpi/96.0),(int)(height*dpi/96.0),dpi,dpi,PixelFormats.Pbgra32);bmp.Render(root);
            var file=$"installer-{language}-{width}x{height}-{dpi}dpi.png";using(var stream=File.Create(Path.Combine(output,file))){var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bmp));encoder.Save(stream);}
            CheckInstallActivity(vm,root,output,$"{language}-{width}x{height}-{dpi}dpi");
            rows.Add(new{language,width,height,dpi,file,status="PASS",activity="install/busy controls/late progress/error/cancel PASS",reconstruction="PL/EN prompt and exception completion/verify status PASS; native modal interaction not automated",scope="WPF layout/bindings/offscreen pixels; not physical DPI/input acceptance"});
            vm.SelectGame("darkness2");
            if(vm.Modules.Count==4)
            {
                if(vm.Modules.Any(m=>m.Manifest.State!="stable"||m.Manifest.Version.Contains("preview",StringComparison.OrdinalIgnoreCase))||vm.DarknessOptions!=Visibility.Visible||vm.SingularityOptions!=Visibility.Collapsed)throw new InvalidDataException("Darkness release/options incomplete");
                root.Measure(new Size(width,height));root.Arrange(new Rect(0,0,width,height));root.UpdateLayout();
                var darkness=new RenderTargetBitmap((int)(width*dpi/96.0),(int)(height*dpi/96.0),dpi,dpi,PixelFormats.Pbgra32);darkness.Render(root);
                using var darkStream=File.Create(Path.Combine(output,"darkness2-"+file));var darkEncoder=new PngBitmapEncoder();darkEncoder.Frames.Add(BitmapFrame.Create(darkness));darkEncoder.Save(darkStream);
            }
            vm.SelectGame("wonderlands");
            if(vm.IsWonderlands)
            {
                if(vm.Modules.Count!=1||!vm.Modules[0].Selected||vm.DarknessOptions!=Visibility.Collapsed||vm.SingularityOptions!=Visibility.Collapsed)throw new InvalidDataException("Wonderlands adapter/options incomplete");
                vm.ManualExport=true;Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.Background);
                if(vm.InstallActionLabel!=vm.L["ExportAction"]||vm.L["ManualHint"]=="ManualHint")throw new InvalidDataException("Manual export localization missing");
                root.Measure(new Size(width,height));root.Arrange(new Rect(0,0,width,height));root.UpdateLayout();
                var tiny=new RenderTargetBitmap((int)(width*dpi/96.0),(int)(height*dpi/96.0),dpi,dpi,PixelFormats.Pbgra32);tiny.Render(root);
                using(var stream=File.Create(Path.Combine(output,"wonderlands-manual-"+file))){var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(tiny));encoder.Save(stream);}
                vm.ManualExport=false;
            }
            window.Close();
        }
        if(errors.Messages.Count>0)throw new InvalidDataException(string.Join("\n",errors.Messages));
        Json.Write(Path.Combine(output,"ui-report.json"),new{status="PASS",bindingErrors=errors.Messages,rows});
    }
    static void CheckReconstructionWarning(InstallerViewModel vm)
    {
        var build=new GameLocation("GOG or disc folder","Manual",false,"Unverified",new string('D',64));
        var buildWarning=vm.UnverifiedExecutableText(build);
        if(!buildWarning.Contains(build.Root)||!buildWarning.Contains(build.ExeHash!)||vm.L["UnverifiedTitle"]=="UnverifiedTitle"||buildWarning.Contains("UnverifiedWarning"))throw new InvalidDataException("Executable warning omitted build identity or localization");
        var warning=new ReconstructionWarning("RvGame/CookedPC/test.xxx","owned.xxx","none","original",5,new string('A',64),new string('B',64));
        var message=vm.MismatchText(warning);
        if(!message.Contains(warning.Path)||!message.Contains(warning.ExpectedSha256)||!message.Contains(warning.ActualSha256)||vm.L["MismatchTitle"]=="MismatchTitle"||message.Contains("MismatchWarning"))throw new InvalidDataException("Mismatch warning omitted identity, hashes or localization");
        var receipt=new InstallReceipt("trilex-install-v1","synthetic","singularity","test",new("en","en","en",[]),[],"synthetic",[warning]);
        if(!vm.CompletionText(receipt).Contains(vm.L["InstalledExceptions"])||vm.CompletionText(receipt).Contains(vm.L["Done"])||!vm.VerificationText([],receipt).Contains(vm.L["VerifiedExceptions"])||!vm.VerificationText([],receipt).Contains(warning.Path))throw new InvalidDataException("Accepted mismatch was presented as clean verification");
        if(vm.VerificationText([warning.Path],receipt).Contains(vm.L["VerifiedExceptions"]))throw new InvalidDataException("Exception status concealed later file damage");
        var clean=receipt with{ReconstructionWarnings=null};
        if(!vm.CompletionText(clean).Contains(vm.L["Done"])||vm.VerificationText([],clean)!=vm.L["Verified"])throw new InvalidDataException("Strict-mode completion regressed");
    }
    static void CheckInstallActivity(InstallerViewModel vm,FrameworkElement root,string output,string name)
    {
        var completion=new TaskCompletionSource();IProgress<ProgressInfo>? progress=null;
        var operation=vm.Run(ct=>{progress=vm.BeginInstallProgress("synthetic-ui-test");return completion.Task;});
        var language=vm.UiLanguage;
        vm.UiLanguage=language;vm.UiLanguage=language=="pl"?"en":"pl";
        progress!.Report(new("Extract","vr-core",1,12));progress.Report(new("Prepare","Binaries/texture.dds",47,178));
        Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.Background);root.UpdateLayout();
        if(!vm.Busy||vm.Activity!=vm.L["Installing"]||vm.UiLanguage!=language||vm.Percent!=0)throw new InvalidDataException("Installation message reset or displayed misleading phase percentage");
        if(Descendants(root).OfType<Button>().Single(b=>b.Content?.ToString()==vm.L["Back"]).IsEnabled||Descendants(root).OfType<ComboBox>().Single(c=>System.Windows.Automation.AutomationProperties.GetName(c)=="Installer language").IsEnabled)throw new InvalidDataException("Busy navigation/language guard missing");
        if(!Descendants(root).OfType<Button>().Single(b=>b.Content?.ToString()==vm.L["Cancel"]).IsEnabled||!Descendants(root).OfType<ProgressBar>().Single().IsIndeterminate)throw new InvalidDataException("Busy feedback/cancellation unavailable");
        Descendants(root).OfType<TextBlock>().Single(t=>t.Text==vm.L["Installing"]).BringIntoView();root.UpdateLayout();
        var busyBitmap=new RenderTargetBitmap((int)root.ActualWidth,(int)root.ActualHeight,96,96,PixelFormats.Pbgra32);busyBitmap.Render(root);
        using(var stream=File.Create(Path.Combine(output,"installing-"+name+".png"))){var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(busyBitmap));encoder.Save(stream);}
        completion.SetException(new IOException("synthetic precise failure"));
        Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.Background);operation.GetAwaiter().GetResult();
        var error=vm.L["Error"]+": synthetic precise failure";vm.UiLanguage=language;
        progress.Report(new("Install","late queued diagnostic",178,178));
        Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.Background);
        if(vm.Busy||vm.Activity!=error||Descendants(root).OfType<ProgressBar>().Single().IsIndeterminate)throw new InvalidDataException("Error result overwritten by language/progress or busy animation retained");
        var cancelled=vm.Run(ct=>Task.Delay(Timeout.Infinite,ct));vm.Cancel();
        Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.Background);cancelled.GetAwaiter().GetResult();
        if(vm.Busy||vm.Activity!=vm.L["Cancelled"])throw new InvalidDataException("Cancellation status not restored");
    }
    static IEnumerable<DependencyObject> Descendants(DependencyObject root){for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);yield return child;foreach(var nested in Descendants(child))yield return nested;}}
    sealed class BindingErrors : TraceListener
    {
        public List<string> Messages{get;}=[];
        public override void Write(string? message){if(!string.IsNullOrWhiteSpace(message))Messages.Add(message);}
        public override void WriteLine(string? message)=>Write(message);
    }
}
