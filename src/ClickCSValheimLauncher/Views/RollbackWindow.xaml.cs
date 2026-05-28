using System.Windows;
using ClickCSValheimLauncher.Helpers;
using ClickCSValheimLauncher.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ClickCSValheimLauncher.Views;

public partial class RollbackWindow : Window
{
    private readonly FileBackupHelper _backupHelper;
    private readonly ManifestService _manifestService;
    private const string ProfileId = "clickcs";
    private readonly string _valheimPath;

    public RollbackWindow(string valheimPath)
    {
        InitializeComponent();

        _backupHelper = App.Services.GetRequiredService<FileBackupHelper>();
        _manifestService = App.Services.GetRequiredService<ManifestService>();
        _valheimPath = valheimPath;

        LoadBackups();
    }

    private void LoadBackups()
    {
        if (string.IsNullOrEmpty(_valheimPath))
        {
            BackupList.Items.Add("Valheim path not set");
            return;
        }

        var backups = _backupHelper.GetAvailableBackupsDetailed(_valheimPath, ProfileId);
        if (backups.Count == 0)
        {
            BackupList.Items.Add("No backups available");
            return;
        }

        foreach (var backup in backups)
        {
            var sizeStr = backup.TotalSizeBytes < 1024 * 1024
                ? $"{backup.TotalSizeBytes / 1024.0:F1} KB"
                : $"{backup.TotalSizeBytes / (1024.0 * 1024.0):F1} MB";

            BackupList.Items.Add(new BackupItem
            {
                DisplayName = $"{backup.DisplayName} ({backup.FileCount} files, {sizeStr})",
                FullPath = backup.FullPath,
                FileCount = backup.FileCount
            });
        }
    }

    private async void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (BackupList.SelectedItem is not BackupItem selected)
        {
            MessageBox.Show("Please select a backup to restore.", "No Selection", MessageBoxButton.OK);
            return;
        }

        if (selected.FileCount == 0)
        {
            MessageBox.Show("Selected backup contains no files.", "Empty Backup", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"Restore backup from {selected.DisplayName}?\n\n" +
            "A pre-rollback backup of currently managed files will be created first.",
            "Confirm Rollback", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            // Create pre-rollback backup using MANAGED files only, not all files
            var managedState = _manifestService.LoadManagedFiles(ProfileId);
            var managedPaths = managedState?.Files.Select(f => f.RelativePath).ToList()
                              ?? new List<string>();

            if (managedPaths.Count > 0)
            {
                await _backupHelper.CreateBackupAsync(_valheimPath, ProfileId + "_pre_rollback", managedPaths);
            }

            // Restore selected backup
            var restoreResult = await _backupHelper.RestoreBackupAsync(selected.FullPath, _valheimPath);

            var message = $"Rollback complete!\n\n" +
                         $"Restored: {restoreResult.RestoredFiles.Count} files";
            if (restoreResult.SkippedFiles.Count > 0)
                message += $"\nSkipped (unsafe paths): {restoreResult.SkippedFiles.Count}";

            MessageBox.Show(message, "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Rollback failed: {ex.Message}\n\nYour pre-rollback backup was created and is available.",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private class BackupItem
    {
        public string DisplayName { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public int FileCount { get; set; }
        public override string ToString() => DisplayName;
    }
}
