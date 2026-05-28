# ============================================================
# ClickCS Valheim Modpack — Deploy Script
# ============================================================
# Edit the variables below, then run this script to generate
# your manifest.json. Upload the results to your web server.
# ============================================================

# ---- EDIT THESE ----
$BaseUrl        = "https://clickcs.org/valheim_modpack/files"    # Public URL where /files/ is accessible
$ModpackId      = "clickcs"                                      # Short unique ID (no spaces)
$ModpackName    = "ClickCS Valheim Modpack"                      # Display name players see
$ModpackVersion = "1.0.0"                                        # Bump this every update
$ChangelogUrl   = "https://clickcs.org/valheim_modpack/changelog.txt"  # Optional: URL to changelog.txt
$ValheimVersion = ""                                             # Optional: e.g. "0.219.16"
$BepInExVersion = "5.4.23.3"                                     # Version string embedded in the zip
$BepInExZipPath = Join-Path $PSScriptRoot "files\BepInExPack_Valheim.zip"  # Path to the zip
$BepInExZipUrl  = "https://clickcs.org/valheim_modpack/deps/BepInExPack_Valheim.zip"  # Public URL for the zip
# ---- END EDIT ----

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$sourceFolder = Join-Path $scriptDir "files"
$outputFile = Join-Path $scriptDir "manifest.json"
$manifestBuilder = Join-Path $scriptDir "..\tools\ManifestBuilder\build-manifest.ps1"

if (-not (Test-Path $sourceFolder)) {
    Write-Error "No 'files' folder found. Place your mod files in: $sourceFolder"
    exit 1
}

if (-not (Test-Path $manifestBuilder)) {
    Write-Error "Manifest builder not found at: $manifestBuilder"
    Write-Host "Make sure you're running this from the server-template folder inside the project."
    exit 1
}

$params = @{
    SourceFolder   = $sourceFolder
    BaseUrl        = $BaseUrl
    ModpackId      = $ModpackId
    ModpackName    = $ModpackName
    ModpackVersion = $ModpackVersion
    OutputFile     = $outputFile
}

if ($ChangelogUrl) { $params.ChangelogUrl = $ChangelogUrl }
if ($ValheimVersion) { $params.ValheimVersion = $ValheimVersion }
if ($BepInExVersion) { $params.BepInExVersion = $BepInExVersion }
if ($BepInExZipPath -and (Test-Path $BepInExZipPath)) {
    $params.BepInExZipPath = $BepInExZipPath
    $params.BepInExZipUrl  = $BepInExZipUrl
}

& $manifestBuilder @params

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host " Manifest ready!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Upload this entire folder to your web server."
Write-Host "Players need this URL:" -ForegroundColor Cyan
Write-Host ""
$manifestUrl = $BaseUrl.Replace("/files", "/manifest.json")
Write-Host "  $manifestUrl" -ForegroundColor Yellow
Write-Host ""
