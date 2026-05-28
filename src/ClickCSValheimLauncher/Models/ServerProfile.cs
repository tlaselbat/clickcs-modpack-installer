using System.Text.Json.Serialization;

namespace ClickCSValheimLauncher.Models;

public class ServerProfile
{
    [JsonPropertyName("profile_id")]
    public string ProfileId { get; set; } = Guid.NewGuid().ToString("N")[..8];

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = "New Profile";

    [JsonPropertyName("manifest_url")]
    public string ManifestUrl { get; set; } = string.Empty;

    [JsonPropertyName("server_host")]
    public string ServerHost { get; set; } = string.Empty;

    [JsonPropertyName("server_port")]
    public int ServerPort { get; set; } = 2456;

    [JsonPropertyName("auto_connect_enabled")]
    public bool AutoConnectEnabled { get; set; } = true;

    [JsonPropertyName("password_saved")]
    public bool PasswordSaved { get; set; }

    [JsonPropertyName("notes")]
    public string Notes { get; set; } = string.Empty;
}
