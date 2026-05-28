# BepInEx Auto-Install Feature

Add automatic BepInEx detection and installation before mod installation, driven by a new `bepinex` section in the manifest, using a modular dependency installer pipeline.

---

## Manifest Changes

**`ModpackManifest` model** — extend the existing (unused) `bepinex_version` and add a new `bepinex` object:

```json
"bepinex": {
  "required_version": "5.4.2202",
  "download_url": "https://clickcs.org/valheim_modpack/deps/BepInEx_x64_5.4.2202.zip",
  "sha256": "abc123...",
  "size_bytes": 1234567
}
```

- New `BepInExRequirement` model with `RequiredVersion`, `DownloadUrl`, `Sha256`, `SizeBytes`.
- New `BepInEx` property on `ModpackManifest` (nullable — omitting it disables the feature).
- `ManifestService.ValidateManifest` validates the block when present (URL, SHA256, size > 0, non-empty version).

---

## New: `BepInExService`

`src/ClickCSValheimLauncher/Services/BepInExService.cs`:

- **`DetectInstallation(valheimPath)`** — checks for `BepInEx/` dir, `winhttp.dll`, `doorstop_config.ini`. Reads installed version from `BepInEx/core/BepInEx.dll` FileVersionInfo or `BepInEx/changelog.txt`. Returns `BepInExInstallInfo { IsInstalled, InstalledVersion }`.
- **`NeedsInstall(valheimPath, requiredVersion)`** → bool: not installed OR installed version < required (semver compare).
- **`InstallAsync(requirement, valheimPath, ct)`** — full flow:
  1. Check local cache `%APPDATA%/ClickCS Valheim Launcher/dep_cache/<sha256>.zip`; download if missing.
  2. Verify SHA256 of cached zip.
  3. Extract zip to Valheim root, preserving paths, overwriting existing files.
  4. Reports progress via `StatusChanged`/`ProgressChanged` events (same pattern as `UpdateEngineService`).
- Returns `DependencyInstallResult { Success, Message, WasInstalled, InstalledVersion, Errors }`.

---

## `UpdateEngineService` — Phase 0 pre-flight

Before Phase 1 (backup) in `ExecuteUpdateAsync` and `RepairAsync`:

```
// Phase 0: Ensure BepInEx prerequisite is satisfied
if (manifest.BepInEx != null && _bepInExService.NeedsInstall(valheimPath, manifest.BepInEx.RequiredVersion))
{
    var depResult = await _bepInExService.InstallAsync(manifest.BepInEx, valheimPath, ct);
    if (!depResult.Success) → set result.Message/Errors, return early
}
```

`BepInExService` injected into `UpdateEngineService` via DI.

---

## `MainViewModel`

- Wire `BepInExService.StatusChanged`/`ProgressChanged` to UI on construction.
- On `InitializeAsync`: detect BepInEx and `AppendLog("BepInEx: installed vX.X.X" / "not detected")`.
- BepInEx install failure surfaces through the existing `SetError` path.

---

## Server Template

- `server-template/manifest.json` — add sample `bepinex` block with placeholder values.
- `tools/ManifestBuilder/build-manifest.ps1` — add BepInEx zip path parameter; auto-compute SHA256 and populate the block.

---

## Files Changed/Created

| File | Action |
|---|---|
| `Models/ModpackManifest.cs` | Add `BepInExRequirement` class + `BepInEx` property |
| `Services/BepInExService.cs` | **New** — detect, version-check, download+cache, extract |
| `Services/UpdateEngineService.cs` | Inject `BepInExService`, Phase 0 pre-flight |
| `Services/ManifestService.cs` | Validate `bepinex` block |
| `ViewModels/MainViewModel.cs` | Wire events, log BepInEx on init |
| `App.xaml.cs` | Register `BepInExService` in DI |
| `server-template/manifest.json` | Sample `bepinex` block |
| `tools/ManifestBuilder/build-manifest.ps1` | BepInEx zip handling |

---

## Out of Scope

- Other dependency types (VC++, etc.) — pipeline is ready to extend
- UI toggle for "skip BepInEx install"
- BepInEx uninstall / rollback
