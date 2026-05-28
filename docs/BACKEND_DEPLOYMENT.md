# Backend Deployment Guide — Hosting a Modpack

This guide walks you through setting up a web server to deploy modpacks to players using the ClickCS Valheim Launcher. By the end, you'll have a URL you can give to players.

---

## What You Need

| Requirement | Why |
|---|---|
| **A web server** | Serves files over HTTPS. Can be a VPS, home server, NAS, or even GitHub Pages / Cloudflare R2 |
| **HTTPS certificate** | The launcher requires HTTPS by default. Use Let's Encrypt (free) or Cloudflare |
| **PowerShell 5.1+** | Runs the manifest builder (included on all Windows 10/11 machines) |
| **Your modpack files** | BepInEx + plugins + configs, organized exactly as they go into the Valheim folder |

> **No database, no app server, no API.** The entire backend is just **static files on a web server**.

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────┐
│  YOUR WEB SERVER (static files)                         │
│                                                         │
│  https://mods.yourserver.com/valheim/                   │
│  ├── manifest.json    ← launcher reads this             │
│  └── files/           ← launcher downloads from here    │
│      ├── BepInEx/plugins/MyMod.dll                      │
│      ├── BepInEx/config/MyMod.cfg                       │
│      └── ...                                            │
└────────────────────────────────┬────────────────────────┘
                                 │ HTTPS
                                 ▼
┌─────────────────────────────────────────────────────────┐
│  PLAYER'S LAUNCHER                                      │
│  1. Downloads manifest.json                             │
│  2. Compares hashes with local files                    │
│  3. Downloads only changed/new files                    │
│  4. Verifies SHA256 hash of every download              │
│  5. Installs into Valheim folder                        │
└─────────────────────────────────────────────────────────┘
```

---

## Step 1 — Prepare Your Modpack Files

Create a folder on your PC that mirrors what should go into the player's Valheim directory:

```
C:\ModpackStaging\
├── BepInEx\
│   ├── core\
│   │   ├── BepInEx.dll
│   │   ├── BepInEx.Harmony.dll
│   │   ├── BepInEx.Preloader.dll
│   │   └── ...
│   ├── plugins\
│   │   ├── Jotunn.dll
│   │   ├── YourCustomMod.dll
│   │   └── ...
│   └── config\
│       ├── BepInEx.cfg
│       ├── YourCustomMod.cfg
│       └── ...
├── doorstop_config.ini
└── winhttp.dll
```

> **Important:** Only include files you want the launcher to manage. Don't include save files, worlds, or player-specific data.

### Tips:
- Start with a clean Valheim install + BepInEx + your mods
- Test that this set of files works on your own machine first
- Use the **exact folder structure** that belongs inside the Valheim root folder

---

## Step 2 — Generate the Manifest

Run the included PowerShell script:

```powershell
cd "C:\path\to\ClickCS Valheim Mod Manager\tools\ManifestBuilder"

.\build-manifest.ps1 `
    -SourceFolder "C:\ModpackStaging" `
    -BaseUrl "https://mods.yourserver.com/valheim/files" `
    -ModpackId "my-server" `
    -ModpackName "My Valheim Server" `
    -ModpackVersion "1.0.0" `
    -OutputFile "C:\ModpackStaging\manifest.json"
```

### Parameters explained:

| Parameter | What to put |
|---|---|
| `-SourceFolder` | Path to your modpack files folder from Step 1 |
| `-BaseUrl` | The public URL where files will be accessible (must match your web server path) |
| `-ModpackId` | A short unique ID (no spaces), e.g. `clickcs-main` |
| `-ModpackName` | Human-readable name players will see |
| `-ModpackVersion` | Semver string — bump this every time you update |
| `-OutputFile` | Where to save the generated manifest.json |

### Optional parameters:

| Parameter | Purpose |
|---|---|
| `-ValheimVersion` | Compatible Valheim version (informational) |
| `-BepInExVersion` | Required BepInEx version (informational) |
| `-ChangelogUrl` | URL to a changelog file (shown in launcher) |

### What it produces:

The script outputs `manifest.json` containing every file's:
- Relative path
- SHA256 hash
- File size
- Download URL
- Auto-detected file type (plugin, config, core, other)

---

## Step 3 — Upload to Your Web Server

Upload the entire structure so the URLs match your `-BaseUrl`:

```
Your web root (e.g., /var/www/mods/valheim/):
├── manifest.json
└── files/
    ├── BepInEx/
    │   ├── core/
    │   │   └── BepInEx.dll
    │   ├── plugins/
    │   │   └── Jotunn.dll
    │   └── config/
    │       └── BepInEx.cfg
    ├── doorstop_config.ini
    └── winhttp.dll
```

After upload, verify:
- `https://mods.yourserver.com/valheim/manifest.json` returns the JSON
- `https://mods.yourserver.com/valheim/files/BepInEx/plugins/Jotunn.dll` downloads the file

---

## Step 4 — Web Server Configuration

### Option A: Nginx (Linux VPS)

```nginx
server {
    listen 443 ssl http2;
    server_name mods.yourserver.com;

    ssl_certificate     /etc/letsencrypt/live/mods.yourserver.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/mods.yourserver.com/privkey.pem;

    root /var/www/mods;

    location /valheim/ {
        autoindex off;
        
        # Prevent caching of manifest so players always get latest version
        location ~* manifest\.json$ {
            add_header Cache-Control "no-cache, no-store, must-revalidate";
        }
        
        # Allow caching of mod files (they're hash-verified anyway)
        location ~* \.(dll|cfg|ini|txt)$ {
            add_header Cache-Control "public, max-age=86400";
        }
    }
}
```

Get a free certificate:
```bash
sudo apt install certbot python3-certbot-nginx
sudo certbot --nginx -d mods.yourserver.com
```

### Option B: IIS (Windows Server)

Place a `web.config` in your web root:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<configuration>
  <system.webServer>
    <staticContent>
      <remove fileExtension=".json" />
      <remove fileExtension=".dll" />
      <remove fileExtension=".cfg" />
      <mimeMap fileExtension=".json" mimeType="application/json" />
      <mimeMap fileExtension=".dll" mimeType="application/octet-stream" />
      <mimeMap fileExtension=".cfg" mimeType="text/plain" />
      <mimeMap fileExtension=".ini" mimeType="text/plain" />
    </staticContent>
    <httpProtocol>
      <customHeaders>
        <add name="Cache-Control" value="no-cache" />
      </customHeaders>
    </httpProtocol>
    <directoryBrowse enabled="false" />
  </system.webServer>
</configuration>
```

### Option C: Caddy (simplest setup)

```
mods.yourserver.com {
    root * /var/www/mods
    file_server
    header /valheim/manifest.json Cache-Control "no-cache"
}
```

Caddy handles HTTPS automatically with Let's Encrypt.

### Option D: Cloudflare R2 / S3 (serverless)

1. Create an R2 bucket (or S3 bucket)
2. Upload all files maintaining the folder structure
3. Enable public access or use a custom domain
4. Set Cache-Control on manifest.json to `no-cache`

### Option E: GitHub Pages (free, simple)

1. Create a private/public repo
2. Put files in a folder structure
3. Enable GitHub Pages
4. Your URL will be `https://yourusername.github.io/repo-name/valheim/manifest.json`

> **Note:** GitHub Pages has a 1GB storage limit and 100MB per-file limit.

---

## Step 5 — Tell Your Players

Give players these details:

| What to tell them | Example |
|---|---|
| **Manifest URL** | `https://mods.yourserver.com/valheim/manifest.json` |
| **Server Host** | `play.yourserver.com` or `192.168.1.50` |
| **Server Port** | `2456` |
| **Server Password** | *(tell them privately, they save it locally)* |

They enter this in the launcher's Profile Manager and click Update. Done.

---

## Updating the Modpack

When you want to push new mods or config changes:

1. **Update your staging folder** — add/remove/modify files in `C:\ModpackStaging\`
2. **Bump the version** and re-run the manifest builder:
   ```powershell
   .\build-manifest.ps1 `
       -SourceFolder "C:\ModpackStaging" `
       -BaseUrl "https://mods.yourserver.com/valheim/files" `
       -ModpackId "my-server" `
       -ModpackName "My Valheim Server" `
       -ModpackVersion "1.1.0" `
       -OutputFile "C:\ModpackStaging\manifest.json"
   ```
3. **Upload changed files + new manifest** to your web server
4. Players click **Update** in the launcher — only changed files download

### Removing mods

To remove a mod that was previously installed, add a `removals` entry to the manifest. The manifest builder doesn't do this automatically — edit the JSON manually:

```json
"removals": [
    {
        "relative_path": "BepInEx/plugins/OldMod.dll",
        "reason": "Replaced by NewMod in v1.1.0",
        "remove_only_if_managed": true
    }
]
```

The launcher will only delete the file if it was previously installed by the launcher (won't touch player-added files unless `remove_only_if_managed` is `false`).

---

## Optional: Launcher Self-Update

If you want to push launcher updates to players too:

1. Create `launcher/latest.json` on your web server:
   ```json
   {
       "version": "1.1.0",
       "download_url": "https://mods.yourserver.com/valheim/launcher/ClickCS-Valheim-Launcher-1.1.0.exe",
       "sha256": "abc123...full 64-char hash here",
       "changelog_url": "https://mods.yourserver.com/valheim/launcher/changelog.txt",
       "mandatory": false
   }
   ```

2. Upload the new launcher exe to that URL

3. Tell players to set **Launcher Update URL** in Settings to:
   ```
   https://mods.yourserver.com/valheim/launcher/latest.json
   ```

Get the SHA256 hash of the exe:
```powershell
(Get-FileHash ".\ClickCS Valheim Launcher.exe" -Algorithm SHA256).Hash.ToLower()
```

---

## Full Example: Start to Finish

```powershell
# 1. Prepare your mod files
$staging = "C:\ValheimModpack"

# 2. Generate the manifest
.\tools\ManifestBuilder\build-manifest.ps1 `
    -SourceFolder $staging `
    -BaseUrl "https://mods.myserver.com/valheim/files" `
    -ModpackId "myserver-main" `
    -ModpackName "MyServer Valheim Pack" `
    -ModpackVersion "1.0.0" `
    -ChangelogUrl "https://mods.myserver.com/valheim/changelog.txt" `
    -OutputFile "$staging\manifest.json"

# 3. Upload (example: SCP to a Linux VPS)
scp -r "$staging\*" user@myserver.com:/var/www/mods/valheim/

# Or if using rsync:
# rsync -avz "$staging/" user@myserver.com:/var/www/mods/valheim/

# 4. Verify
Start-Process "https://mods.myserver.com/valheim/manifest.json"
```

---

## Troubleshooting

| Problem | Cause | Fix |
|---|---|---|
| Player gets "Network error" | manifest.json URL unreachable | Verify URL in browser, check firewall/DNS |
| Player gets "Hash mismatch" | File on server doesn't match manifest | Re-run manifest builder after uploading files |
| `.dll` files return 404 | Web server blocks .dll extension | Add MIME type (see web.config/nginx config above) |
| Manifest returns HTML instead of JSON | URL points to wrong location | Check your web server root path configuration |
| "HTTPS required" error | Player has HTTP URL | Use HTTPS, or player enables "Allow insecure HTTP" in Settings (not recommended) |
| Large files fail to download | Server timeout or size limit | Increase `client_max_body_size` (nginx) or `maxAllowedContentLength` (IIS) |

---

## Security Checklist

- [ ] HTTPS enabled with valid certificate
- [ ] Directory listing disabled (`autoindex off`)
- [ ] No server passwords in the manifest or any hosted files
- [ ] Manifest is regenerated (hashes updated) after every file change
- [ ] Files are uploaded **before** the manifest (so hashes match immediately)
- [ ] Consider restricting access with basic auth or IP allowlist if private

---

## Recommended Folder Layout (Multi-Server)

If you run multiple servers or profiles:

```
https://mods.yourserver.com/
├── launcher/
│   ├── latest.json
│   └── ClickCS-Valheim-Launcher-1.0.0.exe
│
├── main-server/
│   ├── manifest.json
│   ├── changelog.txt
│   └── files/
│       └── ...
│
└── test-server/
    ├── manifest.json
    ├── changelog.txt
    └── files/
        └── ...
```

Each server profile gets its own manifest URL:
- Main: `https://mods.yourserver.com/main-server/manifest.json`
- Test: `https://mods.yourserver.com/test-server/manifest.json`
