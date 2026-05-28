# Troubleshooting

## Common Issues

### "Valheim not detected"
- Open **Settings** and browse to your Valheim folder manually
- Typical path: `C:\Program Files (x86)\Steam\steamapps\common\Valheim`
- The launcher looks for `valheim.exe` in the selected folder

### "Network error: Could not reach the manifest server"
- Check your internet connection
- Verify the manifest URL in your profile settings
- Try opening the URL in a web browser
- If using HTTP, enable "Allow insecure HTTP" in Settings

### "Hash mismatch" during update
- The file on the server doesn't match the hash in the manifest
- The server owner may need to regenerate the manifest
- Try again — it could be a transient download error
- **Your existing modpack is preserved** — the launcher verifies all files before installing

### "File locked" or "Access denied"
- Make sure Valheim is completely closed
- Close any file managers or antivirus scanning the Valheim folder
- Try running the launcher as Administrator

### Update seems stuck
- Large modpacks take time; check the progress bar
- Click **Cancel** if needed — cancellation is safe and preserves your current install
- Check logs for details

### Mods not loading in game
- Click **Repair** to re-verify and re-download all files
- Ensure BepInEx framework files are included in the modpack
- Check that `doorstop_config.ini` and `winhttp.dll` are in the Valheim root

### Auto-connect not working
- Valheim's `+connect` parameter may not work with all server configurations
- Make sure the server host and port are correct in your profile
- Try connecting manually from the server browser as a fallback

### Password not pasting
- The password is copied to your clipboard when you click Launch
- Use Ctrl+V in the Valheim password dialog
- The clipboard is cleared after the configured timeout (default 60s)

## Log Files

Logs are stored at: `%APPDATA%\ClickCS Valheim Launcher\launcher.log`

- Rotated daily, last 7 days retained
- Contains timestamps, log levels, and detailed error information
- **Passwords are never written to logs**

To access logs:
1. Click **Open Logs** in the launcher
2. Or navigate to `%APPDATA%\ClickCS Valheim Launcher\` in Explorer

## Resetting the Launcher

To completely reset:
1. Delete `%APPDATA%\ClickCS Valheim Launcher\` (removes settings, profiles, state)
2. Backup files are in `<Valheim folder>\ClickCS_Backups\` and are not affected

## Reporting Issues

When reporting issues, include:
1. Click **Copy Error Details** for the last error
2. Click **Copy Log** for the session log
3. Include your launcher version (shown in the title bar)
