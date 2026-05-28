# ============================================================
# ClickCS Valheim Modpack — Upload Script (Optional)
# ============================================================
# Uploads your server-template folder to a remote server via SCP.
# Requires: OpenSSH client (built into Windows 10/11)
# ============================================================

# ---- EDIT THESE ----
$RemoteUser   = "youruser"                          # SSH username
$RemoteHost   = "clickcs.org"                       # Server hostname or IP
$RemotePath   = "/var/www/html/valheim_modpack"     # Path on server (adjust to your web root)
$SSHKeyFile   = ""                                  # Optional: path to SSH key (leave empty for password prompt)
# ---- END EDIT ----

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot

# Verify manifest exists
if (-not (Test-Path (Join-Path $scriptDir "manifest.json"))) {
    Write-Error "manifest.json not found. Run deploy.ps1 first!"
    exit 1
}

Write-Host "Uploading to ${RemoteUser}@${RemoteHost}:${RemotePath}" -ForegroundColor Cyan

$scpArgs = @()
if ($SSHKeyFile) { $scpArgs += "-i", $SSHKeyFile }

# Upload manifest
Write-Host "  Uploading manifest.json..." -ForegroundColor Gray
scp @scpArgs (Join-Path $scriptDir "manifest.json") "${RemoteUser}@${RemoteHost}:${RemotePath}/manifest.json"

# Upload changelog if exists
$changelog = Join-Path $scriptDir "changelog.txt"
if (Test-Path $changelog) {
    Write-Host "  Uploading changelog.txt..." -ForegroundColor Gray
    scp @scpArgs $changelog "${RemoteUser}@${RemoteHost}:${RemotePath}/changelog.txt"
}

# Upload files (recursive)
Write-Host "  Uploading files/ (this may take a while)..." -ForegroundColor Gray
scp @scpArgs -r (Join-Path $scriptDir "files") "${RemoteUser}@${RemoteHost}:${RemotePath}/"

Write-Host ""
Write-Host "Upload complete!" -ForegroundColor Green
