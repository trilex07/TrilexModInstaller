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
            if(!labels.Contains("PSVR2") || !labels.Contains(vm.L["Modules"]))throw new InvalidDataException("Localized module content missing");
            if(vm.Modules.Count!=7 || vm.Modules.Any(x=>x.Selectable))throw new InvalidDataException("Unapproved packs must remain unavailable");
            if(Descendants(root).OfType<Button>().Any(b=>b.Content?.ToString()==vm.L["Launch"]&&b.IsEnabled))throw new InvalidDataException("Launch enabled without managed installation");
            var bmp=new RenderTargetBitmap((int)(width*dpi/96.0),(int)(height*dpi/96.0),dpi,dpi,PixelFormats.Pbgra32);bmp.Render(root);
            var file=$"installer-{language}-{width}x{height}-{dpi}dpi.png";using(var stream=File.Create(Path.Combine(output,file))){var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bmp));encoder.Save(stream);}
            rows.Add(new{language,width,height,dpi,file,status="PASS",scope="WPF layout/bindings/offscreen pixels; not physical DPI/input acceptance"});window.Close();
        }
        if(errors.Messages.Count>0)throw new InvalidDataException(string.Join("\n",errors.Messages));
        Json.Write(Path.Combine(output,"ui-report.json"),new{status="PASS",bindingErrors=errors.Messages,rows});
    }
    static IEnumerable<DependencyObject> Descendants(DependencyObject root){for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);yield return child;foreach(var nested in Descendants(child))yield return nested;}}
    sealed class BindingErrors : TraceListener
    {
        public List<string> Messages{get;}=[];
        public override void Write(string? message){if(!string.IsNullOrWhiteSpace(message))Messages.Add(message);}
        public override void WriteLine(string? message)=>Write(message);
    }
}
