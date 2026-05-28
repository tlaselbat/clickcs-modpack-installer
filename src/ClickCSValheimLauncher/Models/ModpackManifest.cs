using System.Text.Json.Serialization;

namespace ClickCSValheimLauncher.Models;

public class ModpackManifest
{
    [JsonPropertyName("schema_version")]
    public int SchemaVersion { get; set; } = 1;

    [JsonPropertyName("modpack_id")]
    public string ModpackId { get; set; } = string.Empty;

    [JsonPropertyName("modpack_name")]
    public string ModpackName { get; set; } = string.Empty;

    [JsonPropertyName("modpack_version")]
    public string ModpackVersion { get; set; } = "0.0.0";

    [JsonPropertyName("valheim_version")]
    public string? ValheimVersion { get; set; }

    [JsonPropertyName("bepinex_version")]
    public string? BepInExVersion { get; set; }

    [JsonPropertyName("bepinex")]
    public BepInExRequirement? BepInEx { get; set; }

    [JsonPropertyName("changelog_url")]
    public string? ChangelogUrl { get; set; }

    [JsonPropertyName("files")]
    public List<ManifestFile> Files { get; set; } = new();

    [JsonPropertyName("removals")]
    public List<ManifestRemoval> Removals { get; set; } = new();
}

public class ManifestFile
{
    [JsonPropertyName("relative_path")]
    public string RelativePath { get; set; } = string.Empty;

    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = string.Empty;

    [JsonPropertyName("size_bytes")]
    public long SizeBytes { get; set; }

    [JsonPropertyName("download_url")]
    public string DownloadUrl { get; set; } = string.Empty;

    [JsonPropertyName("file_type")]
    public string FileType { get; set; } = "other";

    [JsonPropertyName("required")]
    public bool Required { get; set; } = true;
}

public class ManifestRemoval
{
    [JsonPropertyName("relative_path")]
    public string RelativePath { get; set; } = string.Empty;

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;

    [JsonPropertyName("remove_only_if_managed")]
    public bool RemoveOnlyIfManaged { get; set; } = true;
}

public class BepInExRequirement
{
    [JsonPropertyName("required_version")]
    public string RequiredVersion { get; set; } = string.Empty;

    [JsonPropertyName("download_url")]
    public string DownloadUrl { get; set; } = string.Empty;

    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = string.Empty;

    [JsonPropertyName("size_bytes")]
    public long SizeBytes { get; set; }
}

public class BepInExInstallInfo
{
    public bool IsInstalled { get; set; }
    public string? InstalledVersion { get; set; }
}

public class DependencyInstallResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool WasInstalled { get; set; }
    public string? InstalledVersion { get; set; }
    public List<string> Errors { get; set; } = new();
}
