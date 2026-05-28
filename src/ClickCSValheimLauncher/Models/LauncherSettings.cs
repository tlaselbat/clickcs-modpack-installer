using System.Text.Json.Serialization;

namespace ClickCSValheimLauncher.Models;

public class LauncherSettings
{
    [JsonPropertyName("valheim_path")]
    public string? ValheimPath { get; set; }

    [JsonPropertyName("steam_path")]
    public string? SteamPath { get; set; }

    [JsonPropertyName("launcher_update_url")]
    public string LauncherUpdateUrl { get; set; } = "https://clickcs.org/valheim_modpack/launcher/latest.json";

    [JsonPropertyName("check_updates_on_startup")]
    public bool CheckUpdatesOnStartup { get; set; } = true;

    [JsonPropertyName("copy_password_before_launch")]
    public bool CopyPasswordBeforeLaunch { get; set; } = true;

    [JsonPropertyName("clear_clipboard_after_seconds")]
    public int ClearClipboardAfterSeconds { get; set; } = 60;

    [JsonPropertyName("allow_insecure_http")]
    public bool AllowInsecureHttp { get; set; }
}
