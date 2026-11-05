param([switch]$InstallDependencies,[string]$AssetTools='C:/Users/Trilex/Darkness2VR/tools')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Set-Location $root
$deps=Join-Path $root '.build/texture-deps'
$previous=$env:PYTHONPATH
try {
 $env:PYTHONPATH=$deps
 python -s -c "import PyInstaller,numpy,PIL;assert (PyInstaller.__version__,numpy.__version__,PIL.__version__)==('6.20.0','2.5.3','12.3.0')" 2>$null
 if($LASTEXITCODE){
  if(!$InstallDependencies){throw 'Run build-darkness-textures.ps1 -InstallDependencies'}
  python -s -m pip install --target $deps --only-binary=:all: pyinstaller==6.20.0 numpy==2.5.3 pillow==12.3.0
  if($LASTEXITCODE){throw 'Texture worker dependencies failed'}
 }
 python -s -m PyInstaller --noconfirm --onefile --name Trilex.DarknessTextures --distpath .build/darkness-textures --workpath .build/darkness-textures-build --specpath .build --add-binary "$AssetTools/quickbms/quickbms.exe;." --add-data "$AssetTools/darkness2.bms;." tools/darkness-textures/main.py
 if($LASTEXITCODE){throw 'Texture worker build failed'}
 New-Item -ItemType Directory .build/darkness-textures/licenses -Force | Out-Null
 Copy-Item "$AssetTools/quickbms/quickbms.txt" .build/darkness-textures/licenses
}
finally{$env:PYTHONPATH=$previous}
