using System.Diagnostics;
using Trilex.Installer.Core;

namespace Trilex.Installer.Desktop;
public sealed class WindowsProcessGuard : IProcessGuard
{
    public void RequireIdle(string root)
    {
        // Installed/retail copies do not require Steam. Its Running registry
        // values can describe unrelated tools or stale sessions, not this game.
        foreach(var process in Process.GetProcesses())
        {
            using(process)
            {
                if(process.Id==Environment.ProcessId)continue;
                string name;try{name=process.ProcessName;}catch(InvalidOperationException){continue;}
                string? path=null;try{path=process.MainModule?.FileName;}catch(System.ComponentModel.Win32Exception){}catch(InvalidOperationException){continue;}
                var refusal=ProcessGuardPolicy.Refusal(root,Environment.ProcessId,process.Id,name,path);
                if(refusal is not null)throw new IOException(refusal);
            }
        }
    }
}
