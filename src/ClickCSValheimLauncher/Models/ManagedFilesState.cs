using System.Text.Json.Serialization;

namespace ClickCSValheimLauncher.Models;

public class ManagedFilesState
{
    [JsonPropertyName("profile_id")]
    public string ProfileId { get; set; } = string.Empty;

    [JsonPropertyName("last_updated")]
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("installed_version")]
    public string InstalledVersion { get; set; } = "0.0.0";

    [JsonPropertyName("files")]
    public List<ManagedFileEntry> Files { get; set; } = new();
}

public class ManagedFileEntry
{
    [JsonPropertyName("relative_path")]
    public string RelativePath { get; set; } = string.Empty;

    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = string.Empty;

    [JsonPropertyName("installed_at")]
    public DateTime InstalledAt { get; set; } = DateTime.UtcNow;
}
