using System.Text.Json.Serialization;

namespace ClickCSValheimLauncher.Models;

public class LauncherUpdateInfo
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("download_url")]
    public string DownloadUrl { get; set; } = string.Empty;

    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = string.Empty;

    [JsonPropertyName("changelog_url")]
    public string? ChangelogUrl { get; set; }

    [JsonPropertyName("mandatory")]
    public bool Mandatory { get; set; }
}
