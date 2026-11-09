using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using Trilex.Installer.Core;

namespace Trilex.Installer.Desktop;

internal static class Psvr2ClientSetup
{
    private static byte[] Resource(string name,string expected)
    {
        using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Trilex.Psvr2."+name)
            ??throw new IOException("Missing PSVR2 client resource. Redownload the complete installer: "+name);
        using var output=new MemoryStream();stream.CopyTo(output);var bytes=output.ToArray();
        if(!Paths.Same(Convert.ToHexString(SHA256.HashData(bytes)),expected))throw new InvalidDataException("PSVR2 client resource checksum mismatch: "+name);
        return bytes;
    }
    internal static Dictionary<string,byte[]> Payload()
    {
        var loader=Resource("psvr2_toolkit_capi_loader.dll","30A456CC81C2B0D687FE987C2988EE90DCCB54AC24F52C4AAAF8F79AF7BC82D8");
        return new()
        {
            [Psvr2ClientRuntime.Loader]=loader,[Psvr2ClientRuntime.DarknessLoader]=loader,
            [Psvr2ClientRuntime.DarknessBridge]=Resource("Darkness2Psvr2Bridge.exe","9C2C3F69AD107DB5F2BE886D8F749DAC9AB1756A320EF8CF9056F87A84841697"),
            [Psvr2ClientRuntime.Notice]=System.Text.Encoding.UTF8.GetBytes(FileNotice)
        };
    }
    internal static void RequireIdle()
    {
        foreach(var name in new[]{"DarknessII","Singularity","Wonderlands","Wonderlands-Win64-Shipping","Darkness2Psvr2Bridge","SingularityPsvr2Bridge"})
        {
            var processes=Process.GetProcessesByName(name);
            try {if(processes.Length>0)throw new IOException("Close the game / PSVR2 bridge before installing clients: "+name);}
            finally{foreach(var process in processes)process.Dispose();}
        }
    }
    internal static void Install() => Psvr2ClientRuntime.Install(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),Payload(),RequireIdle);

    internal static void Export(string destination)
    {
        var root=Paths.Safe(destination,"psvr2-clients");
        foreach(var (path,bytes) in Payload())Paths.AtomicBytes(Paths.Safe(root,path),bytes);
        File.WriteAllText(Paths.Safe(root,"README.txt"),
            "Optional PSVR2 clients (no driver installation). Close all three games and their PSVR2 bridges. Back up matching existing files first.\n"+
            "Copy the CONTENTS of this directory's Trilex and Darkness2VR folders to %LOCALAPPDATA%, preserving subfolders. These clients are shared; keep them if another Trilex mod uses them.\n"+
            "For Wonderlands set [Haptics] ToolkitLoader in the game's WonderlandsVR.ini to your actual %LOCALAPPDATA%\\Trilex\\PSVR2Toolkit\\capi\\psvr2_toolkit_capi_loader.dll (expand the variable to the full path).\n"+
            "The PSVR2 Toolkit SteamVR driver and successful eye calibration are still required. Use the installer's PSVR2 Toolkit button or upstream installation instructions.\n");
    }
    private const string FileNotice="""
PSVR2 client runtime
Toolkit CAPI loader: Copyright (c) 2026 Bnuuy Solutions, MIT.
https://github.com/BnuuySolutions/PSVR2Toolkit (v1.0.0-experimental-3)
Darkness2VR bridge: Trilex / letsgosportsteam, MIT; adapted from
KHARVOX, Copyright (c) 2026 CactusVRStudios, MIT.
https://github.com/CactusVRStudios/KHARVOX
https://github.com/trilex07/thedarkness2vr

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
""";
}
