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
            if(Descendants(root).OfType<Button>().Any(b=>b.Content?.ToString()==vm.L["Launch"]&&b.IsEnabled))throw new InvalidDataException("Launch enabled without managed installation");
            var bmp=new RenderTargetBitmap((int)(width*dpi/96.0),(int)(height*dpi/96.0),dpi,dpi,PixelFormats.Pbgra32);bmp.Render(root);
            var file=$"installer-{language}-{width}x{height}-{dpi}dpi.png";using(var stream=File.Create(Path.Combine(output,file))){var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bmp));encoder.Save(stream);}
            CheckInstallActivity(vm,root,output,$"{language}-{width}x{height}-{dpi}dpi");
            rows.Add(new{language,width,height,dpi,file,status="PASS",activity="install/busy controls/late progress/error/cancel PASS",scope="WPF layout/bindings/offscreen pixels; not physical DPI/input acceptance"});window.Close();
        }
        if(errors.Messages.Count>0)throw new InvalidDataException(string.Join("\n",errors.Messages));
        Json.Write(Path.Combine(output,"ui-report.json"),new{status="PASS",bindingErrors=errors.Messages,rows});
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
