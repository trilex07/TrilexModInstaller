param(
 [Parameter(Mandatory=$true)][string]$Inputs,
 [Parameter(Mandatory=$true)][string]$Output,
 [string]$Tag='installer-1.0.0-rc.1',
 [string]$SigningKey='private/publisher.pem'
)
$ErrorActionPreference='Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
$env:DOTNET_CLI_HOME=Join-Path $PWD '.build/dotnet-home'
$env:NUGET_PACKAGES=Join-Path $PWD '.build/nuget'
$env:MSBuildEnableWorkloadResolver='false'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
# Build/tests must pass first; this command does not upload, deploy, sign with
# Authenticode or regenerate publisher keys. Producer enforces fresh artifacts.
python scripts/prepare-first-edition.py --inputs $Inputs --output $Output --tag $Tag
if($LASTEXITCODE){throw 'Package assembly failed'}
$full=(Resolve-Path -LiteralPath $Output).Path
$tool='src/Trilex.Installer.Tools/bin/Release/net10.0/Trilex.Installer.Tools.dll'
dotnet $tool sign (Join-Path $full 'catalog.json') $SigningKey (Join-Path $full 'catalog.signed.json')
if($LASTEXITCODE){throw 'Catalog signing failed'}
dotnet $tool verify (Join-Path $full 'catalog.signed.json') catalog/trusted-publisher.pem
if($LASTEXITCODE){throw 'Publisher key does not match pinned trust'}
$catalog=Join-Path $full 'catalog.signed.json'
dotnet publish src/Trilex.Installer.Desktop -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=embedded "-p:BootstrapCatalog=$catalog" -o artifacts/publish --no-restore --nologo
if($LASTEXITCODE){throw 'Complete installer publish failed'}
& scripts/package-portable.ps1
$bundle=Join-Path $full 'Trilex-SingularityVR-V7'
New-Item -ItemType Directory -Path $bundle | Out-Null
foreach($name in @('TrilexModInstaller.exe','LICENSE','THIRD-PARTY-NOTICES.md','licenses')){Copy-Item -LiteralPath (Join-Path $PWD "artifacts/publish/$name") -Destination $bundle -Recurse}
Copy-Item -LiteralPath $catalog -Destination $bundle
$bundlePackages=Join-Path $bundle 'packages'
New-Item -ItemType Directory -Path $bundlePackages | Out-Null
Get-ChildItem -LiteralPath (Join-Path $full 'packages') -Filter '*.zip' -File | Copy-Item -Destination $bundlePackages
Copy-Item -LiteralPath docs/INSTALL-V7.md,docs/DISTRIBUTION-NOTICE.md,docs/distribution/FIRST-EDITION.md -Destination $bundle
$rows=Get-ChildItem -LiteralPath $bundle -Recurse -File | Sort-Object FullName | ForEach-Object {"$((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash)  $([IO.Path]::GetRelativePath($bundle,$_.FullName).Replace('\','/'))"}
[IO.File]::WriteAllLines((Join-Path $bundle 'SHA256SUMS.txt'),$rows,[Text.UTF8Encoding]::new($false))
$zip=Join-Path $full 'Trilex-SingularityVR-V7-complete-win-x64.zip'
Compress-Archive -LiteralPath $bundle -DestinationPath $zip
Get-FileHash -LiteralPath $zip,(Join-Path $bundle 'TrilexModInstaller.exe') -Algorithm SHA256
[IO.File]::WriteAllText((Join-Path $full 'ZIP-SHA256SUMS.txt'),"$((Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash)  $([IO.Path]::GetFileName($zip))`n",[Text.UTF8Encoding]::new($false))
Write-Output 'Complete local unsigned candidate prepared. No upload or Steam modification performed.'
