using System.Windows;

namespace Trilex.Installer.Desktop;
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if(e.Args is ["--render-smoke",var output])
        {
            ShutdownMode=ShutdownMode.OnExplicitShutdown;
            try{UiSmoke.Run(output);Shutdown(0);}catch(Exception ex){Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"failure.txt"),ex.ToString());Shutdown(1);}return;
        }
        if(e.Args is ["--check-idle",var gameRoot,var receipt])
        {
            ShutdownMode=ShutdownMode.OnExplicitShutdown;
            try{new WindowsProcessGuard().RequireIdle(gameRoot);File.WriteAllText(receipt,"PASS: actual Windows process/Steam registry guard permits idle installation; no game writes");Shutdown(0);}
            catch(Exception ex){File.WriteAllText(receipt,ex.ToString());Shutdown(1);}return;
        }
        DispatcherUnhandledException+=(s,a)=>{MessageBox.Show(a.Exception.Message,"Trilex Mod Installer",MessageBoxButton.OK,MessageBoxImage.Error);a.Handled=true;};
        new MainWindow().Show();
    }
}
