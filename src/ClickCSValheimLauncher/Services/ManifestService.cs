using System.IO;
using System.Net.Http;
using System.Text.Json;
using ClickCSValheimLauncher.Helpers;
using ClickCSValheimLauncher.Models;
using Microsoft.Extensions.Logging;

namespace ClickCSValheimLauncher.Services;

public class ManifestService
{
    private readonly ILogger<ManifestService> _logger;
    private readonly HttpClient _httpClient;
    private readonly SettingsService _settingsService;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public ManifestService(ILogger<ManifestService> logger, HttpClient httpClient, SettingsService settingsService)
    {
        _logger = logger;
        _httpClient = httpClient;
        _settingsService = settingsService;
    }

    public async Task<ModpackManifest?> DownloadManifestAsync(string manifestUrl, CancellationToken ct = default)
    {
        ValidateUrl(manifestUrl);

        _logger.LogInformation("Downloading manifest from {Url}", manifestUrl);

        var response = await _httpClient.GetAsync(manifestUrl, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        var manifest = JsonSerializer.Deserialize<ModpackManifest>(json, JsonOptions);

        if (manifest == null)
        {
            _logger.LogError("Failed to deserialize manifest from {Url}", manifestUrl);
            return null;
        }

        if (!ValidateManifest(manifest))
            return null;

        _logger.LogInformation("Manifest downloaded: {Name} v{Version} ({FileCount} files)",
            manifest.ModpackName, manifest.ModpackVersion, manifest.Files.Count);

        return manifest;
    }

    public bool ValidateManifest(ModpackManifest manifest)
    {
        if (manifest.SchemaVersion != 1)
        {
            _logger.LogError("Unsupported manifest schema version: {Version}", manifest.SchemaVersion);
            return false;
        }

        if (string.IsNullOrWhiteSpace(manifest.ModpackId))
        {
            _logger.LogError("Manifest missing modpack_id");
            return false;
        }

        if (manifest.ModpackId.Length > 128)
        {
            _logger.LogError("Manifest modpack_id exceeds max length");
            return false;
        }

        if (string.IsNullOrWhiteSpace(manifest.ModpackVersion))
        {
            _logger.LogError("Manifest missing modpack_version");
            return false;
        }

        if (manifest.ModpackName.Length > 256)
        {
            _logger.LogError("Manifest modpack_name exceeds max length");
            return false;
        }

        // Validate changelog URL if present
        if (!string.IsNullOrEmpty(manifest.ChangelogUrl) && !PathValidator.IsValidUrl(manifest.ChangelogUrl))
        {
            _logger.LogError("Manifest changelog_url is not a valid HTTP/HTTPS URL");
            return false;
        }

        // Track duplicate paths
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in manifest.Files)
        {
            if (string.IsNullOrWhiteSpace(file.RelativePath))
            {
                _logger.LogError("Manifest file entry missing relative_path");
                return false;
            }

            // Normalize for duplicate detection
            var normalizedPath = file.RelativePath.Replace('\\', '/');
            if (!seenPaths.Add(normalizedPath))
            {
                _logger.LogError("Manifest contains duplicate path: {Path}", file.RelativePath);
                return false;
            }

            if (string.IsNullOrWhiteSpace(file.Sha256))
            {
                _logger.LogError("Manifest file entry missing sha256: {Path}", file.RelativePath);
                return false;
            }

            // Validate SHA256 format
            if (!PathValidator.IsValidSha256(file.Sha256))
            {
                _logger.LogError("Manifest file entry has invalid sha256 format: {Path}", file.RelativePath);
                return false;
            }

            if (string.IsNullOrWhiteSpace(file.DownloadUrl))
            {
                _logger.LogError("Manifest file entry missing download_url: {Path}", file.RelativePath);
                return false;
            }

            // Validate download URL
            if (!PathValidator.IsValidUrl(file.DownloadUrl))
            {
                _logger.LogError("Manifest file entry has invalid download_url: {Path}", file.RelativePath);
                return false;
            }

            // Validate file size
            if (!PathValidator.IsFileSizeAllowed(file.SizeBytes))
            {
                _logger.LogError("Manifest file entry has invalid size ({Size} bytes): {Path}",
                    file.SizeBytes, file.RelativePath);
                return false;
            }

            if (PathValidator.ContainsInvalidChars(file.RelativePath))
            {
                _logger.LogError("Manifest file entry contains invalid characters: {Path}", file.RelativePath);
                return false;
            }

            if (file.RelativePath.Contains("..") || Path.IsPathRooted(file.RelativePath))
            {
                _logger.LogError("Manifest file entry contains unsafe path: {Path}", file.RelativePath);
                return false;
            }

            // Reject NTFS alternate data streams
            if (file.RelativePath.Contains(':'))
            {
                _logger.LogError("Manifest file entry contains NTFS ADS marker: {Path}", file.RelativePath);
                return false;
            }
        }

        // Validate optional bepinex block
        if (manifest.BepInEx != null)
        {
            var bx = manifest.BepInEx;

            if (string.IsNullOrWhiteSpace(bx.RequiredVersion))
            {
                _logger.LogError("Manifest bepinex block missing required_version");
                return false;
            }

            if (string.IsNullOrWhiteSpace(bx.DownloadUrl) || !PathValidator.IsValidUrl(bx.DownloadUrl))
            {
                _logger.LogError("Manifest bepinex block has invalid or missing download_url");
                return false;
            }

            if (!PathValidator.IsValidSha256(bx.Sha256))
            {
                _logger.LogError("Manifest bepinex block has invalid sha256 format");
                return false;
            }

            if (!PathValidator.IsFileSizeAllowed(bx.SizeBytes) || bx.SizeBytes == 0)
            {
                _logger.LogError("Manifest bepinex block has invalid size_bytes: {Size}", bx.SizeBytes);
                return false;
            }
        }

        // Validate removals
        foreach (var removal in manifest.Removals)
        {
            if (string.IsNullOrWhiteSpace(removal.RelativePath))
            {
                _logger.LogError("Manifest removal entry missing relative_path");
                return false;
            }

            if (removal.RelativePath.Contains("..") || Path.IsPathRooted(removal.RelativePath))
            {
                _logger.LogError("Manifest removal entry contains unsafe path: {Path}", removal.RelativePath);
                return false;
            }

            if (removal.RelativePath.Contains(':'))
            {
                _logger.LogError("Manifest removal entry contains NTFS ADS marker: {Path}", removal.RelativePath);
                return false;
            }
        }

        return true;
    }

    public ManagedFilesState? LoadManagedFiles(string profileId)
    {
        var path = GetManagedFilesPath(profileId);
        if (!File.Exists(path))
            return null;

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<ManagedFilesState>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load managed files for profile {ProfileId}", profileId);
            return null;
        }
    }

    public void SaveManagedFiles(ManagedFilesState state)
    {
        var path = GetManagedFilesPath(state.ProfileId);
        var dir = Path.GetDirectoryName(path);
        if (dir != null)
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(state, JsonOptions);
        File.WriteAllText(path, json);
    }

    public ModpackManifest? LoadInstalledManifest(string profileId)
    {
        var path = GetInstalledManifestPath(profileId);
        if (!File.Exists(path))
            return null;

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<ModpackManifest>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load installed manifest for profile {ProfileId}", profileId);
            return null;
        }
    }

    public void SaveInstalledManifest(string profileId, ModpackManifest manifest)
    {
        var path = GetInstalledManifestPath(profileId);
        var dir = Path.GetDirectoryName(path);
        if (dir != null)
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(manifest, JsonOptions);
        File.WriteAllText(path, json);
    }

    private void ValidateUrl(string url)
    {
        var uri = new Uri(url);
        if (uri.Scheme == "http" && !_settingsService.Settings.AllowInsecureHttp)
        {
            throw new InvalidOperationException(
                "HTTP URLs are not allowed unless insecure mode is explicitly enabled in settings.");
        }
    }

    private string GetManagedFilesPath(string profileId)
    {
        return Path.Combine(_settingsService.GetAppDataPath(), $"managed_files_{profileId}.json");
    }

    private string GetInstalledManifestPath(string profileId)
    {
        return Path.Combine(_settingsService.GetAppDataPath(), $"installed_manifest_{profileId}.json");
    }
}
