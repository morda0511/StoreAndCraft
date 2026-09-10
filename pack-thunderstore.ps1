param(
    [Parameter(Mandatory = $true)][string]$Version,
    [Parameter(Mandatory = $true)][string]$PackRoot,
    [Parameter(Mandatory = $true)][string]$OutputDir,
    [Parameter(Mandatory = $false)][string]$PackageName = "StoreAndCraft"
)

$ErrorActionPreference = "Stop"

$manifestPath = Join-Path $PackRoot "manifest.json"
$iconPath = Join-Path $PackRoot "icon.png"
$readmePath = Join-Path $PackRoot "README.md"
$changelogPath = Join-Path $PackRoot "CHANGELOG.md"
$pluginDll = Join-Path $PackRoot "plugins\$PackageName\$PackageName.dll"

foreach ($required in @($manifestPath, $iconPath, $readmePath, $pluginDll)) {
    if (-not (Test-Path $required)) {
        throw "Missing required package file: $required"
    }
}

$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$manifest.version_number = $Version
$json = $manifest | ConvertTo-Json -Depth 8
[System.IO.File]::WriteAllText($manifestPath, $json)

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

$zipName = "$PackageName-$Version.zip"
$zipPath = Join-Path $OutputDir $zipName
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$zip = [System.IO.Compression.ZipFile]::Open($zipPath, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $iconPath, "icon.png")
    [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $manifestPath, "manifest.json")
    [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $readmePath, "README.md")
    if (Test-Path $changelogPath) {
        [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $changelogPath, "CHANGELOG.md")
    }
    [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
        $zip,
        $pluginDll,
        "plugins/$PackageName/$PackageName.dll")
}
finally {
    $zip.Dispose()
}

Write-Host "Thunderstore pack created: $zipPath"
