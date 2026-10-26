$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$out=Join-Path $root '.build/recipe-maker'; New-Item -ItemType Directory -Force $out | Out-Null
$vswhere="${env:ProgramFiles(x86)}/Microsoft Visual Studio/Installer/vswhere.exe"
$vs=& $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if(!$vs){throw 'Visual Studio C++ tools required'}
$vc=Join-Path $vs 'VC/Auxiliary/Build/vcvarsall.bat';$source=Join-Path $root 'tools/recipe-maker/main.cpp'
$exe=Join-Path $out 'recipe-maker.exe';$obj=Join-Path $out 'recipe-maker.obj'
& cmd /c "`"$vc`" x64 && cl /nologo /EHsc /O2 /MT /W4 /std:c++17 /Brepro /Fe:`"$exe`" /Fo:`"$obj`" `"$source`""
if($LASTEXITCODE){throw 'Recipe producer failed'}
