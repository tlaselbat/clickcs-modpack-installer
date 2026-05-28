using System.Diagnostics;

namespace ClickCSValheimLauncher.UpdateHelper;

/// <summary>
/// Self-update helper. Called by the main launcher to replace its own executable.
/// Usage: UpdateHelper.exe "source_new_exe" "target_exe_path" "backup_path"
/// </summary>
class Program
{
    static async Task<int> Main(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("Usage: UpdateHelper.exe <source_new_exe> <target_exe_path> <backup_path>");
            return 1;
        }

        var sourceExe = args[0];
        var targetExe = args[1];
        var backupPath = args[2];

        Console.WriteLine("ClickCS Valheim Launcher - Update Helper");
        Console.WriteLine("========================================");
        Console.WriteLine($"Source:  {sourceExe}");
        Console.WriteLine($"Target:  {targetExe}");
        Console.WriteLine($"Backup:  {backupPath}");
        Console.WriteLine();

        try
        {
            // Wait for main process to exit
            Console.WriteLine("Waiting for launcher to close...");
            await WaitForProcessExitAsync(targetExe, TimeSpan.FromSeconds(30));

            // Backup current exe
            if (File.Exists(targetExe))
            {
                Console.WriteLine("Backing up current launcher...");
                File.Copy(targetExe, backupPath, overwrite: true);
            }

            // Replace with new version
            Console.WriteLine("Installing new version...");
            File.Copy(sourceExe, targetExe, overwrite: true);

            // Clean up temp file
            try { File.Delete(sourceExe); } catch { }

            Console.WriteLine("Update complete! Restarting launcher...");

            // Restart the launcher
            Process.Start(new ProcessStartInfo
            {
                FileName = targetExe,
                UseShellExecute = true
            });

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: Update failed - {ex.Message}");
            Console.WriteLine();

            // Try to restore backup
            if (File.Exists(backupPath) && !File.Exists(targetExe))
            {
                Console.WriteLine("Attempting to restore backup...");
                try
                {
                    File.Copy(backupPath, targetExe, overwrite: true);
                    Console.WriteLine("Backup restored successfully.");

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = targetExe,
                        UseShellExecute = true
                    });
                }
                catch (Exception restoreEx)
                {
                    Console.WriteLine($"ERROR: Could not restore backup - {restoreEx.Message}");
                }
            }

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
            return 1;
        }
    }

    static async Task WaitForProcessExitAsync(string exePath, TimeSpan timeout)
    {
        var exeName = Path.GetFileNameWithoutExtension(exePath);
        var deadline = DateTime.Now + timeout;

        while (DateTime.Now < deadline)
        {
            var processes = Process.GetProcessesByName(exeName);
            if (processes.Length == 0)
                return;

            foreach (var p in processes)
                p.Dispose();

            await Task.Delay(500);
        }

        throw new TimeoutException($"Process '{exeName}' did not exit within {timeout.TotalSeconds}s");
    }
}
