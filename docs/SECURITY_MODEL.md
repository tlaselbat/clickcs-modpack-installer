# Security Model

## Threat Model

The launcher downloads and installs files from a private web server controlled by a trusted server owner. The primary threats are:

1. **Malicious or compromised manifests** attempting to write outside the Valheim folder
2. **Man-in-the-middle attacks** intercepting downloads
3. **Password exposure** through logs, config files, or crash dumps
4. **Path traversal attacks** via crafted relative paths
5. **Command injection** through server host/port fields

## Defenses

### Path Safety
- All manifest paths are validated before use
- Rejects: `..`, absolute paths, NTFS alternate data streams (`:`), null bytes
- Rejects: Windows reserved device names (CON, NUL, COM1, etc.)
- Rejects: trailing dots/spaces (Windows strips silently)
- Final canonicalization check ensures resolved path stays inside Valheim folder
- Path validation applies to: downloads, backups, restores, and removals

### Download Integrity
- All files are downloaded to a **staging directory** first
- **Every staged file** has its SHA256 hash verified before any live files are touched
- If any hash fails, the entire update is aborted with no live files modified
- SHA256 hash format is validated (must be exactly 64 hex characters)
- Download URLs must be valid HTTP/HTTPS

### Transport Security
- HTTPS is required by default
- HTTP requires explicit opt-in via Settings (for LAN/development only)
- Download and manifest URLs are validated before use

### Password Security
- Passwords are stored using **Windows Credential Manager** (preferred)
- Falls back to **DPAPI** (Data Protection API) with current-user scope
- Passwords are **never** written to:
  - Log files
  - Configuration files (settings.json, profiles.json)
  - Manifest files
  - Crash dumps (stored as SecureString where possible)
- Clipboard is cleared after a configurable timeout (default 60s)
- Delete Saved Password button removes from both Credential Manager and DPAPI

### Manifest Validation
- Schema version must be exactly `1`
- ModpackId max 128 chars, ModpackName max 256 chars
- No duplicate relative paths (case-insensitive, separator-normalized)
- File sizes must be 0 to 2 GB
- Changelog URL validated if present
- Removal paths validated for traversal

### Launch Command Safety
- Server hostname is sanitized with an allowlist regex: `[a-zA-Z0-9.\-:]`
- Port must be 1–65535
- No shell metacharacters allowed in launch arguments
- Steam launch uses `ProcessStartInfo` (no shell interpretation)

### Self-Update Safety
- Update info JSON is validated (version, URL, hash format)
- Downloaded update is SHA256-verified before replacement
- Downgrades are prevented (version must be strictly greater)
- Old launcher is backed up before replacement
- Failed replacement triggers automatic backup restore

### Update Transactionality
- Backup is created before any files are modified
- All downloads go to a staging directory
- All hashes are verified before touching live files
- Local state (`installed_manifest`, `managed_files`) is only written on **full success**
- Failed or cancelled updates preserve the previous working state
- Staging directory is cleaned up in all cases (success, failure, cancellation)

### File Operations
- Never deletes unknown files (only managed files or explicitly listed removals)
- Removals default to `remove_only_if_managed: true`
- Backup is created before every update and rollback

## What This Does NOT Protect Against

- A compromised server owner pushing malicious DLLs (trust model)
- Physical access to the machine (DPAPI is user-scoped, not hardware-bound)
- Memory inspection of the running process
- Code-signing (not implemented — planned for future)
