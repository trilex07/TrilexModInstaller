param([string]$ReleaseDirectory='artifacts/installer-1.0.9')
$ErrorActionPreference='Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
$release=Join-Path $PWD $ReleaseDirectory
$publish=Join-Path $release 'publish'
$bundle=Join-Path $release 'bundle'
$public=Join-Path $release 'release-assets'
New-Item -ItemType Directory -Force $public | Out-Null
$env:NUGET_PACKAGES=Join-Path $PWD '.build/nuget'
& ./scripts/package-portable.ps1 -PublishDirectory ($ReleaseDirectory+'/publish')
foreach($name in @('TrilexModInstaller.exe','LICENSE','THIRD-PARTY-NOTICES.md','RELEASE-NOTES.md')){Copy-Item -LiteralPath (Join-Path $publish $name) -Destination $bundle -Force}
Copy-Item -LiteralPath (Join-Path $publish 'licenses') -Destination $bundle -Recurse -Force
Copy-Item -LiteralPath 'README.md' -Destination $bundle -Force
foreach($document in @('docs/releases/installer-1.0.9.md','docs/DISTRIBUTION-NOTICE.md','docs/distribution/FIRST-EDITION.md','docs/screenshots/installer-en.png')){
 $destination=Join-Path $bundle $document
 New-Item -ItemType Directory -Force (Split-Path $destination -Parent) | Out-Null
 Copy-Item -LiteralPath $document -Destination $destination -Force
}
Copy-Item -LiteralPath (Join-Path $release 'catalog.signed.json') -Destination $bundle -Force
& ./scripts/bundle-ueviewer.ps1 -Bundle $bundle
# Preserve runtime notices in the portable archive as well as the installed assets.
Add-Type -AssemblyName System.IO.Compression.FileSystem
$nativeNotices=Join-Path $bundle 'licenses/vr-runtime'
New-Item -ItemType Directory -Force $nativeNotices | Out-Null
$zip=[IO.Compression.ZipFile]::OpenRead((Join-Path $bundle 'packages/vr-core.zip'))
try{foreach($entry in $zip.Entries | Where-Object FullName -like 'Binaries/vr-assets/licenses/*'){
 [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,(Join-Path $nativeNotices ([IO.Path]::GetFileName($entry.FullName))),$true)
}}finally{$zip.Dispose()}
Copy-Item -LiteralPath (Join-Path $release 'catalog.signed.json') -Destination $public -Force
foreach($package in Get-ChildItem (Join-Path $bundle 'packages/darkness2') -File -Filter '*.zip'){
 Copy-Item -LiteralPath $package.FullName -Destination (Join-Path $public ('darkness2-'+$package.Name)) -Force
}
foreach($package in Get-ChildItem (Join-Path $bundle 'packages/wonderlands') -File -Filter '*.zip'){
 Copy-Item -LiteralPath $package.FullName -Destination (Join-Path $public ('wonderlands-'+$package.Name)) -Force
}
$wonderNotices=Join-Path $bundle 'licenses/wonderlands'
New-Item -ItemType Directory -Force $wonderNotices | Out-Null
$wonderZip=[IO.Compression.ZipFile]::OpenRead((Join-Path $bundle 'packages/wonderlands/vr-core.zip'))
try{foreach($entry in $wonderZip.Entries | Where-Object FullName -Match 'runtime/(LICENSE-|THIRD-PARTY)'){
 [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,(Join-Path $wonderNotices ([IO.Path]::GetFileName($entry.FullName))),$true)
}}finally{$wonderZip.Dispose()}
$manifest=@(Get-ChildItem $bundle -File -Recurse | Where-Object {$_.FullName -ne (Join-Path $bundle 'SHA256SUMS.txt')} | Sort-Object FullName | ForEach-Object {
 $relative=[IO.Path]::GetRelativePath($bundle,$_.FullName).Replace('\','/')
 "$((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash)  $relative"
})
[IO.File]::WriteAllLines((Join-Path $bundle 'SHA256SUMS.txt'),$manifest,[Text.UTF8Encoding]::new($false))
$archive=Join-Path $public 'Trilex-Mod-Installer-1.0.9-win-x64.zip'
if(Test-Path -LiteralPath $archive){Remove-Item -LiteralPath $archive}
[IO.Compression.ZipFile]::CreateFromDirectory($bundle,$archive,[IO.Compression.CompressionLevel]::Optimal,$false)
$checks=@(Get-ChildItem $public -File | Where-Object Name -ne 'SHA256SUMS.txt' | Sort-Object Name | ForEach-Object {"$((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash)  $($_.Name)"})
[IO.File]::WriteAllLines((Join-Path $public 'SHA256SUMS.txt'),$checks,[Text.UTF8Encoding]::new($false))
Write-Output $archive
