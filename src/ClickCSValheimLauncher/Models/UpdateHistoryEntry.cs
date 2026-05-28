using System.Text.Json.Serialization;

namespace ClickCSValheimLauncher.Models;

public class UpdateHistory
{
    [JsonPropertyName("entries")]
    public List<UpdateHistoryEntry> Entries { get; set; } = new();
}

public class UpdateHistoryEntry
{
    [JsonPropertyName("profile_id")]
    public string ProfileId { get; set; } = string.Empty;

    [JsonPropertyName("from_version")]
    public string FromVersion { get; set; } = string.Empty;

    [JsonPropertyName("to_version")]
    public string ToVersion { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("backup_path")]
    public string? BackupPath { get; set; }

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; set; }
}
