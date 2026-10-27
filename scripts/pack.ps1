# Builds the plugin and packs it as artifacts/kyuplan-plugin-example.kyuplugin (any folder with -Output).
# A package is a zip with plugin.json, the assembly and the JSON files in its root, and with / in the names of the files (the zip format wants it). The zip is
# written entry by entry on purpose: Compress-Archive and ZipFile.CreateFromDirectory of Windows PowerShell 5.1 put backslashes there.
param([string] $Output = (Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts'))

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
New-Item -ItemType Directory -Force $Output | Out-Null
$Output = (Resolve-Path $Output).Path
$publish = Join-Path $Output 'publish'
$package = Join-Path $Output 'kyuplan-plugin-example.kyuplugin'

if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
if (Test-Path $package) { Remove-Item $package -Force }

dotnet publish (Join-Path $root 'src/Kyuplan.Plugins.Example') -c Release -o $publish
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

Add-Type -AssemblyName System.IO.Compression.FileSystem
$publish = (Get-Item $publish).FullName # the same spelling the files below have (not an 8.3 short name)
$zip = [System.IO.Compression.ZipFile]::Open($package, 'Create')
try {
    foreach ($file in Get-ChildItem $publish -Recurse -File) {
        $name = $file.FullName.Substring($publish.Length).TrimStart('\', '/').Replace('\', '/')
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $file.FullName, $name, 'Optimal') | Out-Null
    }
}
finally {
    $zip.Dispose()
}

Write-Host "Packed $package"
