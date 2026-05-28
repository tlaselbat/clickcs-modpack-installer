using System.Text.Json;
using ClickCSValheimLauncher.Models;
using Microsoft.Extensions.Logging;

namespace ClickCSValheimLauncher.Services;

public class SettingsService
{
    private readonly ILogger<SettingsService> _logger;
    private readonly string _settingsPath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public LauncherSettings Settings { get; private set; } = new();

    public SettingsService(ILogger<SettingsService> logger)
    {
        _logger = logger;
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appDir = Path.Combine(appData, "ClickCS Valheim Launcher");
        Directory.CreateDirectory(appDir);
        _settingsPath = Path.Combine(appDir, "settings.json");
    }

    public void Load()
    {
        if (!File.Exists(_settingsPath))
        {
            Settings = new LauncherSettings();
            _logger.LogInformation("No settings file found, using defaults");
            return;
        }

        try
        {
            var json = File.ReadAllText(_settingsPath);
            Settings = JsonSerializer.Deserialize<LauncherSettings>(json, JsonOptions) ?? new();
            _logger.LogInformation("Settings loaded");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load settings");
            Settings = new LauncherSettings();
        }
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(Settings, JsonOptions);
            File.WriteAllText(_settingsPath, json);
            _logger.LogDebug("Settings saved");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
        }
    }

    public string GetAppDataPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "ClickCS Valheim Launcher");
    }

    public string GetLogPath()
    {
        return Path.Combine(GetAppDataPath(), "launcher.log");
    }
}
