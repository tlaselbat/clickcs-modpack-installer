using System.IO;
using System.Net.Http;
using System.Text;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClickCSValheimLauncher.Models;
using ClickCSValheimLauncher.Services;
using Microsoft.Extensions.Logging;

namespace ClickCSValheimLauncher.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private const string ServerPassword = "tablex";

    private static readonly ServerProfile HardcodedProfile = new()
    {
        ProfileId = "clickcs",
        DisplayName = "ClickCS",
        ManifestUrl = "https://clickcs.org/valheim_modpack/manifest.json",
        ServerHost = "172.249.147.228",
        ServerPort = 2456,
        AutoConnectEnabled = true,
        PasswordSaved = true
    };

    private readonly ILogger<MainViewModel> _logger;
    private readonly SettingsService _settingsService;
    private readonly ManifestService _manifestService;
    private readonly UpdateEngineService _updateEngine;
    private readonly LaunchService _launchService;
    private readonly SelfUpdateService _selfUpdateService;
    private readonly SteamDetectorService _steamDetector;
    private readonly PasswordStorageService _passwordStorage;
    private readonly BepInExService _bepInExService;

    private CancellationTokenSource? _cts;
    private readonly StringBuilder _logBuilder = new();
    private const int MaxLogLines = 500;

    public MainViewModel(
        ILogger<MainViewModel> logger,
        SettingsService settingsService,
        ManifestService manifestService,
        UpdateEngineService updateEngine,
        LaunchService launchService,
        SelfUpdateService selfUpdateService,
        SteamDetectorService steamDetector,
        PasswordStorageService passwordStorage,
        BepInExService bepInExService)
    {
        _logger = logger;
        _settingsService = settingsService;
        _manifestService = manifestService;
        _updateEngine = updateEngine;
        _launchService = launchService;
        _selfUpdateService = selfUpdateService;
        _steamDetector = steamDetector;
        _passwordStorage = passwordStorage;
        _bepInExService = bepInExService;

        _updateEngine.StatusChanged += msg => Application.Current.Dispatcher.Invoke(() => StatusText = msg);
        _updateEngine.ProgressChanged += p => Application.Current.Dispatcher.Invoke(() => Progress = p);
    }

    [ObservableProperty] private string _statusText = "Ready";
    [ObservableProperty] private double _progress;
    [ObservableProperty] private string _installedVersion = "Not installed";
    [ObservableProperty] private string _latestVersion = "Unknown";
    [ObservableProperty] private string _valheimPath = "Not detected";
    [ObservableProperty] private string _changelogText = string.Empty;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UpdateCommand))]
    [NotifyCanExecuteChangedFor(nameof(RepairCommand))]
    [NotifyCanExecuteChangedFor(nameof(CheckForUpdatesCommand))]
    private bool _isWorking;
    [ObservableProperty] private bool _isIndeterminate;
    [ObservableProperty] private bool _autoConnectEnabled = true;
    [ObservableProperty] private bool _copyPasswordBeforeLaunch = true;
    [ObservableProperty] private bool _clearClipboardAfterLaunch = true;
    [ObservableProperty] private string _logOutput = string.Empty;
    [ObservableProperty] private LauncherUpdateInfo? _availableLauncherUpdate;
    [ObservableProperty] private string _lastErrorDetails = string.Empty;

    public string LauncherVersion => _selfUpdateService.CurrentVersion;

    public async Task InitializeAsync()
    {
        _settingsService.Load();

        // Detect paths
        var steamPath = _steamDetector.DetectSteamPath();
        if (steamPath != null)
            _settingsService.Settings.SteamPath ??= steamPath;

        var vPath = _settingsService.Settings.ValheimPath ?? _steamDetector.DetectValheimPath(steamPath);
        if (vPath != null)
        {
            _settingsService.Settings.ValheimPath = vPath;
            ValheimPath = vPath;
        }
        else
        {
            ValheimPath = "Not detected - please set in Settings";
        }

        _settingsService.Save();

        // Load settings
        AutoConnectEnabled = HardcodedProfile.AutoConnectEnabled;
        CopyPasswordBeforeLaunch = _settingsService.Settings.CopyPasswordBeforeLaunch;
        ClearClipboardAfterLaunch = _settingsService.Settings.ClearClipboardAfterSeconds > 0;

        // Ensure server password is always saved
        if (_passwordStorage.GetPassword(HardcodedProfile.ProfileId) != ServerPassword)
            _passwordStorage.SavePassword(HardcodedProfile.ProfileId, ServerPassword);

        // Load installed version
        var installed = _manifestService.LoadInstalledManifest(HardcodedProfile.ProfileId);
        InstalledVersion = installed?.ModpackVersion ?? "Not installed";

        // Check launcher update
        if (_settingsService.Settings.CheckUpdatesOnStartup)
        {
            AvailableLauncherUpdate = await _selfUpdateService.CheckForUpdateAsync();
        }

        AppendLog($"ClickCS Valheim Launcher v{LauncherVersion} initialized");
        AppendLog($"Valheim path: {ValheimPath}");

        // Detect and log BepInEx status
        if (Directory.Exists(ValheimPath))
        {
            var bepInfo = _bepInExService.DetectInstallation(ValheimPath);
            if (bepInfo.IsInstalled)
                AppendLog($"BepInEx: installed{(bepInfo.InstalledVersion != null ? $" v{bepInfo.InstalledVersion}" : " (version unknown)")} ");
            else
                AppendLog("BepInEx: not detected (will be installed automatically if required by modpack)");
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecuteAction))]
    private async Task CheckForUpdatesAsync()
    {

        IsWorking = true;
        IsIndeterminate = true;
        Progress = 0;
        StatusText = "Checking for updates...";

        try
        {
            _cts = new CancellationTokenSource();
            var manifest = await _manifestService.DownloadManifestAsync(
                HardcodedProfile.ManifestUrl, _cts.Token);

            if (manifest == null)
            {
                StatusText = "Failed to download manifest";
                return;
            }

            LatestVersion = manifest.ModpackVersion;

            if (!string.IsNullOrEmpty(manifest.ChangelogUrl))
            {
                try
                {
                    using var client = new HttpClient();
                    ChangelogText = await client.GetStringAsync(manifest.ChangelogUrl, _cts.Token);
                }
                catch { ChangelogText = "Could not load changelog"; }
            }

            var comparison = await _updateEngine.CompareFilesAsync(
                manifest, ValheimPath, HardcodedProfile.ProfileId, _cts.Token);

            if (comparison.FilesToDownload.Count == 0 && comparison.FilesToRemove.Count == 0)
            {
                StatusText = "Modpack is up to date!";
                AppendLog("All files are up to date");
            }
            else
            {
                StatusText = $"Update available: {comparison.FilesToDownload.Count} files to update, " +
                            $"{comparison.FilesToRemove.Count} to remove ({FormatSize(comparison.TotalDownloadSize)})";
                AppendLog(StatusText);
            }
        }
        catch (HttpRequestException ex)
        {
            SetError("Network error: Could not reach the manifest server. Check your internet connection.", ex);
        }
        catch (TaskCanceledException)
        {
            StatusText = "Check timed out or was cancelled";
            AppendLog("Check for updates timed out or was cancelled");
        }
        catch (System.Text.Json.JsonException ex)
        {
            SetError("The manifest file is invalid or corrupt. Contact the server owner.", ex);
        }
        catch (Exception ex)
        {
            SetError($"Unexpected error: {ex.Message}", ex);
            _logger.LogError(ex, "Check for updates failed");
        }
        finally
        {
            IsWorking = false;
            IsIndeterminate = false;
        }
    }

    private bool IsValheimPathSet() =>
        !string.IsNullOrWhiteSpace(ValheimPath)
        && !ValheimPath.StartsWith("Not detected")
        && System.IO.Directory.Exists(ValheimPath);

    [RelayCommand(CanExecute = nameof(CanExecuteAction))]
    private async Task UpdateAsync()
    {
        if (!IsValheimPathSet())
        {
            StatusText = "Valheim path not set. Open Settings (⚙) and set or detect your Valheim install folder.";
            AppendLog("ERROR: Valheim path is not configured. Go to Settings and set the Valheim install path.");
            return;
        }

        IsWorking = true;
        Progress = 0;

        try
        {
            _cts = new CancellationTokenSource();
            var manifest = await _manifestService.DownloadManifestAsync(
                HardcodedProfile.ManifestUrl, _cts.Token);

            if (manifest == null)
            {
                StatusText = "Failed to download manifest";
                return;
            }

            var comparison = await _updateEngine.CompareFilesAsync(
                manifest, ValheimPath, HardcodedProfile.ProfileId, _cts.Token);

            var result = await _updateEngine.ExecuteUpdateAsync(
                manifest, comparison, ValheimPath, HardcodedProfile.ProfileId, _cts.Token);

            InstalledVersion = manifest.ModpackVersion;
            LatestVersion = manifest.ModpackVersion;

            AppendLog(result.Message);
            foreach (var err in result.Errors)
                AppendLog($"  ERROR: {err}");
            foreach (var warn in result.Warnings)
                AppendLog($"  WARNING: {warn}");
        }
        catch (OperationCanceledException)
        {
            StatusText = "Update cancelled";
            AppendLog("Update was cancelled by user");
        }
        catch (HttpRequestException ex)
        {
            SetError("Network error during update. Your previous modpack is preserved.", ex);
            _logger.LogError(ex, "Update failed - network error");
        }
        catch (IOException ex)
        {
            SetError($"File error: {ex.Message}. A file may be locked by another process (Valheim running?).", ex);
            _logger.LogError(ex, "Update failed - IO error");
        }
        catch (UnauthorizedAccessException ex)
        {
            SetError("Permission denied. Try running the launcher as administrator.", ex);
            _logger.LogError(ex, "Update failed - permission denied");
        }
        catch (Exception ex)
        {
            SetError($"Update failed: {ex.Message}", ex);
            _logger.LogError(ex, "Update failed");
        }
        finally
        {
            IsWorking = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecuteAction))]
    private async Task RepairAsync()
    {
        if (!IsValheimPathSet())
        {
            StatusText = "Valheim path not set. Open Settings (⚙) and set or detect your Valheim install folder.";
            AppendLog("ERROR: Valheim path is not configured. Go to Settings and set the Valheim install path.");
            return;
        }

        IsWorking = true;
        Progress = 0;

        try
        {
            _cts = new CancellationTokenSource();
            var manifest = await _manifestService.DownloadManifestAsync(
                HardcodedProfile.ManifestUrl, _cts.Token);

            if (manifest == null)
            {
                StatusText = "Failed to download manifest";
                return;
            }

            var result = await _updateEngine.RepairAsync(
                manifest, ValheimPath, HardcodedProfile.ProfileId, _cts.Token);

            AppendLog(result.Message);
        }
        catch (Exception ex)
        {
            StatusText = $"Repair failed: {ex.Message}";
            _logger.LogError(ex, "Repair failed");
            AppendLog($"ERROR: {ex.Message}");
        }
        finally
        {
            IsWorking = false;
        }
    }

    [RelayCommand]
    private void LaunchValheim()
    {
        AppendLog("Launching Valheim...");
        var result = _launchService.LaunchValheim(HardcodedProfile, AutoConnectEnabled, CopyPasswordBeforeLaunch);

        if (result.Success)
        {
            StatusText = $"Launched via {result.LaunchMethod}";
            AppendLog($"Launch command: {result.LaunchCommand}");
            if (result.PasswordCopied)
            {
                AppendLog("Password copied to clipboard");
                if (ClearClipboardAfterLaunch)
                    AppendLog($"Clipboard will be cleared in {_settingsService.Settings.ClearClipboardAfterSeconds}s");
            }
        }
        else
        {
            StatusText = $"Launch failed: {result.ErrorMessage}";
            AppendLog($"ERROR: {result.ErrorMessage}");
        }
    }

    [RelayCommand]
    private void CopyPassword()
    {
        var password = _passwordStorage.GetPassword(HardcodedProfile.ProfileId);
        if (password != null)
        {
            Clipboard.SetText(password);
            StatusText = "Password copied to clipboard";
            AppendLog("Password copied to clipboard");
        }
        else
        {
            StatusText = "No password saved for this profile";
        }
    }

    [RelayCommand]
    private void CancelOperation()
    {
        _cts?.Cancel();
        StatusText = "Cancelling...";
    }

    [RelayCommand]
    private void OpenValheimFolder()
    {
        if (Directory.Exists(ValheimPath))
        {
            System.Diagnostics.Process.Start("explorer.exe", ValheimPath);
        }
    }

    [RelayCommand]
    private void OpenLogs()
    {
        var logPath = _settingsService.GetLogPath();
        if (File.Exists(logPath))
        {
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{logPath}\"");
        }
        else
        {
            var dir = Path.GetDirectoryName(logPath);
            if (dir != null && Directory.Exists(dir))
                System.Diagnostics.Process.Start("explorer.exe", dir);
        }
    }

    [RelayCommand]
    private async Task UpdateLauncherAsync()
    {
        if (AvailableLauncherUpdate == null) return;

        IsWorking = true;
        StatusText = "Downloading launcher update...";
        AppendLog($"Updating launcher to v{AvailableLauncherUpdate.Version}...");

        var success = await _selfUpdateService.DownloadAndApplyUpdateAsync(AvailableLauncherUpdate);
        if (success)
        {
            AppendLog("Launcher update downloaded. Restarting to apply...");
            Application.Current.Shutdown();
        }
        else
        {
            StatusText = "Launcher update failed";
            AppendLog("ERROR: Launcher update failed - hash verification or download error");
            IsWorking = false;
        }
    }

    [RelayCommand]
    private void CopyErrorDetails()
    {
        if (!string.IsNullOrEmpty(LastErrorDetails))
        {
            Clipboard.SetText(LastErrorDetails);
            StatusText = "Error details copied to clipboard";
        }
    }

    [RelayCommand]
    private void CopyLog()
    {
        if (!string.IsNullOrEmpty(LogOutput))
        {
            Clipboard.SetText(LogOutput);
            StatusText = "Log copied to clipboard";
        }
    }

    private bool CanExecuteAction() => !IsWorking;

    private void SetError(string userMessage, Exception ex)
    {
        StatusText = userMessage;
        AppendLog($"ERROR: {userMessage}");
        LastErrorDetails = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {userMessage}\n" +
                          $"Exception: {ex.GetType().Name}\n" +
                          $"Message: {ex.Message}\n" +
                          $"Stack: {ex.StackTrace}";
    }

    private void AppendLog(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        _logBuilder.AppendLine($"[{timestamp}] {message}");

        // Trim log if it gets too long (keep last MaxLogLines)
        var text = _logBuilder.ToString();
        var lines = text.Split('\n');
        if (lines.Length > MaxLogLines)
        {
            _logBuilder.Clear();
            _logBuilder.Append(string.Join('\n', lines.Skip(lines.Length - MaxLogLines)));
        }

        LogOutput = _logBuilder.ToString();
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }
}
