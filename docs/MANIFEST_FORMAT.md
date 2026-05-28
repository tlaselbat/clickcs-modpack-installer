# Manifest Format Reference

## Schema Version: 1

```json
{
    "schema_version": 1,
    "modpack_id": "my-server-pack",
    "modpack_name": "My Server Modpack",
    "modpack_version": "1.2.0",
    "valheim_version": "0.217.46",
    "bepinex_version": "5.4.2200",
    "changelog_url": "https://example.com/changelog.txt",
    "files": [
        {
            "relative_path": "BepInEx/plugins/MyMod.dll",
            "sha256": "abc123...64 hex chars",
            "size_bytes": 102400,
            "download_url": "https://example.com/files/BepInEx/plugins/MyMod.dll",
            "file_type": "plugin",
            "required": true
        }
    ],
    "removals": [
        {
            "relative_path": "BepInEx/plugins/OldMod.dll",
            "reason": "Replaced by NewMod",
            "remove_only_if_managed": true
        }
    ]
}
```

## Field Reference

### Root Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `schema_version` | int | Yes | Must be `1` |
| `modpack_id` | string | Yes | Unique identifier, max 128 chars |
| `modpack_name` | string | Yes | Display name, max 256 chars |
| `modpack_version` | string | Yes | Semantic version (e.g., `1.2.0`) |
| `valheim_version` | string | No | Compatible Valheim version |
| `bepinex_version` | string | No | Required BepInEx version |
| `changelog_url` | string | No | Valid HTTP/HTTPS URL to changelog |
| `files` | array | Yes | List of files to install |
| `removals` | array | No | List of files to remove |

### File Entry Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `relative_path` | string | Yes | Path relative to Valheim folder |
| `sha256` | string | Yes | 64-char lowercase hex SHA256 hash |
| `size_bytes` | long | Yes | File size (0 to 2GB max) |
| `download_url` | string | Yes | Valid HTTP/HTTPS download URL |
| `file_type` | string | No | Category: `plugin`, `config`, `framework`, `other` |
| `required` | bool | No | Default `true` |

### Removal Entry Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `relative_path` | string | Yes | Path relative to Valheim folder |
| `reason` | string | No | Human-readable reason for removal |
| `remove_only_if_managed` | bool | No | Default `true`; only remove if installed by launcher |

## Validation Rules

The launcher enforces these rules on every manifest:

- **Path safety**: No `..`, no absolute paths, no NTFS alternate data streams (`:`)
- **No duplicates**: Each `relative_path` must be unique (case-insensitive, separator-normalized)
- **Valid hashes**: Must be exactly 64 hex characters
- **Valid URLs**: Must be `http://` or `https://` scheme
- **Size limits**: 0 to 2 GB per file
- **No dangerous names**: Windows reserved names (CON, NUL, COM1, etc.) rejected
- **No trailing dots/spaces**: Windows silently strips these, causing confusion
