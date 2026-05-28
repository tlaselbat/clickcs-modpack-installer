# ClickCS Valheim Launcher - Build & Package Script
# Usage: .\build.ps1 [-Configuration Release] [-Clean]
param(
    [string]$Configuration = "Release",
    [switch]$Clean
)

$ErrorActionPreference = "Stop"
$rootDir = $PSScriptRoot
$publishDir = Join-Path $rootDir "publish"
$mainProject = Join-Path $rootDir "src\ClickCSValheimLauncher\ClickCSValheimLauncher.csproj"
$helperProject = Join-Path $rootDir "src\ClickCSValheimLauncher.UpdateHelper\ClickCSValheimLauncher.UpdateHelper.csproj"

Write-Host "======================================" -ForegroundColor Cyan
Write-Host " ClickCS Valheim Launcher - Build" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

# Clean
if ($Clean -and (Test-Path $publishDir)) {
    Write-Host "Cleaning publish directory..." -ForegroundColor Yellow
    Remove-Item -Recurse -Force $publishDir
}

# Restore
Write-Host "Restoring packages..." -ForegroundColor Green
dotnet restore $rootDir
if ($LASTEXITCODE -ne 0) { throw "Restore failed" }

# Build
Write-Host "Building solution ($Configuration)..." -ForegroundColor Green
dotnet build $rootDir -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

# Test
Write-Host "Running tests..." -ForegroundColor Green
dotnet test $rootDir -c $Configuration --no-build --verbosity normal
if ($LASTEXITCODE -ne 0) { throw "Tests failed" }

# Publish main app
Write-Host "Publishing launcher..." -ForegroundColor Green
dotnet publish $mainProject -c $Configuration -r win-x64 --self-contained `
    -p:PublishSingleFile=true `
    -p:PublishReadyToRun=true `
    -p:EnableCompressionInSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "Publish (launcher) failed" }

# Publish update helper
Write-Host "Publishing update helper..." -ForegroundColor Green
dotnet publish $helperProject -c $Configuration -r win-x64 --self-contained `
    -p:PublishSingleFile=true `
    -p:PublishReadyToRun=true `
    -p:EnableCompressionInSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "Publish (helper) failed" }

# Summary
Write-Host ""
Write-Host "======================================" -ForegroundColor Cyan
Write-Host " Build Complete!" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Output: $publishDir" -ForegroundColor White
Write-Host ""
Get-ChildItem $publishDir -Filter "*.exe" | ForEach-Object {
    $size = [math]::Round($_.Length / 1MB, 1)
    Write-Host "  $($_.Name) ($size MB)" -ForegroundColor Green
}
Write-Host ""
Write-Host "To create a portable zip:" -ForegroundColor Yellow
Write-Host "  Compress-Archive -Path '$publishDir\*' -DestinationPath '.\ClickCS-Valheim-Launcher-Portable.zip'"
