# Example Private Web Server Layout

This document shows the recommended folder structure for hosting modpack files
on your private web server.

## Directory Structure

```
/clickcs/
├── launcher/
│   ├── latest.json                              # Launcher self-update manifest
│   └── releases/
│       └── ClickCS-Valheim-Launcher-1.0.0.exe   # Launcher binary
│
└── modpacks/
    ├── main-server/                             # Profile: Main Server
    │   ├── manifest.json                        # Modpack manifest
    │   ├── changelog.md                         # Changelog
    │   └── files/                               # All modpack files
    │       ├── BepInEx/
    │       │   ├── core/
    │       │   │   ├── BepInEx.dll
    │       │   │   └── ...
    │       │   ├── plugins/
    │       │   │   ├── Jotunn.dll
    │       │   │   ├── ValheimPlus.dll
    │       │   │   └── ...
    │       │   └── config/
    │       │       ├── BepInEx.cfg
    │       │       └── ...
    │       ├── doorstop_config.ini
    │       └── winhttp.dll
    │
    └── test-server/                             # Profile: Test Server
        ├── manifest.json
        ├── changelog.md
        └── files/
            └── ...
```

## latest.json Example (Launcher Self-Update)

```json
{
  "version": "1.1.0",
  "download_url": "https://mods.yourserver.com/clickcs/launcher/releases/ClickCS-Valheim-Launcher-1.1.0.exe",
  "sha256": "abc123def456...",
  "changelog_url": "https://mods.yourserver.com/clickcs/launcher/changelog.md",
  "mandatory": false
}
```

## manifest.json Example (Modpack)

```json
{
  "schema_version": 1,
  "modpack_id": "clickcs-main",
  "modpack_name": "ClickCS Main Server Pack",
  "modpack_version": "2.1.0",
  "valheim_version": "0.217.46",
  "bepinex_version": "5.4.2200",
  "changelog_url": "https://mods.yourserver.com/clickcs/modpacks/main-server/changelog.md",
  "files": [
    {
      "relative_path": "BepInEx/core/BepInEx.dll",
      "sha256": "a1b2c3d4...",
      "size_bytes": 123456,
      "download_url": "https://mods.yourserver.com/clickcs/modpacks/main-server/files/BepInEx/core/BepInEx.dll",
      "file_type": "core",
      "required": true
    },
    {
      "relative_path": "BepInEx/plugins/Jotunn.dll",
      "sha256": "e5f6a7b8...",
      "size_bytes": 789012,
      "download_url": "https://mods.yourserver.com/clickcs/modpacks/main-server/files/BepInEx/plugins/Jotunn.dll",
      "file_type": "plugin",
      "required": true
    }
  ],
  "removals": [
    {
      "relative_path": "BepInEx/plugins/OldMod.dll",
      "reason": "Replaced by NewMod in v2.1.0",
      "remove_only_if_managed": true
    }
  ]
}
```

## Web Server Configuration

### Nginx Example

```nginx
server {
    listen 443 ssl;
    server_name mods.yourserver.com;

    ssl_certificate /etc/ssl/certs/your-cert.pem;
    ssl_certificate_key /etc/ssl/private/your-key.pem;

    root /var/www/mods;

    location /clickcs/ {
        autoindex off;
        types {
            application/json json;
            application/octet-stream dll;
            text/markdown md;
        }
    }
}
```

### IIS Example (web.config)

```xml
<?xml version="1.0" encoding="UTF-8"?>
<configuration>
  <system.webServer>
    <staticContent>
      <mimeMap fileExtension=".json" mimeType="application/json" />
      <mimeMap fileExtension=".dll" mimeType="application/octet-stream" />
      <mimeMap fileExtension=".md" mimeType="text/markdown" />
    </staticContent>
    <directoryBrowse enabled="false" />
  </system.webServer>
</configuration>
```

## Updating Your Modpack

1. Place your updated files in the `files/` directory
2. Run the manifest builder script:
   ```powershell
   .\build-manifest.ps1 `
     -SourceFolder "C:\modpack_staging\main-server\files" `
     -BaseUrl "https://mods.yourserver.com/clickcs/modpacks/main-server/files" `
     -ModpackId "clickcs-main" `
     -ModpackName "ClickCS Main Server Pack" `
     -ModpackVersion "2.2.0" `
     -OutputFile "C:\webroot\clickcs\modpacks\main-server\manifest.json"
   ```
3. Upload the new files and manifest to your server
4. Players will see the update next time they check in the launcher
