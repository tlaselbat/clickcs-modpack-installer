# ClickCS Valheim Launcher — Player Guide

## Requirements

| Requirement | Details |
|---|---|
| **OS** | Windows 10 or 11 (64-bit) |
| **Steam** | Installed with Valheim purchased |
| **Valheim** | Installed via Steam (any drive/library) |
| **Internet** | Required to download modpack manifests and mod files |
| **Disk space** | ~500 MB free beyond Valheim install (for backups + staging) |

> The launcher is fully self-contained — no .NET runtime install needed.

---

## Installation

1. Download `ClickCS Valheim Launcher.exe` from the link your server owner gave you
2. Place it anywhere (Desktop, Downloads, a dedicated folder — doesn't matter)
3. Double-click to run

That's it. No installer required.

---

## First Run — What You'll See

```
┌──────────────────────────────────────┐
│ ⚔ ClickCS Valheim  v1.0.0       [⚙] │
│                                      │
│ ┌──────────────────────────────[📝]┐ │
│ │ (profile dropdown)               │ │
│ │ Installed: Not installed          │ │
│ │ Latest: Unknown                   │ │
│ └──────────────────────────────────┘ │
│                                      │
│  [ ⬇ Update ]  [ 🔧 Repair ]  [ 🚀 Launch ] │
│                                      │
│ Ready                                │
│ ████████████████████████████████████ │
│                                      │
│ ▸ Details                            │
└──────────────────────────────────────┘
```

On first launch the launcher will:
- Auto-detect your Steam and Valheim installation paths
- Show "Not installed" for the modpack (you haven't set up a profile yet)

If Valheim is **not detected**, click the **⚙ gear icon** (top-right) → browse to your Valheim folder manually.

---

## Step 1 — Add a Server Profile

Your server owner will give you these details:

| Field | Example | What it is |
|---|---|---|
| **Profile Name** | `ClickCS Server` | Any friendly name you want |
| **Manifest URL** | `https://mods.example.com/manifest.json` | Where the modpack file list lives |
| **Server Host** | `play.example.com` or `192.168.1.100` | The Valheim server address |
| **Server Port** | `2456` | Default Valheim port |
| **Password** | *(optional)* | Server password — stored securely on your PC |

### How to add:

1. Click the **📝 icon** next to the profile dropdown
2. Click **+ Add Profile**
3. Fill in the fields above
4. Click **Test** next to the Manifest URL to verify it works
5. *(Optional)* Click **Test Server** to verify the game server is reachable
6. Click **Save**

---

## Step 2 — Install / Update the Modpack

1. Select your profile from the dropdown
2. Click **⬇ Update**

The launcher will automatically:
1. Download the manifest from your server owner's URL
2. Compare it with your installed files
3. Back up any files that will change
4. Download new/changed files to a temporary staging area
5. Verify every file's SHA256 hash
6. Install only after **all** files pass verification

The progress bar and status text show what's happening. This is safe — if anything fails, your original files are untouched.

---

## Step 3 — Launch Valheim

1. Select your profile
2. Click **🚀 Launch**

The launcher will:
- Start Valheim through Steam
- Auto-connect to the server (if configured in the profile)
- Copy the server password to your clipboard (if saved)
- Auto-clear the clipboard after 60 seconds

> **Tip:** Valheim may still show a password prompt. Just press **Ctrl+V** to paste.

---

## Repairing

If something seems wrong with your mods (crashes, missing files):

1. Click **🔧 Repair**
2. The launcher will re-verify every file and re-download any that are corrupted or missing

---

## Rolling Back

If an update causes problems:

1. Click **Details** (bottom of the window) to expand
2. Click **↩ Rollback**
3. Select a backup from the list (shows date, file count, size)
4. Click **Restore**

A safety backup is created before every rollback, so you can always undo it.

---

## Settings (⚙ Gear Icon)

| Setting | Description | Default |
|---|---|---|
| **Valheim Path** | Where Valheim is installed | Auto-detected |
| **Steam Path** | Where Steam is installed | Auto-detected |
| **Launcher Update URL** | URL for launcher self-updates (set by server owner) | Empty |
| **Check for updates on startup** | Auto-check modpack updates when launcher opens | On |
| **Allow insecure HTTP** | Allow non-HTTPS manifest/download URLs | Off |
| **Clipboard clear seconds** | How long before the password is cleared from clipboard | 60 |

---

## File Locations

| What | Where |
|---|---|
| **Settings** | `%APPDATA%\ClickCS Valheim Launcher\settings.json` |
| **Profiles** | `%APPDATA%\ClickCS Valheim Launcher\profiles.json` |
| **Logs** | `%APPDATA%\ClickCS Valheim Launcher\launcher.log` |
| **Backups** | `<Valheim folder>\ClickCS_Backups\<profile>\<timestamp>\` |
| **Passwords** | Windows Credential Manager (never stored in files) |

> To open the log folder quickly: expand **Details** → click **📋 Logs**.

---

## Troubleshooting

| Problem | Solution |
|---|---|
| "Valheim not detected" | Click ⚙ → Browse to your Valheim folder |
| "Network error" | Check internet connection; verify the Manifest URL with your server owner |
| "Hash mismatch" | Server files may have changed — try Update again |
| "File is locked" | Close Valheim before updating |
| Update seems stuck | Expand Details → click **✕ Cancel**, then try again |
| Modpack broke my game | Click Repair, or use Rollback to restore a previous version |
| Forgot server password | Click 📝 → select profile → click 👁 to reveal |

### Getting Help

1. Expand **Details** → click **Copy Log**
2. Send the log text to your server owner or post in the support channel
3. Log files are also at `%APPDATA%\ClickCS Valheim Launcher\launcher.log`

---

## Security Notes

- Passwords are stored in **Windows Credential Manager**, never in plain text files
- All mod downloads are **SHA256 hash-verified** before installation
- The launcher **never writes outside** your Valheim folder
- Backups are created before every update or rollback
- HTTPS is enforced by default (HTTP can be allowed in Settings if needed)
