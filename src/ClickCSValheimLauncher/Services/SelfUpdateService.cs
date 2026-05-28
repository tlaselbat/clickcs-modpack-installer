using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using ClickCSValheimLauncher.Helpers;
using ClickCSValheimLauncher.Models;
using Microsoft.Extensions.Logging;

namespace ClickCSValheimLauncher.Services;

// PathValidator is in Helpers namespace, already imported above

public class SelfUpdateService
{
    private readonly ILogger<SelfUpdateService> _logger;
    private readonly HttpClient _httpClient;
    private readonly SettingsService _settingsService;

    public SelfUpdateService(
        ILogger<SelfUpdateService> logger,
        HttpClient httpClient,
        SettingsService settingsService)
    {
        _logger = logger;
        _httpClient = httpClient;
        _settingsService = settingsService;
    }

    public string CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

    public async Task<LauncherUpdateInfo?> CheckForUpdateAsync(CancellationToken ct = default)
    {
        var updateUrl = _settingsService.Settings.LauncherUpdateUrl;
        if (string.IsNullOrWhiteSpace(updateUrl))
        {
            _logger.LogDebug("No launcher update URL configured");
            return null;
        }

        // Validate update URL
        if (!PathValidator.IsValidUrl(updateUrl))
        {
            _logger.LogWarning("Launcher update URL is not a valid HTTP/HTTPS URL: {Url}", updateUrl);
            return null;
        }

        try
        {
            var response = await _httpClient.GetAsync(updateUrl, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            var updateInfo = JsonSerializer.Deserialize<LauncherUpdateInfo>(json);

            if (updateInfo == null)
            {
                _logger.LogWarning("Failed to deserialize launcher update info");
                return null;
            }

            // Validate schema
            if (!ValidateUpdateInfo(updateInfo))
                return null;

            var current = Version.Parse(CurrentVersion);
            var latest = Version.Parse(updateInfo.Version);

            // Prevent downgrades
            if (latest <= current)
            {
                _logger.LogDebug("Launcher is up to date (v{Version})", CurrentVersion);
                return null;
            }

            _logger.LogInformation("Launcher update available: {Current} -> {Latest}",
                CurrentVersion, updateInfo.Version);
            return updateInfo;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check for launcher updates");
            return null;
        }
    }

    private bool ValidateUpdateInfo(LauncherUpdateInfo info)
    {
        if (string.IsNullOrWhiteSpace(info.Version))
        {
            _logger.LogWarning("Launcher update info missing version");
            return false;
        }

        if (!Version.TryParse(info.Version, out _))
        {
            _logger.LogWarning("Launcher update info has invalid version format: {Version}", info.Version);
            return false;
        }

        if (string.IsNullOrWhiteSpace(info.DownloadUrl) || !PathValidator.IsValidUrl(info.DownloadUrl))
        {
            _logger.LogWarning("Launcher update info has invalid download URL");
            return false;
        }

        if (!PathValidator.IsValidSha256(info.Sha256))
        {
            _logger.LogWarning("Launcher update info has invalid SHA256 hash format");
            return false;
        }

        // Validate changelog URL if present
        if (!string.IsNullOrEmpty(info.ChangelogUrl) && !PathValidator.IsValidUrl(info.ChangelogUrl))
        {
            _logger.LogWarning("Launcher update info has invalid changelog URL");
            return false;
        }

        return true;
    }

    public async Task<bool> DownloadAndApplyUpdateAsync(LauncherUpdateInfo updateInfo, CancellationToken ct = default)
    {
        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "ClickCSValheimLauncher_Update");
            Directory.CreateDirectory(tempDir);

            var tempFile = Path.Combine(tempDir, $"ClickCS-Valheim-Launcher-{updateInfo.Version}.exe");

            // Download
            _logger.LogInformation("Downloading launcher update v{Version}...", updateInfo.Version);
            using (var response = await _httpClient.GetAsync(updateInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                response.EnsureSuccessStatusCode();
                using var stream = await response.Content.ReadAsStreamAsync(ct);
                using var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None);
                await stream.CopyToAsync(fileStream, ct);
            }

            // Verify hash
            var actualHash = await HashHelper.ComputeSha256Async(tempFile, ct);
            if (!HashHelper.VerifyHash(updateInfo.Sha256, actualHash))
            {
                _logger.LogError("Launcher update hash mismatch! Expected: {Expected}, Got: {Actual}",
                    updateInfo.Sha256, actualHash);
                try { File.Delete(tempFile); } catch { }
                return false;
            }

            _logger.LogInformation("Launcher update hash verified successfully");

            // Launch update helper
            var currentExe = Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location;
            var helperExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "ClickCS Valheim Launcher Updater.exe");

            if (!File.Exists(helperExe))
            {
                _logger.LogError("Update helper not found at: {Path}", helperExe);
                try { File.Delete(tempFile); } catch { }
                return false;
            }

            var args = $"\"{tempFile}\" \"{currentExe}\" \"{currentExe}.bak\"";
            _logger.LogInformation("Launching update helper: {Helper}", helperExe);

            Process.Start(new ProcessStartInfo
            {
                FileName = helperExe,
                Arguments = args,
                UseShellExecute = true
            });

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download/apply launcher update");
            return false;
        }
    }
}
