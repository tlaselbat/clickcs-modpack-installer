using System.IO;
using System.Net.Http;
using ClickCSValheimLauncher.Helpers;
using ClickCSValheimLauncher.Models;
using Microsoft.Extensions.Logging;

namespace ClickCSValheimLauncher.Services;

public class UpdateResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int FilesUpdated { get; set; }
    public int FilesRemoved { get; set; }
    public int FilesSkipped { get; set; }
    public string? BackupPath { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class FileComparisonResult
{
    public List<ManifestFile> FilesToDownload { get; set; } = new();
    public List<ManifestFile> FilesUpToDate { get; set; } = new();
    public List<ManifestRemoval> FilesToRemove { get; set; } = new();
    public List<string> UnknownFiles { get; set; } = new();
    public long TotalDownloadSize { get; set; }
}

public class UpdateEngineService
{
    private readonly ILogger<UpdateEngineService> _logger;
    private readonly HttpClient _httpClient;
    private readonly ManifestService _manifestService;
    private readonly FileBackupHelper _backupHelper;
    private readonly SettingsService _settingsService;
    private readonly BepInExService _bepInExService;

    public event Action<string>? StatusChanged;
    public event Action<double>? ProgressChanged;

    public UpdateEngineService(
        ILogger<UpdateEngineService> logger,
        HttpClient httpClient,
        ManifestService manifestService,
        FileBackupHelper backupHelper,
        SettingsService settingsService,
        BepInExService bepInExService)
    {
        _logger = logger;
        _httpClient = httpClient;
        _manifestService = manifestService;
        _backupHelper = backupHelper;
        _settingsService = settingsService;
        _bepInExService = bepInExService;

        _bepInExService.StatusChanged += msg => StatusChanged?.Invoke(msg);
        _bepInExService.ProgressChanged += pct => ProgressChanged?.Invoke(pct);
    }

    public async Task<FileComparisonResult> CompareFilesAsync(
        ModpackManifest manifest,
        string valheimPath,
        string profileId,
        CancellationToken ct = default)
    {
        var result = new FileComparisonResult();
        var managedState = _manifestService.LoadManagedFiles(profileId);
        var managedPaths = managedState?.Files.Select(f => f.RelativePath).ToHashSet(StringComparer.OrdinalIgnoreCase)
                          ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        int processed = 0;
        int total = manifest.Files.Count;

        foreach (var file in manifest.Files)
        {
            ct.ThrowIfCancellationRequested();
            processed++;
            ProgressChanged?.Invoke((double)processed / total * 50);

            var safePath = PathValidator.GetSafePath(file.RelativePath, valheimPath);
            if (safePath == null)
            {
                result.UnknownFiles.Add(file.RelativePath);
                _logger.LogWarning("Unsafe path in manifest, skipping: {Path}", file.RelativePath);
                continue;
            }

            if (!File.Exists(safePath))
            {
                result.FilesToDownload.Add(file);
                result.TotalDownloadSize += file.SizeBytes;
                continue;
            }

            var localHash = await HashHelper.ComputeSha256Async(safePath, ct);
            if (!HashHelper.VerifyHash(file.Sha256, localHash))
            {
                result.FilesToDownload.Add(file);
                result.TotalDownloadSize += file.SizeBytes;
            }
            else
            {
                result.FilesUpToDate.Add(file);
            }
        }

        foreach (var removal in manifest.Removals)
        {
            if (removal.RemoveOnlyIfManaged && !managedPaths.Contains(removal.RelativePath))
                continue;

            var safePath = PathValidator.GetSafePath(removal.RelativePath, valheimPath);
            if (safePath != null && File.Exists(safePath))
            {
                result.FilesToRemove.Add(removal);
            }
        }

        StatusChanged?.Invoke($"Comparison complete: {result.FilesToDownload.Count} to download, " +
                             $"{result.FilesUpToDate.Count} up to date, {result.FilesToRemove.Count} to remove");

        return result;
    }

    public async Task<UpdateResult> ExecuteUpdateAsync(
        ModpackManifest manifest,
        FileComparisonResult comparison,
        string valheimPath,
        string profileId,
        CancellationToken ct = default)
    {
        var result = new UpdateResult();
        var stagingDir = Path.Combine(Path.GetTempPath(), "ClickCSValheimLauncher_Staging", profileId);

        try
        {
            // Phase 0: Ensure BepInEx prerequisite is satisfied
            if (manifest.BepInEx != null && _bepInExService.NeedsInstall(valheimPath, manifest.BepInEx.RequiredVersion))
            {
                StatusChanged?.Invoke($"BepInEx {manifest.BepInEx.RequiredVersion} is required — installing...");
                var depResult = await _bepInExService.InstallAsync(manifest.BepInEx, valheimPath, ct);
                if (!depResult.Success)
                {
                    result.Success = false;
                    result.Message = depResult.Message;
                    result.Errors.AddRange(depResult.Errors);
                    StatusChanged?.Invoke(result.Message);
                    return result;
                }
                _logger.LogInformation("BepInEx install completed: {Message}", depResult.Message);
            }

            // Phase 1: Create backup of files that will be replaced
            StatusChanged?.Invoke("Creating backup...");
            var filesToBackup = comparison.FilesToDownload
                .Select(f => f.RelativePath)
                .Concat(comparison.FilesToRemove.Select(r => r.RelativePath))
                .ToList();

            result.BackupPath = await _backupHelper.CreateBackupAsync(valheimPath, profileId, filesToBackup, ct);

            // Phase 2: Download ALL files to staging directory first
            if (Directory.Exists(stagingDir))
                Directory.Delete(stagingDir, true);
            Directory.CreateDirectory(stagingDir);

            int processed = 0;
            int total = comparison.FilesToDownload.Count;
            var stagedFiles = new List<(ManifestFile File, string StagedPath)>();

            foreach (var file in comparison.FilesToDownload)
            {
                ct.ThrowIfCancellationRequested();
                processed++;
                var progress = 20 + ((double)processed / total * 40);
                ProgressChanged?.Invoke(progress);
                StatusChanged?.Invoke($"Downloading ({processed}/{total}): {file.RelativePath}");

                var stagedPath = await DownloadToStagingAsync(file, stagingDir, ct);
                stagedFiles.Add((file, stagedPath));
                _logger.LogDebug("Staged: {Path}", file.RelativePath);
            }

            // Phase 3: Verify ALL staged file hashes before touching live files
            StatusChanged?.Invoke("Verifying downloaded files...");
            ProgressChanged?.Invoke(65);

            foreach (var (file, stagedPath) in stagedFiles)
            {
                ct.ThrowIfCancellationRequested();
                var actualHash = await HashHelper.ComputeSha256Async(stagedPath, ct);
                if (!HashHelper.VerifyHash(file.Sha256, actualHash))
                {
                    result.Success = false;
                    result.Message = $"Hash verification failed for {file.RelativePath}. " +
                                    "No files were modified. Backup is available for recovery.";
                    result.Errors.Add($"Hash mismatch: {file.RelativePath} (expected {file.Sha256}, got {actualHash})");
                    _logger.LogError("STAGED FILE HASH MISMATCH: {Path} expected={Expected} actual={Actual}",
                        file.RelativePath, file.Sha256, actualHash);
                    StatusChanged?.Invoke(result.Message);
                    return result;
                }
            }

            // Phase 4: Apply all verified files to live directory
            StatusChanged?.Invoke("Installing verified files...");
            processed = 0;

            foreach (var (file, stagedPath) in stagedFiles)
            {
                ct.ThrowIfCancellationRequested();
                processed++;
                ProgressChanged?.Invoke(70 + ((double)processed / total * 20));

                var safePath = PathValidator.GetSafePath(file.RelativePath, valheimPath);
                if (safePath == null)
                {
                    result.Warnings.Add($"Skipped unsafe path: {file.RelativePath}");
                    continue;
                }

                try
                {
                    var dir = Path.GetDirectoryName(safePath);
                    if (dir != null)
                        Directory.CreateDirectory(dir);

                    // Atomic replace: delete existing, move staged file in
                    if (File.Exists(safePath))
                        File.Delete(safePath);
                    File.Move(stagedPath, safePath);
                    result.FilesUpdated++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to install staged file: {Path}", file.RelativePath);
                    result.Errors.Add($"Install failed: {file.RelativePath} - {ex.Message}");
                }
            }

            // Phase 5: Process removals (only managed files)
            foreach (var removal in comparison.FilesToRemove)
            {
                ct.ThrowIfCancellationRequested();
                var safePath = PathValidator.GetSafePath(removal.RelativePath, valheimPath);
                if (safePath != null && File.Exists(safePath))
                {
                    try
                    {
                        File.Delete(safePath);
                        result.FilesRemoved++;
                        _logger.LogInformation("Removed: {Path} (Reason: {Reason})", removal.RelativePath, removal.Reason);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to remove: {Path}", removal.RelativePath);
                        result.Errors.Add($"Failed to remove: {removal.RelativePath} - {ex.Message}");
                    }
                }
            }

            // Phase 6: Update local state ONLY on full success
            result.Success = result.Errors.Count == 0;

            if (result.Success)
            {
                var managedState = new ManagedFilesState
                {
                    ProfileId = profileId,
                    LastUpdated = DateTime.UtcNow,
                    InstalledVersion = manifest.ModpackVersion,
                    Files = manifest.Files.Select(f => new ManagedFileEntry
                    {
                        RelativePath = f.RelativePath,
                        Sha256 = f.Sha256,
                        InstalledAt = DateTime.UtcNow
                    }).ToList()
                };

                _manifestService.SaveManagedFiles(managedState);
                _manifestService.SaveInstalledManifest(profileId, manifest);

                result.Message = $"Update complete: {result.FilesUpdated} files updated, {result.FilesRemoved} removed";
            }
            else
            {
                result.Message = $"Update completed with {result.Errors.Count} errors. " +
                                "Local state was NOT updated. Repair or rollback recommended.";
            }

            ProgressChanged?.Invoke(100);
            StatusChanged?.Invoke(result.Message);
        }
        catch (OperationCanceledException)
        {
            result.Success = false;
            result.Message = "Update cancelled. No local state was changed.";
            StatusChanged?.Invoke(result.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update failed");
            result.Success = false;
            result.Message = $"Update failed: {ex.Message}";
            StatusChanged?.Invoke(result.Message);
        }
        finally
        {
            // Clean up staging directory
            try
            {
                if (Directory.Exists(stagingDir))
                    Directory.Delete(stagingDir, true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean up staging directory: {Dir}", stagingDir);
            }
        }

        return result;
    }

    public async Task<UpdateResult> RepairAsync(
        ModpackManifest manifest,
        string valheimPath,
        string profileId,
        CancellationToken ct = default)
    {
        StatusChanged?.Invoke("Starting repair...");
        _logger.LogInformation("Starting repair for profile {ProfileId}", profileId);

        var comparison = await CompareFilesAsync(manifest, valheimPath, profileId, ct);
        return await ExecuteUpdateAsync(manifest, comparison, valheimPath, profileId, ct);
    }

    private async Task<string> DownloadToStagingAsync(ManifestFile file, string stagingDir, CancellationToken ct)
    {
        // Validate path safety against staging dir
        var relativeSafe = file.RelativePath.Replace('/', Path.DirectorySeparatorChar);
        var stagedPath = Path.Combine(stagingDir, relativeSafe);

        // Ensure staged path doesn't escape staging dir
        var normalizedStaging = Path.GetFullPath(stagingDir);
        var normalizedTarget = Path.GetFullPath(stagedPath);
        if (!normalizedTarget.StartsWith(normalizedStaging, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Unsafe staging path: {file.RelativePath}");

        var dir = Path.GetDirectoryName(stagedPath);
        if (dir != null)
            Directory.CreateDirectory(dir);

        using var response = await _httpClient.GetAsync(file.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var fileStream = new FileStream(stagedPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await stream.CopyToAsync(fileStream, ct);

        return stagedPath;
    }
}
