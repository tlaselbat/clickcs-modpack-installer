using System.Text.RegularExpressions;

namespace ClickCSValheimLauncher.Helpers;

public static class PathValidator
{
    private static readonly HashSet<string> DangerousFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    private static readonly Regex ValidRelativePathRegex = new(
        @"^[a-zA-Z0-9_\-./\\][a-zA-Z0-9_\-./\\ ()\[\]{}@#$%^&+=!~`,]+$",
        RegexOptions.Compiled);

    public const int MaxRelativePathLength = 260;
    public const long MaxFileSizeBytes = 2L * 1024 * 1024 * 1024; // 2 GB

    public static bool IsPathSafe(string relativePath, string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return false;

        // Reject null bytes
        if (relativePath.Contains('\0'))
            return false;

        // Reject overly long paths
        if (relativePath.Length > MaxRelativePathLength)
            return false;

        // Reject absolute paths
        if (Path.IsPathRooted(relativePath))
            return false;

        // Normalize separators for consistent checking
        var normalized = relativePath.Replace('\\', '/');

        // Reject path traversal
        if (normalized.Contains("..")
            || normalized.StartsWith("/")
            || normalized.StartsWith("~"))
            return false;

        // Reject alternate data streams (NTFS)
        if (normalized.Contains(':'))
            return false;

        // Reject invalid path/file characters
        if (ContainsInvalidChars(relativePath))
            return false;

        // Reject Windows reserved device names in any segment
        var segments = normalized.Split('/');
        foreach (var segment in segments)
        {
            if (string.IsNullOrWhiteSpace(segment))
                return false;

            var nameOnly = Path.GetFileNameWithoutExtension(segment);
            if (DangerousFileNames.Contains(nameOnly))
                return false;

            // Reject trailing dots/spaces (Windows silently strips them)
            if (segment.EndsWith('.') || segment.EndsWith(' '))
                return false;
        }

        // Final canonicalization check
        try
        {
            var fullPath = Path.GetFullPath(Path.Combine(baseDirectory, relativePath));
            var normalizedBase = Path.GetFullPath(baseDirectory);

            if (!normalizedBase.EndsWith(Path.DirectorySeparatorChar.ToString()))
                normalizedBase += Path.DirectorySeparatorChar;

            return fullPath.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static string? GetSafePath(string relativePath, string baseDirectory)
    {
        if (!IsPathSafe(relativePath, baseDirectory))
            return null;

        return Path.GetFullPath(Path.Combine(baseDirectory, relativePath));
    }

    public static bool ContainsInvalidChars(string path)
    {
        var invalidPathChars = Path.GetInvalidPathChars();
        if (path.IndexOfAny(invalidPathChars) >= 0)
            return true;

        // Also check individual filename segments for invalid filename chars
        var segments = path.Replace('\\', '/').Split('/');
        var invalidFileChars = Path.GetInvalidFileNameChars()
            .Where(c => c != '/' && c != '\\').ToArray();

        foreach (var segment in segments)
        {
            if (segment.IndexOfAny(invalidFileChars) >= 0)
                return true;
        }

        return false;
    }

    public static bool IsFileSizeAllowed(long sizeBytes)
    {
        return sizeBytes >= 0 && sizeBytes <= MaxFileSizeBytes;
    }

    public static bool IsValidSha256(string hash)
    {
        if (string.IsNullOrWhiteSpace(hash))
            return false;

        return hash.Length == 64 && Regex.IsMatch(hash, "^[a-fA-F0-9]{64}$");
    }

    public static bool IsValidUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
    }
}
