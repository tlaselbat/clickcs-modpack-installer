using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace ClickCSValheimLauncher.Services;

public class SteamDetectorService
{
    private const int ValheimAppId = 892970;
    private readonly ILogger<SteamDetectorService> _logger;

    public SteamDetectorService(ILogger<SteamDetectorService> logger)
    {
        _logger = logger;
    }

    public string? DetectSteamPath()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam")
                         ?? Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam");

            var path = key?.GetValue("InstallPath") as string;
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                _logger.LogInformation("Steam found at: {Path}", path);
                return path;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to detect Steam from registry");
        }

        var defaultPaths = new[]
        {
            @"C:\Program Files (x86)\Steam",
            @"C:\Program Files\Steam",
            @"D:\Steam",
            @"D:\Program Files (x86)\Steam"
        };

        foreach (var p in defaultPaths)
        {
            if (Directory.Exists(p))
            {
                _logger.LogInformation("Steam found at default path: {Path}", p);
                return p;
            }
        }

        _logger.LogWarning("Steam installation not found");
        return null;
    }

    public string? DetectValheimPath(string? steamPath = null)
    {
        steamPath ??= DetectSteamPath();
        if (steamPath == null)
            return null;

        var libraryFolders = GetLibraryFolders(steamPath);
        foreach (var folder in libraryFolders)
        {
            var valheimPath = FindValheimInLibrary(folder);
            if (valheimPath != null)
                return valheimPath;
        }

        _logger.LogWarning("Valheim installation not found in any Steam library");
        return null;
    }

    private List<string> GetLibraryFolders(string steamPath)
    {
        var folders = new List<string> { steamPath };
        var vdfPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");

        if (!File.Exists(vdfPath))
        {
            _logger.LogWarning("libraryfolders.vdf not found at {Path}", vdfPath);
            return folders;
        }

        try
        {
            var content = File.ReadAllText(vdfPath);
            var lines = content.Split('\n');

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("\"path\""))
                {
                    var parts = trimmed.Split('"');
                    if (parts.Length >= 4)
                    {
                        var path = parts[3].Replace("\\\\", "\\");
                        if (Directory.Exists(path))
                        {
                            folders.Add(path);
                            _logger.LogDebug("Found Steam library: {Path}", path);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse libraryfolders.vdf");
        }

        return folders;
    }

    private string? FindValheimInLibrary(string libraryPath)
    {
        var acfPath = Path.Combine(libraryPath, "steamapps", $"appmanifest_{ValheimAppId}.acf");
        if (!File.Exists(acfPath))
            return null;

        try
        {
            var content = File.ReadAllText(acfPath);
            var lines = content.Split('\n');

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("\"installdir\""))
                {
                    var parts = trimmed.Split('"');
                    if (parts.Length >= 4)
                    {
                        var installDir = parts[3];
                        var fullPath = Path.Combine(libraryPath, "steamapps", "common", installDir);
                        if (Directory.Exists(fullPath))
                        {
                            _logger.LogInformation("Valheim found at: {Path}", fullPath);
                            return fullPath;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse appmanifest for Valheim");
        }

        return null;
    }

    public string? GetSteamExePath(string? steamPath = null)
    {
        steamPath ??= DetectSteamPath();
        if (steamPath == null)
            return null;

        var exePath = Path.Combine(steamPath, "steam.exe");
        return File.Exists(exePath) ? exePath : null;
    }
}
