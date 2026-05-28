using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using ClickCSValheimLauncher.Helpers;
using ClickCSValheimLauncher.Models;
using Microsoft.Extensions.Logging;

namespace ClickCSValheimLauncher.Services;

public class BepInExService
{
    private readonly ILogger<BepInExService> _logger;
    private readonly HttpClient _httpClient;
    private readonly SettingsService _settingsService;

    public event Action<string>? StatusChanged;
    public event Action<double>? ProgressChanged;

    public BepInExService(
        ILogger<BepInExService> logger,
        HttpClient httpClient,
        SettingsService settingsService)
    {
        _logger = logger;
        _httpClient = httpClient;
        _settingsService = settingsService;
    }

    public BepInExInstallInfo DetectInstallation(string valheimPath)
    {
        var info = new BepInExInstallInfo();

        var bepInExDir = Path.Combine(valheimPath, "BepInEx");
        var winhttpDll = Path.Combine(valheimPath, "winhttp.dll");
        var doorstopCfg = Path.Combine(valheimPath, "doorstop_config.ini");

        if (!Directory.Exists(bepInExDir) || !File.Exists(winhttpDll) || !File.Exists(doorstopCfg))
        {
            _logger.LogInformation("BepInEx not detected in {Path}", valheimPath);
            return info;
        }

        info.IsInstalled = true;
        info.InstalledVersion = ReadInstalledVersion(valheimPath);
        _logger.LogInformation("BepInEx detected: version={Version}", info.InstalledVersion ?? "unknown");
        return info;
    }

    public bool NeedsInstall(string valheimPath, string requiredVersion)
    {
        var info = DetectInstallation(valheimPath);
        if (!info.IsInstalled)
            return true;

        if (string.IsNullOrEmpty(info.InstalledVersion))
        {
            _logger.LogWarning("BepInEx installed but version unknown; will reinstall to satisfy {Required}", requiredVersion);
            return true;
        }

        if (!Version.TryParse(info.InstalledVersion, out var installed) ||
            !Version.TryParse(requiredVersion, out var required))
        {
            _logger.LogWarning("Could not compare BepInEx versions (installed={Installed}, required={Required}); reinstalling",
                info.InstalledVersion, requiredVersion);
            return true;
        }

        var needsUpdate = installed < required;
        if (needsUpdate)
            _logger.LogInformation("BepInEx upgrade needed: {Installed} < {Required}", installed, required);

        return needsUpdate;
    }

    public async Task<DependencyInstallResult> InstallAsync(
        BepInExRequirement requirement,
        string valheimPath,
        CancellationToken ct = default)
    {
        var result = new DependencyInstallResult();

        try
        {
            if (!PathValidator.IsValidUrl(requirement.DownloadUrl))
            {
                result.Message = "BepInEx download URL is invalid.";
                result.Errors.Add(result.Message);
                return result;
            }

            // Phase 1: Resolve cached zip
            StatusChanged?.Invoke("Checking BepInEx cache...");
            ProgressChanged?.Invoke(5);

            var cacheDir = Path.Combine(_settingsService.GetAppDataPath(), "dep_cache");
            Directory.CreateDirectory(cacheDir);
            var cacheFile = Path.Combine(cacheDir, $"{requirement.Sha256.ToLowerInvariant()}.zip");

            if (File.Exists(cacheFile))
            {
                _logger.LogInformation("BepInEx zip found in cache: {File}", cacheFile);
                StatusChanged?.Invoke("Verifying cached BepInEx package...");
                ProgressChanged?.Invoke(15);

                var cachedHash = await HashHelper.ComputeSha256Async(cacheFile, ct);
                if (!HashHelper.VerifyHash(requirement.Sha256, cachedHash))
                {
                    _logger.LogWarning("Cached BepInEx zip hash mismatch — re-downloading");
                    File.Delete(cacheFile);
                }
            }

            if (!File.Exists(cacheFile))
            {
                StatusChanged?.Invoke("Downloading BepInEx...");
                ProgressChanged?.Invoke(20);

                await DownloadWithProgressAsync(requirement.DownloadUrl, cacheFile, requirement.SizeBytes, ct);
                ProgressChanged?.Invoke(60);

                // Verify downloaded zip
                StatusChanged?.Invoke("Verifying BepInEx download integrity...");
                var downloadedHash = await HashHelper.ComputeSha256Async(cacheFile, ct);
                if (!HashHelper.VerifyHash(requirement.Sha256, downloadedHash))
                {
                    File.Delete(cacheFile);
                    result.Message = "BepInEx download hash verification failed. The file may be corrupt or tampered with.";
                    result.Errors.Add($"Hash mismatch: expected {requirement.Sha256}, got {downloadedHash}");
                    _logger.LogError("BepInEx hash mismatch: expected={Expected} actual={Actual}",
                        requirement.Sha256, downloadedHash);
                    return result;
                }
            }

            // Phase 2: Extract to Valheim root
            StatusChanged?.Invoke("Installing BepInEx...");
            ProgressChanged?.Invoke(65);

            ExtractZipToDirectory(cacheFile, valheimPath);
            ProgressChanged?.Invoke(95);

            result.Success = true;
            result.WasInstalled = true;
            result.InstalledVersion = requirement.RequiredVersion;
            result.Message = $"BepInEx {requirement.RequiredVersion} installed successfully.";

            StatusChanged?.Invoke(result.Message);
            ProgressChanged?.Invoke(100);
            _logger.LogInformation("BepInEx {Version} installed to {Path}", requirement.RequiredVersion, valheimPath);
        }
        catch (OperationCanceledException)
        {
            result.Message = "BepInEx installation cancelled.";
            StatusChanged?.Invoke(result.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BepInEx installation failed");
            result.Message = $"BepInEx installation failed: {ex.Message}";
            result.Errors.Add(ex.Message);
        }

        return result;
    }

    private async Task DownloadWithProgressAsync(string url, string destPath, long expectedBytes, CancellationToken ct)
    {
        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? expectedBytes;

        using var srcStream = await response.Content.ReadAsStreamAsync(ct);
        using var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

        var buffer = new byte[81920];
        long downloaded = 0;
        int read;

        while ((read = await srcStream.ReadAsync(buffer, ct)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
            downloaded += read;

            if (totalBytes > 0)
            {
                var pct = 20 + (double)downloaded / totalBytes * 40;
                ProgressChanged?.Invoke(pct);
            }
        }
    }

    private static void ExtractZipToDirectory(string zipPath, string destDir)
    {
        using var archive = ZipFile.OpenRead(zipPath);

        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name))
                continue;

            // Normalise the entry path — strip any leading slashes
            var entryPath = entry.FullName.Replace('\\', '/').TrimStart('/');

            // Safety: skip traversal attempts
            if (entryPath.Contains(".."))
                continue;

            var destPath = Path.GetFullPath(Path.Combine(destDir, entryPath));
            if (!destPath.StartsWith(Path.GetFullPath(destDir), StringComparison.OrdinalIgnoreCase))
                continue;

            var dir = Path.GetDirectoryName(destPath);
            if (dir != null)
                Directory.CreateDirectory(dir);

            entry.ExtractToFile(destPath, overwrite: true);
        }
    }

    private string? ReadInstalledVersion(string valheimPath)
    {
        // Prefer FileVersionInfo from BepInEx.dll
        var dllPath = Path.Combine(valheimPath, "BepInEx", "core", "BepInEx.dll");
        if (File.Exists(dllPath))
        {
            try
            {
                var fvi = FileVersionInfo.GetVersionInfo(dllPath);
                if (!string.IsNullOrEmpty(fvi.ProductVersion))
                    return fvi.ProductVersion.Split('+')[0].Trim(); // strip build metadata

                if (!string.IsNullOrEmpty(fvi.FileVersion))
                    return fvi.FileVersion.Trim();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read BepInEx.dll version info");
            }
        }

        // Fallback: parse version from changelog.txt
        var changelogPath = Path.Combine(valheimPath, "BepInEx", "changelog.txt");
        if (File.Exists(changelogPath))
        {
            try
            {
                var firstLine = File.ReadLines(changelogPath).FirstOrDefault();
                if (firstLine != null)
                {
                    var match = System.Text.RegularExpressions.Regex.Match(firstLine, @"\d+\.\d+\.\d+(\.\d+)?");
                    if (match.Success)
                        return match.Value;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read BepInEx changelog.txt");
            }
        }

        return null;
    }
}
