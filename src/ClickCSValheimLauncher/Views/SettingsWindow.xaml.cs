using System.Windows;
using ClickCSValheimLauncher.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace ClickCSValheimLauncher.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly SteamDetectorService _steamDetector;

    public SettingsWindow()
    {
        InitializeComponent();

        _settingsService = App.Services.GetRequiredService<SettingsService>();
        _steamDetector = App.Services.GetRequiredService<SteamDetectorService>();

        LoadSettings();
    }

    private void LoadSettings()
    {
        ValheimPathBox.Text = _settingsService.Settings.ValheimPath ?? string.Empty;
        SteamPathBox.Text = _settingsService.Settings.SteamPath ?? string.Empty;
        UpdateUrlBox.Text = _settingsService.Settings.LauncherUpdateUrl;
        CheckUpdatesBox.IsChecked = _settingsService.Settings.CheckUpdatesOnStartup;
        AllowHttpBox.IsChecked = _settingsService.Settings.AllowInsecureHttp;
        ClipboardSecondsBox.Text = _settingsService.Settings.ClearClipboardAfterSeconds.ToString();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _settingsService.Settings.ValheimPath = string.IsNullOrWhiteSpace(ValheimPathBox.Text) ? null : ValheimPathBox.Text;
        _settingsService.Settings.SteamPath = string.IsNullOrWhiteSpace(SteamPathBox.Text) ? null : SteamPathBox.Text;
        _settingsService.Settings.LauncherUpdateUrl = UpdateUrlBox.Text;
        _settingsService.Settings.CheckUpdatesOnStartup = CheckUpdatesBox.IsChecked ?? true;
        _settingsService.Settings.AllowInsecureHttp = AllowHttpBox.IsChecked ?? false;

        if (int.TryParse(ClipboardSecondsBox.Text, out var seconds))
            _settingsService.Settings.ClearClipboardAfterSeconds = seconds;

        _settingsService.Save();
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void BrowseValheim_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Valheim Install Folder"
        };

        if (dialog.ShowDialog() == true)
        {
            ValheimPathBox.Text = dialog.FolderName;
        }
    }

    private void BrowseSteam_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Steam Install Folder"
        };

        if (dialog.ShowDialog() == true)
        {
            SteamPathBox.Text = dialog.FolderName;
        }
    }

    private void DetectValheim_Click(object sender, RoutedEventArgs e)
    {
        var path = _steamDetector.DetectValheimPath();
        if (path != null)
        {
            ValheimPathBox.Text = path;
            MessageBox.Show($"Valheim detected at:\n{path}", "Detection Successful", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show("Could not auto-detect Valheim installation.\nPlease browse to the folder manually.",
                "Detection Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
