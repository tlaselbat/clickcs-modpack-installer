using System.Diagnostics;
using System.Text.RegularExpressions;
using ClickCSValheimLauncher.Models;
using Microsoft.Extensions.Logging;

namespace ClickCSValheimLauncher.Services;

public class LaunchService
{
    private const int ValheimAppId = 892970;
    private readonly ILogger<LaunchService> _logger;
    private readonly SteamDetectorService _steamDetector;
    private readonly PasswordStorageService _passwordStorage;
    private readonly SettingsService _settingsService;

    // Only allow safe hostname characters: alphanumeric, dots, hyphens, colons (IPv6)
    private static readonly Regex SafeHostRegex = new(
        @"^[a-zA-Z0-9\.\-\:]+$", RegexOptions.Compiled);

    public LaunchService(
        ILogger<LaunchService> logger,
        SteamDetectorService steamDetector,
        PasswordStorageService passwordStorage,
        SettingsService settingsService)
    {
        _logger = logger;
        _steamDetector = steamDetector;
        _passwordStorage = passwordStorage;
        _settingsService = settingsService;
    }

    public LaunchResult LaunchValheim(ServerProfile profile, bool autoConnect, bool copyPassword)
    {
        var result = new LaunchResult();

        try
        {
            // Copy password to clipboard if requested
            if (copyPassword && profile.PasswordSaved)
            {
                var password = _passwordStorage.GetPassword(profile.ProfileId);
                if (!string.IsNullOrEmpty(password))
                {
                    CopyToClipboard(password);
                    result.PasswordCopied = true;
                    _logger.LogInformation("Password copied to clipboard");

                    if (_settingsService.Settings.ClearClipboardAfterSeconds > 0)
                    {
                        ScheduleClipboardClear(_settingsService.Settings.ClearClipboardAfterSeconds);
                    }
                }
            }

            // Build launch command
            var steamExe = _steamDetector.GetSteamExePath(_settingsService.Settings.SteamPath);

            if (steamExe != null)
            {
                result = LaunchViaSteamExe(steamExe, profile, autoConnect, result);
            }
            else
            {
                result = LaunchViaSteamUri(profile, autoConnect, result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to launch Valheim");
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    private static string? SanitizeHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return null;

        var trimmed = host.Trim();

        // Reject any shell metacharacters, spaces, quotes, etc.
        if (!SafeHostRegex.IsMatch(trimmed))
            return null;

        // Reject excessively long hostnames
        if (trimmed.Length > 253)
            return null;

        return trimmed;
    }

    private static bool IsPortValid(int port) => port > 0 && port <= 65535;

    private LaunchResult LaunchViaSteamExe(string steamExe, ServerProfile profile, bool autoConnect, LaunchResult result)
    {
        var args = $"-applaunch {ValheimAppId}";

        if (autoConnect && profile.AutoConnectEnabled)
        {
            var safeHost = SanitizeHost(profile.ServerHost);
            if (safeHost != null && IsPortValid(profile.ServerPort))
            {
                args += $" +connect {safeHost}:{profile.ServerPort}";
            }
        }

        _logger.LogInformation("Launching: {Exe} {Args}", steamExe, args);

        var psi = new ProcessStartInfo
        {
            FileName = steamExe,
            Arguments = args,
            UseShellExecute = true
        };

        Process.Start(psi);
        result.Success = true;
        result.LaunchMethod = "Steam executable";
        result.LaunchCommand = $"\"{steamExe}\" {args}";
        return result;
    }

    private LaunchResult LaunchViaSteamUri(ServerProfile profile, bool autoConnect, LaunchResult result)
    {
        var uri = $"steam://rungameid/{ValheimAppId}";

        if (autoConnect && profile.AutoConnectEnabled)
        {
            var safeHost = SanitizeHost(profile.ServerHost);
            if (safeHost != null && IsPortValid(profile.ServerPort))
            {
                uri = $"steam://rungameid/{ValheimAppId}//+connect%20{safeHost}:{profile.ServerPort}/";
            }
        }

        _logger.LogInformation("Launching via URI: {Uri}", uri);

        var psi = new ProcessStartInfo
        {
            FileName = uri,
            UseShellExecute = true
        };

        Process.Start(psi);
        result.Success = true;
        result.LaunchMethod = "Steam URI";
        result.LaunchCommand = uri;
        return result;
    }

    public string? GetSavedPassword(string profileId)
    {
        return _passwordStorage.GetPassword(profileId);
    }

    private static void CopyToClipboard(string text)
    {
        var thread = new Thread(() =>
        {
            System.Windows.Clipboard.SetText(text);
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    private void ScheduleClipboardClear(int seconds)
    {
        Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(seconds));
            var thread = new Thread(() =>
            {
                try
                {
                    System.Windows.Clipboard.Clear();
                    _logger.LogDebug("Clipboard cleared after {Seconds}s", seconds);
                }
                catch { }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
        });
    }
}

public class LaunchResult
{
    public bool Success { get; set; }
    public string LaunchMethod { get; set; } = string.Empty;
    public string LaunchCommand { get; set; } = string.Empty;
    public bool PasswordCopied { get; set; }
    public string? ErrorMessage { get; set; }
}
