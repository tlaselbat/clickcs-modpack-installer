using Microsoft.Extensions.Logging;

namespace ClickCSValheimLauncher.Helpers;

public class BackupInfo
{
    public string FullPath { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int FileCount { get; set; }
    public long TotalSizeBytes { get; set; }
    public DateTime Created { get; set; }
}

public class FileBackupHelper
{
    private readonly ILogger<FileBackupHelper> _logger;

    public FileBackupHelper(ILogger<FileBackupHelper> logger)
    {
        _logger = logger;
    }

    public string GetBackupDirectory(string valheimPath, string profileId)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        return Path.Combine(valheimPath, "ClickCS_Backups", profileId, timestamp);
    }

    public async Task<string> CreateBackupAsync(
        string valheimPath,
        string profileId,
        IEnumerable<string> relativePaths,
        CancellationToken ct = default)
    {
        var backupDir = GetBackupDirectory(valheimPath, profileId);
        Directory.CreateDirectory(backupDir);

        int backedUp = 0;
        foreach (var relativePath in relativePaths)
        {
            ct.ThrowIfCancellationRequested();

            // Validate the relative path against valheim dir
            var safePath = PathValidator.GetSafePath(relativePath, valheimPath);
            if (safePath == null || !File.Exists(safePath))
                continue;

            var destPath = Path.Combine(backupDir, relativePath);
            var destDir = Path.GetDirectoryName(destPath);
            if (destDir != null)
                Directory.CreateDirectory(destDir);

            await CopyFileAsync(safePath, destPath, ct);
            backedUp++;
        }

        _logger.LogInformation("Backed up {Count} files to {BackupDir}", backedUp, backupDir);
        return backupDir;
    }

    public async Task<RestoreResult> RestoreBackupAsync(
        string backupDir,
        string valheimPath,
        CancellationToken ct = default)
    {
        var result = new RestoreResult();

        if (!Directory.Exists(backupDir))
            throw new DirectoryNotFoundException($"Backup directory not found: {backupDir}");

        var files = Directory.GetFiles(backupDir, "*", SearchOption.AllDirectories);

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(backupDir, file);

            // Validate each restored path for safety
            var safePath = PathValidator.GetSafePath(relativePath, valheimPath);
            if (safePath == null)
            {
                _logger.LogWarning("Skipping unsafe backup path during restore: {Path}", relativePath);
                result.SkippedFiles.Add(relativePath);
                continue;
            }

            var destDir = Path.GetDirectoryName(safePath);
            if (destDir != null)
                Directory.CreateDirectory(destDir);

            await CopyFileAsync(file, safePath, ct);
            result.RestoredFiles.Add(relativePath);
        }

        _logger.LogInformation("Restored {Restored} files from {BackupDir} ({Skipped} skipped)",
            result.RestoredFiles.Count, backupDir, result.SkippedFiles.Count);

        result.Success = true;
        return result;
    }

    public BackupInfo? GetBackupInfo(string backupDir)
    {
        if (!Directory.Exists(backupDir))
            return null;

        var files = Directory.GetFiles(backupDir, "*", SearchOption.AllDirectories);
        var dirInfo = new DirectoryInfo(backupDir);

        return new BackupInfo
        {
            FullPath = backupDir,
            DisplayName = dirInfo.Name,
            FileCount = files.Length,
            TotalSizeBytes = files.Sum(f => new FileInfo(f).Length),
            Created = dirInfo.CreationTime
        };
    }

    public List<BackupInfo> GetAvailableBackupsDetailed(string valheimPath, string profileId)
    {
        var backupsRoot = Path.Combine(valheimPath, "ClickCS_Backups", profileId);
        if (!Directory.Exists(backupsRoot))
            return new List<BackupInfo>();

        return Directory.GetDirectories(backupsRoot)
            .OrderByDescending(d => d)
            .Select(d => GetBackupInfo(d))
            .Where(b => b != null)
            .Cast<BackupInfo>()
            .ToList();
    }

    public List<string> GetAvailableBackups(string valheimPath, string profileId)
    {
        var backupsRoot = Path.Combine(valheimPath, "ClickCS_Backups", profileId);
        if (!Directory.Exists(backupsRoot))
            return new List<string>();

        return Directory.GetDirectories(backupsRoot)
            .OrderByDescending(d => d)
            .ToList();
    }

    private static async Task CopyFileAsync(string source, string dest, CancellationToken ct)
    {
        const int bufferSize = 81920;
        using var sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, true);
        using var destStream = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, true);
        await sourceStream.CopyToAsync(destStream, bufferSize, ct);
    }
}

public class RestoreResult
{
    public bool Success { get; set; }
    public List<string> RestoredFiles { get; set; } = new();
    public List<string> SkippedFiles { get; set; } = new();
}
