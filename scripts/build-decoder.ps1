$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$out=Join-Path $root '.build/ue3decode'
New-Item -ItemType Directory -Force $out | Out-Null
$vswhere="${env:ProgramFiles(x86)}/Microsoft Visual Studio/Installer/vswhere.exe"
$vs=& $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if(!$vs){throw 'Visual Studio C++ x64 tools required for bundled standalone GPL decoder'}
$vc=Join-Path $vs 'VC/Auxiliary/Build/vcvarsall.bat'
$source=Join-Path $root 'third_party/ue3decode/worker.cpp'
$exe=Join-Path $out 'Trilex.Ue3Decode.exe'
$obj=Join-Path $out 'worker.obj'
$cmd="`"$vc`" x64 && cl /nologo /EHsc /O2 /MT /W4 /std:c++17 /Brepro /Fe:`"$exe`" /Fo:`"$obj`" `"$source`" /link /DYNAMICBASE /NXCOMPAT"
& cmd /c $cmd
if($LASTEXITCODE){throw 'Decoder build failed'}
Get-FileHash -LiteralPath $exe
