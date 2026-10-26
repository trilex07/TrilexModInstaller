param([switch]$Publish,[switch]$SkipUi)
$ErrorActionPreference='Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
$env:DOTNET_CLI_HOME=Join-Path $PWD '.build/dotnet-home'
$env:NUGET_PACKAGES=Join-Path $PWD '.build/nuget'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:MSBuildEnableWorkloadResolver='false'
$env:TRILEX_TEST_ROOT=Join-Path $PWD 'artifacts/test-fixtures'
dotnet run --project tests/Trilex.Installer.Tests -c Release
if($LASTEXITCODE){throw 'Core regressions failed'}
dotnet build src/Trilex.Installer.Tools -c Release --nologo
if($LASTEXITCODE){throw 'Tools build failed'}
dotnet run --project src/Trilex.Installer.Tools -c Release --no-build -- verify catalog/bootstrap.json catalog/trusted-publisher.pem
if($LASTEXITCODE){throw 'Bootstrap signature failed'}
dotnet build src/Trilex.Installer.Desktop -c Release --nologo
if($LASTEXITCODE){throw 'Desktop build failed'}
if(!$SkipUi){
    $env:TRILEX_HOME=Join-Path $PWD 'artifacts/clean-ui-profile'
    $exe=Join-Path $PWD 'src/Trilex.Installer.Desktop/bin/Release/net10.0-windows/TrilexModInstaller.exe'
    $out=Join-Path $PWD 'artifacts/ui-smoke'
    $process=Start-Process -FilePath $exe -ArgumentList @('--render-smoke',('"'+$out+'"')) -PassThru -WindowStyle Hidden -Wait
    if($process.ExitCode -ne 0){throw 'WPF smoke failed; see artifacts/ui-smoke/failure.txt'}
    $report=Get-Content (Join-Path $out 'ui-report.json') -Raw | ConvertFrom-Json
    if($report.status -ne 'PASS' -or $report.rows.Count -ne 8){throw 'Incomplete WPF smoke'}
}
if($Publish){
    dotnet publish src/Trilex.Installer.Desktop -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=embedded -o artifacts/publish --nologo
    if($LASTEXITCODE){throw 'Self-contained publish failed'}
    $file=Join-Path $PWD 'artifacts/publish/TrilexModInstaller.exe'
    if(!$SkipUi){
        # Exercise the published bundle without using the machine's installed runtime.
        # Keep native-library extraction inside the disposable build workspace.
        $names=@('TRILEX_HOME','DOTNET_ROOT','DOTNET_MULTILEVEL_LOOKUP','DOTNET_BUNDLE_EXTRACT_BASE_DIR')
        $saved=@{}
        foreach($name in $names){$saved[$name]=[Environment]::GetEnvironmentVariable($name,'Process')}
        try{
            $env:TRILEX_HOME=Join-Path $PWD 'artifacts/portable-profile'
            $env:DOTNET_ROOT=Join-Path $PWD 'artifacts/nonexistent-runtime'
            $env:DOTNET_MULTILEVEL_LOOKUP='0'
            $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR=Join-Path $PWD '.build/bundle'
            $out=Join-Path $PWD 'artifacts/portable-smoke'
            New-Item -ItemType Directory -Force -Path $out,$env:DOTNET_BUNDLE_EXTRACT_BASE_DIR | Out-Null
            $process=Start-Process -FilePath $file -ArgumentList @('--render-smoke',('"'+$out+'"')) -PassThru -WindowStyle Hidden -Wait -RedirectStandardOutput (Join-Path $out 'stdout.txt') -RedirectStandardError (Join-Path $out 'stderr.txt')
            if($process.ExitCode -ne 0){throw 'Published bundle smoke failed; see artifacts/portable-smoke'}
            $report=Get-Content (Join-Path $out 'ui-report.json') -Raw | ConvertFrom-Json
            if($report.status -ne 'PASS' -or $report.rows.Count -ne 8){throw 'Incomplete published bundle smoke'}
        }finally{
            foreach($name in $names){[Environment]::SetEnvironmentVariable($name,$saved[$name],'Process')}
        }
    }
    $sha=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash
    "$sha  TrilexModInstaller.exe" | Set-Content artifacts/publish/SHA256SUMS.txt -Encoding utf8NoBOM
    & (Join-Path $PSScriptRoot 'package-portable.ps1')
    Write-Output "Self-contained RC: $file SHA256=$sha"
}
