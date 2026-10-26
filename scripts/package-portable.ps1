$ErrorActionPreference='Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
$publish=Join-Path $PWD 'artifacts/publish'
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
$exe=Join-Path $publish 'TrilexModInstaller.exe'
$sha=(Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash
"$sha  TrilexModInstaller.exe" | Set-Content (Join-Path $publish 'SHA256SUMS.txt') -Encoding utf8NoBOM
$zip=Join-Path $publish 'TrilexModInstaller-win-x64.zip'
# Explicit inputs prevent stale/debug files or a previous ZIP entering the bundle.
Compress-Archive -LiteralPath $exe,(Join-Path $publish 'SHA256SUMS.txt'),(Join-Path $publish 'LICENSE'),(Join-Path $publish 'THIRD-PARTY-NOTICES.md'),(Join-Path $publish 'RELEASE-NOTES.md'),$notices -DestinationPath $zip -Force
"$((Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash)  TrilexModInstaller-win-x64.zip" | Set-Content (Join-Path $publish 'ZIP-SHA256SUMS.txt') -Encoding utf8NoBOM

