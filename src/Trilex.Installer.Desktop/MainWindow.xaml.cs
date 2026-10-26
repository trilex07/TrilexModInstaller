using System.Windows;
using Microsoft.Win32;

namespace Trilex.Installer.Desktop;
public partial class MainWindow : Window
{
    private readonly InstallerViewModel vm=new();
    public MainWindow()
    {
        InitializeComponent();DataContext=vm;
        Closing+=(s,e)=>{if(vm.Busy){vm.Cancel();e.Cancel=true;}else vm.Dispose();};
    }
    private async void Detect_Click(object sender,RoutedEventArgs e)=>await vm.Run(vm.Detect);
    private async void Validate_Click(object sender,RoutedEventArgs e)=>await vm.Run(vm.Validate);
    private async void Browse_Click(object sender,RoutedEventArgs e)
    {
        var dialog=new OpenFolderDialog{Title=vm.L["Folder"],Multiselect=false};if(dialog.ShowDialog(this)==true){vm.GameRoot=dialog.FolderName;await vm.Run(vm.Validate);}
    }
    private async void Review_Click(object sender,RoutedEventArgs e)=>await vm.Run(vm.Review);
    private async void Install_Click(object sender,RoutedEventArgs e)=>await vm.Run(ct=>vm.Execute(ct));
    private async void Repair_Click(object sender,RoutedEventArgs e)=>await vm.Run(ct=>vm.Execute(ct,true));
    private async void Verify_Click(object sender,RoutedEventArgs e)=>await vm.Run(vm.Verify);
    private async void Update_Click(object sender,RoutedEventArgs e)=>await vm.Run(vm.Update);
    private async void Recover_Click(object sender,RoutedEventArgs e)=>await vm.Run(vm.Recover);
    private async void Uninstall_Click(object sender,RoutedEventArgs e){if(MessageBox.Show(this,vm.L["ConfirmRestore"],"Trilex",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes)await vm.Run(vm.Uninstall);}
    private void Cancel_Click(object sender,RoutedEventArgs e)=>vm.Cancel();
    private void Back_Click(object sender,RoutedEventArgs e){vm.GameRoot=vm.GameRoot;}
    private void Launch_Click(object sender,RoutedEventArgs e)=>vm.Launch();
    private void Logs_Click(object sender,RoutedEventArgs e)=>vm.OpenLogs();
    private async void Import_Click(object sender,RoutedEventArgs e){var dialog=new OpenFileDialog{Filter="Signed catalog|*.json",Multiselect=false};if(dialog.ShowDialog(this)==true)await vm.Run(ct=>{vm.ImportCatalog(dialog.FileName);return Task.CompletedTask;});}
    private async void Cache_Click(object sender,RoutedEventArgs e){var dialog=new OpenFileDialog{Filter="Release packages|*.zip",Multiselect=true};if(dialog.ShowDialog(this)==true)await vm.Run(ct=>{vm.ImportPackages(dialog.FileNames);return Task.CompletedTask;});}
}
