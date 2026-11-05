param([string]$PublishDirectory='artifacts/publish')
$ErrorActionPreference='Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
$publish=Join-Path $PWD $PublishDirectory
$deps=Get-Content 'src/Trilex.Installer.Desktop/bin/Release/net10.0-windows/win-x64/TrilexModInstaller.deps.json' -Raw | ConvertFrom-Json
$notices=Join-Path $publish 'licenses'
New-Item -ItemType Directory -Force -Path $notices | Out-Null
foreach($runtime in $deps.libraries.PSObject.Properties.Name | Where-Object {$_ -like 'runtimepack.*'}){
    $parts=$runtime.Substring('runtimepack.'.Length).Split('/')
    $package=Join-Path $env:NUGET_PACKAGES ($parts[0].ToLowerInvariant()+'/'+$parts[1])
    $licenseFiles=@(Get-ChildItem -LiteralPath $package -File | Where-Object {$_.Name -like 'LICENSE*' -or $_.Name -like 'THIRD-PARTY-NOTICES*'})
    if(!$licenseFiles.Count){throw "Missing runtime license notices: $runtime"}
    foreach($license in $licenseFiles){Copy-Item -LiteralPath $license.FullName -Destination (Join-Path $notices ($parts[0]+'-'+$parts[1]+'-'+$license.Name)) -Force}
}
Copy-Item -LiteralPath 'LICENSE','THIRD-PARTY-NOTICES.md','docs/RELEASE-NOTES.md' -Destination $publish -Force
$decoderSource=Join-Path $notices 'ue3decode-corresponding-source'
New-Item -ItemType Directory -Force (Join-Path $decoderSource 'third_party/ue3decode'),(Join-Path $decoderSource 'scripts') | Out-Null
Copy-Item -Path 'third_party/ue3decode/*' -Destination (Join-Path $decoderSource 'third_party/ue3decode') -Force
Copy-Item -LiteralPath 'scripts/build-decoder.ps1' -Destination (Join-Path $decoderSource 'scripts') -Force
$imageNotices=Join-Path $notices 'asset-worker'
if(!(Test-Path '.build/asset-worker/licenses/Python-LICENSE.txt')){throw 'Missing bundled image-worker license audit'}
New-Item -ItemType Directory -Force $imageNotices | Out-Null
Copy-Item -Path '.build/asset-worker/licenses/*' -Destination $imageNotices -Recurse -Force
$extractorNotices=Join-Path $notices 'ueviewer'
New-Item -ItemType Directory -Force $extractorNotices | Out-Null
Copy-Item -LiteralPath 'third_party/ueviewer/SDL2-COPYING.txt','docs/distribution/UEVIEWER.md' -Destination $extractorNotices -Force
$textureNotices=Join-Path $notices 'darkness-textures'
New-Item -ItemType Directory -Force $textureNotices | Out-Null
Copy-Item -Path '.build/darkness-textures/licenses/*' -Destination $textureNotices -Recurse -Force
Copy-Item -Path 'tools/darkness-textures/*.py' -Destination $textureNotices -Force
$quickSource=Join-Path $PWD '.build/quickbms-source/quickbms-src-0.12.0.zip'
if(!(Test-Path -LiteralPath $quickSource) -or (Get-FileHash -LiteralPath $quickSource -Algorithm SHA256).Hash -ne 'E96794AF8369FDCB6DD4744E126CC82BDA22329153359C756A4C70B44FD6B328'){throw 'Matching QuickBMS 0.12.0 source archive required'}
Copy-Item -LiteralPath $quickSource -Destination $textureNotices -Force
Copy-Item -LiteralPath 'scripts/build-darkness-textures.ps1','scripts/collect-worker-licenses.py' -Destination $textureNotices -Force
$exe=Join-Path $publish 'TrilexModInstaller.exe'
$sha=(Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash
[IO.File]::WriteAllText((Join-Path $publish 'SHA256SUMS.txt'),"$sha  TrilexModInstaller.exe`n",(New-Object Text.UTF8Encoding($false)))
$zip=Join-Path $publish 'TrilexModInstaller-win-x64.zip'
# Explicit inputs prevent stale/debug files or a previous ZIP entering the bundle.
Compress-Archive -LiteralPath $exe,(Join-Path $publish 'SHA256SUMS.txt'),(Join-Path $publish 'LICENSE'),(Join-Path $publish 'THIRD-PARTY-NOTICES.md'),(Join-Path $publish 'RELEASE-NOTES.md'),$notices -DestinationPath $zip -Force
[IO.File]::WriteAllText((Join-Path $publish 'ZIP-SHA256SUMS.txt'),"$((Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash)  TrilexModInstaller-win-x64.zip`n",(New-Object Text.UTF8Encoding($false)))

