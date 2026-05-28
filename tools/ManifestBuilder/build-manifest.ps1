<#
.SYNOPSIS
    ClickCS Valheim Launcher - Manifest Builder
    Generates a modpack manifest JSON from a folder of files.

.DESCRIPTION
    Point this script at a folder of modpack files and it will generate
    a manifest.json with SHA256 hashes and relative paths.

.PARAMETER SourceFolder
    Path to the folder containing modpack files (BepInEx structure)

.PARAMETER BaseUrl
    Base URL where files will be hosted (e.g., https://mods.example.com/clickcs/modpacks/myserver/files)

.PARAMETER ModpackId
    Unique identifier for the modpack

.PARAMETER ModpackName
    Display name for the modpack

.PARAMETER ModpackVersion
    Semantic version string (e.g., 1.2.0)

.PARAMETER ChangelogUrl
    Base URL where files will be hosted (e.g., https://mods.example.com/clickcs/modpacks/myserver/files)

.PARAMETER BepInExZipPath
    Optional path to a BepInEx zip file. When supplied, its SHA256 and size are computed and the bepinex block
    is populated in the manifest. Upload the zip to your web server at BepInExZipUrl before distributing.

.PARAMETER BepInExZipUrl
    Public download URL for the BepInEx zip (required if BepInExZipPath is supplied).

.PARAMETER BepInExVersion
    Version of BepInEx (required if BepInExZipPath is supplied).

.PARAMETER OutputFile
    Path for the output manifest.json file

.PARAMETER ValheimVersion
    Version of Valheim (optional)

.EXAMPLE
    .\build-manifest.ps1 -SourceFolder ".\modpack_files" -BaseUrl "https://mods.example.com/files" -ModpackId "clickcs-main" -ModpackName "ClickCS Server Pack" -ModpackVersion "1.0.0" -OutputFile ".\manifest.json"

.EXAMPLE
    .\build-manifest.ps1 -SourceFolder ".\modpack_files" -BaseUrl "https://mods.example.com/files" -ModpackId "clickcs-main" -ModpackName "ClickCS Server Pack" -ModpackVersion "1.0.0" -BepInExZipPath ".\BepInEx_x64_5.4.2202.0.zip" -BepInExZipUrl "https://mods.example.com/deps/BepInEx_x64_5.4.2202.0.zip" -BepInExVersion "5.4.2202" -OutputFile ".\manifest.json"
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$SourceFolder,

    [Parameter(Mandatory=$true)]
    [string]$BaseUrl,

    [Parameter(Mandatory=$true)]
    [string]$ModpackId,

    [Parameter(Mandatory=$true)]
    [string]$ModpackName,

    [Parameter(Mandatory=$true)]
    [string]$ModpackVersion,

    [string]$OutputFile = ".\manifest.json",

    [string]$ValheimVersion = $null,

    [string]$BepInExVersion = $null,

    [string]$ChangelogUrl = $null,

    [string]$BepInExZipPath = $null,

    [string]$BepInExZipUrl = $null
)

$ErrorActionPreference = "Stop"

Write-Host "ClickCS Valheim Launcher - Manifest Builder" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

if (-not (Test-Path $SourceFolder)) {
    Write-Error "Source folder not found: $SourceFolder"
    exit 1
}

$BaseUrl = $BaseUrl.TrimEnd('/')
$excludePatterns = @('.gitkeep', 'desktop.ini', 'Thumbs.db', '.DS_Store')
$files = Get-ChildItem -Path $SourceFolder -Recurse -File | Where-Object { $excludePatterns -notcontains $_.Name }

Write-Host "Processing $($files.Count) files from: $SourceFolder"
Write-Host "Base URL: $BaseUrl"
Write-Host ""

$manifestFiles = @()

foreach ($file in $files) {
    $relativePath = $file.FullName.Substring((Resolve-Path $SourceFolder).Path.Length + 1)
    $relativePath = $relativePath.Replace('\', '/')
    
    $hash = (Get-FileHash -Path $file.FullName -Algorithm SHA256).Hash.ToLower()
    $size = $file.Length

    # Determine file type based on path
    $fileType = "other"
    if ($relativePath -match "^BepInEx/plugins/") { $fileType = "plugin" }
    elseif ($relativePath -match "^BepInEx/config/") { $fileType = "config" }
    elseif ($relativePath -match "^BepInEx/patchers/") { $fileType = "patcher" }
    elseif ($relativePath -match "^BepInEx/core/") { $fileType = "core" }

    $downloadUrl = "$BaseUrl/$relativePath"

    $manifestFiles += @{
        relative_path = $relativePath
        sha256 = $hash
        size_bytes = $size
        download_url = $downloadUrl
        file_type = $fileType
        required = $true
    }

    Write-Host "  [$fileType] $relativePath ($([math]::Round($size/1KB, 1)) KB)" -ForegroundColor Gray
}

$bepInExBlock = $null
if ($BepInExZipPath) {
    if (-not (Test-Path $BepInExZipPath)) {
        Write-Error "BepInEx zip not found: $BepInExZipPath"
        exit 1
    }
    if (-not $BepInExZipUrl) {
        Write-Error "-BepInExZipUrl is required when -BepInExZipPath is supplied"
        exit 1
    }
    if (-not $BepInExVersion) {
        Write-Error "-BepInExVersion is required when -BepInExZipPath is supplied"
        exit 1
    }
    Write-Host "Computing BepInEx zip hash: $BepInExZipPath" -ForegroundColor Cyan
    $bepHash = (Get-FileHash -Path $BepInExZipPath -Algorithm SHA256).Hash.ToLower()
    $bepSize = (Get-Item $BepInExZipPath).Length
    $bepInExBlock = [ordered]@{
        required_version = $BepInExVersion
        download_url     = $BepInExZipUrl
        sha256           = $bepHash
        size_bytes       = $bepSize
    }
    Write-Host "  Version : $BepInExVersion" -ForegroundColor Gray
    Write-Host "  SHA256  : $bepHash" -ForegroundColor Gray
    Write-Host "  Size    : $([math]::Round($bepSize/1MB, 2)) MB" -ForegroundColor Gray
    Write-Host ""
}

$manifest = [ordered]@{
    schema_version = 1
    modpack_id = $ModpackId
    modpack_name = $ModpackName
    modpack_version = $ModpackVersion
    valheim_version = $ValheimVersion
    bepinex_version = $BepInExVersion
    changelog_url = $ChangelogUrl
    files = $manifestFiles
    removals = @()
}

if ($bepInExBlock) {
    $manifest['bepinex'] = $bepInExBlock
}

$json = $manifest | ConvertTo-Json -Depth 10
$json | Out-File -FilePath $OutputFile -Encoding utf8

Write-Host ""
Write-Host "Manifest generated: $OutputFile" -ForegroundColor Green
Write-Host "  Files: $($manifestFiles.Count)"
Write-Host "  Total size: $([math]::Round(($manifestFiles | Measure-Object -Property size_bytes -Sum).Sum / 1MB, 2)) MB"
Write-Host ""
Write-Host "Done!" -ForegroundColor Green
