# Release Checklist

## Pre-Release Gate Tests

All must pass before shipping:

### Security Tests
- [ ] `../evil.dll` path — rejected by PathValidator
- [ ] `C:/Windows/System32/test.dll` — rejected
- [ ] `BepInEx/plugins/../../evil.dll` — rejected
- [ ] NTFS ADS paths (`file.dll:hidden`) — rejected
- [ ] Windows reserved names (CON, NUL, COM1) — rejected
- [ ] Duplicate manifest paths — rejected
- [ ] Invalid SHA256 format — rejected
- [ ] Invalid download URL — rejected
- [ ] Oversized file entry (>2GB) — rejected
- [ ] Malicious hostname in launch args — sanitized
- [ ] Password does not appear in logs
- [ ] Password does not appear in settings.json or profiles.json
- [ ] HTTP manifest URL rejected (without opt-in)

### Update Tests
- [ ] Clean install from empty Valheim folder
- [ ] Update with changed files
- [ ] Update with new files added
- [ ] Update with files removed
- [ ] Wrong hash aborts entire update (no live files changed)
- [ ] Cancelled update preserves existing install
- [ ] `installed_manifest` not updated on partial failure
- [ ] Staging directory cleaned up after update

### Repair Test
- [ ] Repair re-downloads all mismatched files
- [ ] Repair does not affect unmanaged files

### Rollback Tests
- [ ] Pre-rollback backup is created
- [ ] Rollback restores correct files
- [ ] Empty backup shows warning
- [ ] Unsafe paths in backup are skipped during restore

### Self-Update Tests
- [ ] Valid update detected and version compared
- [ ] Downgrade prevented
- [ ] Invalid SHA256 in latest.json — rejected
- [ ] Hash mismatch on downloaded exe — rejected
- [ ] Update helper backs up old exe
- [ ] Failed replacement restores backup

### Launch Tests
- [ ] Steam executable launch works
- [ ] Steam URI fallback works
- [ ] Auto-connect enabled — adds +connect args
- [ ] Auto-connect disabled — no +connect args
- [ ] Password copied to clipboard
- [ ] Clipboard cleared after timeout

### UI Tests
- [ ] GUI stays responsive during downloads
- [ ] Progress bar shows meaningful progress
- [ ] Cancel button works
- [ ] Copy Log button works
- [ ] Copy Error Details button works
- [ ] Settings save and reload correctly
- [ ] Profile CRUD works

### File Safety
- [ ] No unknown files deleted during update
- [ ] No files written outside Valheim folder
- [ ] Backups created before every destructive operation

## Build Steps

```bash
# Build
dotnet build -c Release

# Run tests
dotnet test -c Release

# Publish self-contained single-file
dotnet publish src/ClickCSValheimLauncher/ClickCSValheimLauncher.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o ./publish
dotnet publish src/ClickCSValheimLauncher.UpdateHelper/ClickCSValheimLauncher.UpdateHelper.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o ./publish

# Create portable zip
# Zip contents of ./publish/
```

## Release Artifacts

- [ ] `ClickCS Valheim Launcher.exe` (main app)
- [ ] `ClickCS Valheim Launcher Updater.exe` (self-update helper)
- [ ] `README.md`
- [ ] `CHANGELOG.md`
- [ ] Docs folder (optional, for server owners)

## Post-Release

- [ ] Verify download links work
- [ ] Test clean install on fresh Windows 10/11 VM
- [ ] Update `latest.json` on server if using self-update
- [ ] Notify players
