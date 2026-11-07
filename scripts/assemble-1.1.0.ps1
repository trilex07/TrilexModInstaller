param([string]$ReleaseDirectory='artifacts/installer-1.1.0',[string]$PreviousRelease='artifacts/installer-1.0.9')
$ErrorActionPreference='Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
$release=Join-Path $PWD $ReleaseDirectory
$catalog=Join-Path $release 'catalog.signed.json'
if(!(Test-Path -LiteralPath $catalog)){throw 'Sign catalog.unsigned.json first'}
dotnet run --project src/Trilex.Installer.Tools -c Release -- verify $catalog catalog/trusted-publisher.pem
if($LASTEXITCODE){throw 'Catalog verification failed'}
$env:NUGET_PACKAGES=Join-Path $PWD '.build/nuget'
dotnet publish src/Trilex.Installer.Desktop -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=embedded "-p:BootstrapCatalog=$catalog" -o ($ReleaseDirectory+'/publish') --nologo
if($LASTEXITCODE){throw 'Publish failed'}
# Game packages are unchanged from the previous release; reuse its verified bundle tree.
$bundle=Join-Path $release 'bundle'
if(Test-Path -LiteralPath $bundle){Remove-Item -LiteralPath $bundle -Recurse -Force}
Copy-Item -LiteralPath (Join-Path $PWD "$PreviousRelease/bundle") -Destination $bundle -Recurse
Remove-Item -LiteralPath (Join-Path $bundle 'docs/releases') -Recurse -Force -ErrorAction SilentlyContinue
& ./scripts/assemble-1.0.9.ps1 -ReleaseDirectory $ReleaseDirectory -ReleaseNotes 'docs/releases/installer-1.1.0.md' -Version '1.1.0'
