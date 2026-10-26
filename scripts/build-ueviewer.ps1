param([string]$Archive=$env:TRILEX_UEVIEWER_ARCHIVE)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(!$Archive){$Archive=Join-Path $root '../research/tools/UEViewer/umodel_win32.zip'}
if(!(Test-Path -LiteralPath $Archive)){
 $Archive=Join-Path $root '.build/umodel_win32.zip'
 New-Item -ItemType Directory -Force (Split-Path $Archive -Parent) | Out-Null
 if(!(Test-Path -LiteralPath $Archive)){Invoke-WebRequest 'https://www.gildor.org/down/47/umodel/umodel_win32.zip' -OutFile $Archive}
}
if((Get-FileHash -LiteralPath $Archive -Algorithm SHA256).Hash -ne '2CEAD261B360DBCC3E703F091837B415F868250DD6A73A154F4232D09E226EF8'){throw 'Unreviewed UE Viewer archive'}
$out=Join-Path $root '.build/ueviewer'
New-Item -ItemType Directory -Force $out | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip=[IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $Archive).Path)
try {
 foreach($name in @('umodel.exe','SDL2.dll','LICENSE.txt','readme.txt')){
  $entry=$zip.GetEntry($name);if(!$entry){throw 'Incomplete verified extractor archive'}
  [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,(Join-Path $out $name),$true)
 }
}finally{$zip.Dispose()}
if((Get-Content (Join-Path $out 'LICENSE.txt') -Raw) -notmatch 'The MIT License'){throw 'Missing UE Viewer license'}
