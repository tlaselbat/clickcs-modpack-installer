# ClickCS Valheim Launcher

A Windows-only GUI launcher/updater for private Valheim modpacks. No Thunderstore, r2modman, or Overwolf required.

## Features

- **Multiple Server Profiles** — Configure different servers with their own modpacks
- **Private Web Server Hosting** — Download modpacks from your own server
- **SHA256 Verification** — Every file is hash-verified before installation
- **Automatic Backups** — Files are backed up before updates
- **Rollback Support** — Restore any previous backup
- **Secure Password Storage** — Uses Windows Credential Manager / DPAPI
- **Steam Launch Integration** — Launch Valheim through Steam with optional `+connect`
- **Auto-Connect** — Toggle per-profile auto-connect to your server
- **Self-Updating** — Launcher can update itself from your private server
- **Safe by Design** — Never deletes unknown files, validates all paths, no path traversal

## Requirements

- Windows 10/11
- .NET 8.0 Runtime (or build as self-contained)
- Steam with Valheim installed

## Building

```bash
dotnet restore
dotnet build
```

### Publish as single-file executable:

```bash
dotnet publish src/ClickCSValheimLauncher/ClickCSValheimLauncher.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o ./publish
dotnet publish src/ClickCSValheimLauncher.UpdateHelper/ClickCSValheimLauncher.UpdateHelper.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o ./publish
```

## Project Structure

```
ClickCSValheimLauncher.sln
├── src/
│   ├── ClickCSValheimLauncher/          # Main WPF application
│   │   ├── Models/                      # Data models
│   │   ├── Services/                    # Business logic services
│   │   ├── ViewModels/                  # MVVM ViewModels
│   │   ├── Views/                       # WPF XAML windows
│   │   ├── Helpers/                     # Utility classes
│   │   ├── Converters/                  # XAML value converters
│   │   └── Themes/                      # Dark theme styling
│   └── ClickCSValheimLauncher.UpdateHelper/  # Self-update helper process
└── tools/
    └── ManifestBuilder/                 # Server-side manifest generation
```

## Configuration

Settings and profiles are stored in `%APPDATA%/ClickCS Valheim Launcher/`:
- `settings.json` — Paths, update URL, preferences
- `profiles.json` — Server profile configurations
- `managed_files_<profile>.json` — Tracked installed files
- `installed_manifest_<profile>.json` — Last installed manifest
- `launcher.log` — Rolling log file

Backups are stored in: `<ValheimFolder>/ClickCS_Backups/<profile>/<timestamp>/`

## Server Setup

See `tools/ManifestBuilder/example-server-layout.md` for complete server hosting documentation.

Quick start:
1. Host your modpack files on any HTTPS web server
2. Use `tools/ManifestBuilder/build-manifest.ps1` to generate `manifest.json`
3. Configure the manifest URL in a launcher profile

## Security

- Passwords are stored using Windows Credential Manager (preferred) or DPAPI
- Passwords are **never** written to logs or config files
- All file paths are validated against traversal attacks
- Downloads go to temp files and are hash-verified before installation
- HTTPS is required by default (HTTP requires explicit opt-in)

## License

Private use — ClickCS
