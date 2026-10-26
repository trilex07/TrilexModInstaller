param([switch]$InstallDependencies)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Set-Location $root
$deps=Join-Path $root '.build/asset-worker-build'
New-Item -ItemType Directory -Force $deps | Out-Null
$previous=$env:PYTHONPATH
try {
    $env:PYTHONPATH=$deps
    & python -s -c "import PyInstaller,numpy,PIL;assert (PyInstaller.__version__,numpy.__version__,PIL.__version__)==('6.20.0','2.5.3','12.3.0')" 2>$null
    if($LASTEXITCODE){
        if(!$InstallDependencies){throw 'Pinned image-worker dependencies missing. Run scripts/build-asset-worker.ps1 -InstallDependencies.'}
        & python -s -m pip install --target $deps --upgrade --only-binary=:all: --disable-pip-version-check pyinstaller==6.20.0 numpy==2.5.3 pillow==12.3.0
        if($LASTEXITCODE){throw 'Pinned image-worker dependency installation failed'}
    }
    & python -s -m PyInstaller --noconfirm --clean --onefile --name Trilex.AssetWorker --distpath .build/asset-worker --workpath .build/asset-worker-build-temp --specpath .build tools/asset-worker/main.py
    if($LASTEXITCODE){throw 'Image worker build failed'}
    & python -s scripts/collect-worker-licenses.py
    if($LASTEXITCODE){throw 'Image worker license collection failed'}
    Get-FileHash -LiteralPath '.build/asset-worker/Trilex.AssetWorker.exe'
}finally{$env:PYTHONPATH=$previous}
