# Publish the staged installer 1.1.4 release (owner-approved 2026-11-23) and verify it from GitHub.
# Requires `gh auth login`. Run from anywhere; it changes to the repository root.
$ErrorActionPreference='Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
$repo='trilex07/TrilexModInstaller'; $tag='installer-1.1.4'; $stage='artifacts/installer-1.1.4'; $assets="$stage/release-assets"
gh auth status; if($LASTEXITCODE){throw 'gh is not logged in: run gh auth login'}
$sums=@{}; foreach($line in Get-Content "$assets/SHA256SUMS.txt"){ if($line){ $h,$n=$line -split '  ',2; $sums[$n]=$h } }
foreach($n in $sums.Keys){ if((Get-FileHash "$assets/$n" -Algorithm SHA256).Hash -ne $sums[$n]){throw "Staged asset changed: $n"} }
$branch=git rev-parse --abbrev-ref HEAD; $commit=git rev-parse HEAD
git push -u origin $branch; if($LASTEXITCODE){throw 'git push failed'}
$files=@('Trilex-Mod-Installer-1.1.4-win-x64.zip','catalog.signed.json','catalog-1.1.13.signed.json','rapture-vr-core-20261123.zip','RaptureVR-test-20261123.zip','SHA256SUMS.txt') | ForEach-Object {"$assets/$_"}
gh release create $tag --repo $repo --target $commit --latest --title "Trilex Mod Installer 1.1.4 - Everybody's Gone to the Rapture VR (preview)" --notes-file "$stage/RELEASE-BODY.md" @files
if($LASTEXITCODE){throw 'gh release create failed'}
$back="$stage/public-download"; if(Test-Path $back){Remove-Item -Recurse -Force $back}; New-Item -ItemType Directory $back | Out-Null
gh release download $tag --repo $repo --dir $back; if($LASTEXITCODE){throw 'download back failed'}
$remote=Get-Content "$back/SHA256SUMS.txt"; if(($remote -join "`n") -ne ((Get-Content "$assets/SHA256SUMS.txt") -join "`n")){throw 'remote SHA256SUMS differs'}
foreach($n in $sums.Keys){ $h=(Get-FileHash "$back/$n" -Algorithm SHA256).Hash; if($h -ne $sums[$n]){throw "Downloaded hash mismatch: $n"}; "PASS $n $h" }
$latest=gh api "repos/$repo/releases/latest" --jq .tag_name; if($latest -ne $tag){throw "releases/latest is $latest"}; "PASS releases/latest = $latest"
$cache="$stage/verify-downloads-cache"; if(Test-Path $cache){Remove-Item -Recurse -Force $cache}
dotnet run --project src/Trilex.Installer.Tools -c Release --no-build -- verify-downloads "$back/catalog-1.1.13.signed.json" catalog/trusted-publisher.pem rapture $cache vr-core
if($LASTEXITCODE){throw 'verify-downloads failed'}
dotnet run --project src/Trilex.Installer.Tools -c Release --no-build -- verify "$back/catalog.signed.json" catalog/trusted-publisher.pem
