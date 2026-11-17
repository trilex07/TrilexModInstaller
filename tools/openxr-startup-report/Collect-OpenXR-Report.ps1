param([string]$OutputPath = (Join-Path $PSScriptRoot 'OpenXR-startup-report.json'))
# Read-only inventory: no DLL loads, game launches, registry or runtime changes.
$ErrorActionPreference = 'Stop'
function Get-PeInfo([string]$Path) {
    $stream = $null
    $reader = $null
    try {
        $stream = [IO.File]::Open($Path, 'Open', 'Read', 'ReadWrite')
        $reader = New-Object IO.BinaryReader($stream)
        if ($reader.ReadUInt16() -ne 0x5a4d) { throw 'Not a PE image' }
        $stream.Position = 0x3c
        $offset = $reader.ReadInt32()
        if ($offset -lt 0 -or $offset -gt ($stream.Length - 6)) { throw 'Invalid PE offset' }
        $stream.Position = $offset
        if ($reader.ReadUInt32() -ne 0x4550) { throw 'Invalid PE signature' }
        $machine = $reader.ReadUInt16()
        $architecture = switch ($machine) { 0x14c {'x86'} 0x8664 {'x64'} 0xaa64 {'arm64'} default {'unknown'} }
        return @{ architecture=$architecture; machine=('0x{0:X4}' -f $machine) }
    } catch { return @{ error=$_.Exception.Message } }
    finally { if ($reader) { $reader.Dispose() } elseif ($stream) { $stream.Dispose() } }
}
function Get-ManifestInfo([string]$Path) {
    $info = [ordered]@{ path=$Path; exists=$false }
    try {
        $expanded = [Environment]::ExpandEnvironmentVariables($Path)
        $info.exists = [IO.File]::Exists($expanded)
        if (-not $info.exists) { return $info }
        $manifest = [IO.File]::ReadAllText($expanded) | ConvertFrom-Json
        $node = $manifest.runtime
        if ($manifest.api_layer) { $node = $manifest.api_layer }
        if (-not $node) { throw 'No runtime or api_layer entry' }
        $info.name = $node.name
        $info.apiVersion = $node.api_version
        $info.libraryPath = $node.library_path
        $info.enableEnvironment = $node.enable_environment
        $info.disableEnvironment = $node.disable_environment
        if ($node.library_path -is [string]) {
            $library = [Environment]::ExpandEnvironmentVariables($node.library_path)
            if (-not [IO.Path]::IsPathRooted($library)) {
                $library = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetDirectoryName($expanded)) $library))
            }
            $info.resolvedLibrary = $library
            $info.libraryExists = [IO.File]::Exists($library)
            if ($info.libraryExists) { $info.pe = Get-PeInfo $library }
        }
    } catch { $info.error=$_.Exception.Message }
    return $info
}
$entries = New-Object 'Collections.Generic.List[object]'
foreach ($viewName in @('Registry32','Registry64')) {
    foreach ($hiveName in @('LocalMachine','CurrentUser')) {
        $base = $null
        try {
            $base = [Microsoft.Win32.RegistryKey]::OpenBaseKey(
                [Microsoft.Win32.RegistryHive]::$hiveName, [Microsoft.Win32.RegistryView]::$viewName)
            foreach ($suffix in @('', '\ApiLayers\Implicit', '\ApiLayers\Explicit')) {
                $subkey = 'SOFTWARE\Khronos\OpenXR\1' + $suffix
                $key = $base.OpenSubKey($subkey, $false)
                if (-not $key) { continue }
                try {
                    foreach ($name in $key.GetValueNames()) {
                        if ($suffix -eq '' -and $name -ne 'ActiveRuntime') { continue }
                        $value = $key.GetValue($name, $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
                        $entry = [ordered]@{ hive=$hiveName; view=$viewName; key=$subkey; name=$name; value=$value }
                        if ($suffix -eq '' -and $value -is [string]) { $entry.manifest=Get-ManifestInfo $value }
                        elseif ($suffix -ne '') { $entry.manifest=Get-ManifestInfo $name }
                        $entries.Add($entry)
                    }
                } finally { $key.Dispose() }
            }
        } catch { $entries.Add(@{ hive=$hiveName; view=$viewName; error=$_.Exception.Message }) }
        finally { if ($base) { $base.Dispose() } }
    }
}
$environment = [ordered]@{}
foreach ($name in @('XR_RUNTIME_JSON','XR_API_LAYER_PATH','XR_ENABLE_API_LAYERS','XR_LOADER_DEBUG',
                     'XR_LOADER_LAYERS_ENABLE','XR_LOADER_LAYERS_DISABLE','XR_LOADER_LAYERS_ALLOW')) {
    $environment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
$report = [ordered]@{
    formatVersion=1; collectedAt=[DateTime]::UtcNow.ToString('o')
    processBits=([IntPtr]::Size * 8); os64Bit=[Environment]::Is64BitOperatingSystem
    note='Read-only inventory, not a runtime load test. Paths may contain your Windows user name. Nothing is uploaded.'
    environment=$environment; registry=$entries.ToArray()
}
if ($environment.XR_RUNTIME_JSON) { $report.environmentRuntime=Get-ManifestInfo $environment.XR_RUNTIME_JSON }
$report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $OutputPath -Encoding UTF8
Write-Host "Saved: $OutputPath"
Write-Host 'No settings changed. Send the JSON file to the mod author after checking its paths.'
