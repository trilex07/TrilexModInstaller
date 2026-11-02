param(
 [Parameter(Mandatory=$true)][string]$Bundle,
 [string]$Archive=$env:TRILEX_UEVIEWER_ARCHIVE
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(!$Archive){$Archive=Join-Path $root '.build/ueviewer-http-reference.zip'}
if(!(Test-Path -LiteralPath $Archive -PathType Leaf)){throw 'Pinned UE Viewer archive required; set TRILEX_UEVIEWER_ARCHIVE before packaging'}
if((Get-FileHash -LiteralPath $Archive -Algorithm SHA256).Hash -ne '2CEAD261B360DBCC3E703F091837B415F868250DD6A73A154F4232D09E226EF8'){throw 'Unreviewed UE Viewer archive; refusing bundle'}
$tools=Join-Path $Bundle 'tools'
$notices=Join-Path $Bundle 'licenses/ueviewer'
New-Item -ItemType Directory -Force $tools,$notices | Out-Null
# Preserve the complete upstream archive and its notices. Import independently
# verifies the archive, entry allowlist and both runnable x86 binary identities.
Copy-Item -LiteralPath $Archive -Destination (Join-Path $tools 'umodel_win32.zip')
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip=[IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $Archive).Path)
try{
 foreach($name in @('LICENSE.txt','readme.txt')){
  $entry=$zip.GetEntry($name);if(!$entry){throw 'Missing upstream exporter notices'}
  [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,(Join-Path $notices $name),$true)
 }
}finally{$zip.Dispose()}
Copy-Item -LiteralPath (Join-Path $root 'third_party/ueviewer/SDL2-COPYING.txt'),(Join-Path $root 'docs/distribution/UEVIEWER.md') -Destination $notices -Force
