using ClickCSValheimLauncher.Helpers;
using Xunit;

namespace ClickCSValheimLauncher.Tests;

public class PathValidatorTests
{
    private readonly string _baseDir;

    public PathValidatorTests()
    {
        _baseDir = Path.Combine(Path.GetTempPath(), "PathValidatorTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_baseDir);
    }

    [Theory]
    [InlineData("BepInEx/plugins/MyMod.dll")]
    [InlineData("BepInEx/config/MyMod.cfg")]
    [InlineData("doorstop_config.ini")]
    [InlineData("winhttp.dll")]
    public void SafePaths_AreAccepted(string path)
    {
        Assert.True(PathValidator.IsPathSafe(path, _baseDir));
        Assert.NotNull(PathValidator.GetSafePath(path, _baseDir));
    }

    [Theory]
    [InlineData("../evil.dll")]
    [InlineData("..\\evil.dll")]
    [InlineData("BepInEx/plugins/../../evil.dll")]
    [InlineData("BepInEx\\plugins\\..\\..\\evil.dll")]
    public void PathTraversal_IsRejected(string path)
    {
        Assert.False(PathValidator.IsPathSafe(path, _baseDir));
        Assert.Null(PathValidator.GetSafePath(path, _baseDir));
    }

    [Theory]
    [InlineData("C:/Windows/System32/test.dll")]
    [InlineData("C:\\Windows\\System32\\test.dll")]
    [InlineData("/absolute/path/file.dll")]
    [InlineData("D:\\evil.dll")]
    public void AbsolutePaths_AreRejected(string path)
    {
        Assert.False(PathValidator.IsPathSafe(path, _baseDir));
        Assert.Null(PathValidator.GetSafePath(path, _baseDir));
    }

    [Theory]
    [InlineData("BepInEx/plugins/mod.dll:Zone.Identifier")]
    [InlineData("file.txt:hidden_stream")]
    public void NtfsAlternateDataStreams_AreRejected(string path)
    {
        Assert.False(PathValidator.IsPathSafe(path, _baseDir));
    }

    [Theory]
    [InlineData("CON")]
    [InlineData("PRN")]
    [InlineData("AUX")]
    [InlineData("NUL")]
    [InlineData("COM1")]
    [InlineData("LPT1")]
    [InlineData("BepInEx/CON.dll")]
    [InlineData("BepInEx/NUL.txt")]
    public void WindowsReservedNames_AreRejected(string path)
    {
        Assert.False(PathValidator.IsPathSafe(path, _baseDir));
    }

    [Theory]
    [InlineData("file.dll ")]
    [InlineData("file.dll.")]
    [InlineData("BepInEx/plugins/mod. ")]
    public void TrailingDotsAndSpaces_AreRejected(string path)
    {
        Assert.False(PathValidator.IsPathSafe(path, _baseDir));
    }

    [Fact]
    public void NullByte_IsRejected()
    {
        Assert.False(PathValidator.IsPathSafe("evil\0.dll", _baseDir));
    }

    [Fact]
    public void EmptyAndWhitespace_AreRejected()
    {
        Assert.False(PathValidator.IsPathSafe("", _baseDir));
        Assert.False(PathValidator.IsPathSafe("   ", _baseDir));
        Assert.False(PathValidator.IsPathSafe(null!, _baseDir));
    }

    [Fact]
    public void OverlongPath_IsRejected()
    {
        var longPath = "BepInEx/" + new string('a', 300) + ".dll";
        Assert.False(PathValidator.IsPathSafe(longPath, _baseDir));
    }

    [Fact]
    public void TildePath_IsRejected()
    {
        Assert.False(PathValidator.IsPathSafe("~/evil.dll", _baseDir));
    }

    [Theory]
    [InlineData("abc123def456abc123def456abc123def456abc123def456abc123def456abcd", true)]
    [InlineData("ABC123DEF456ABC123DEF456ABC123DEF456ABC123DEF456ABC123DEF456ABCD", true)]
    [InlineData("abc123", false)]
    [InlineData("", false)]
    [InlineData("xyz_not_hex_abc123def456abc123def456abc123def456abc123def456abcde", false)]
    [InlineData("abc123def456abc123def456abc123def456abc123def456abc123def456abcX", false)]
    public void IsValidSha256_ValidatesCorrectly(string hash, bool expected)
    {
        Assert.Equal(expected, PathValidator.IsValidSha256(hash));
    }

    [Theory]
    [InlineData("https://example.com/file.dll", true)]
    [InlineData("http://192.168.1.1/file.dll", true)]
    [InlineData("ftp://example.com/file.dll", false)]
    [InlineData("file:///etc/passwd", false)]
    [InlineData("", false)]
    [InlineData("not-a-url", false)]
    public void IsValidUrl_ValidatesCorrectly(string url, bool expected)
    {
        Assert.Equal(expected, PathValidator.IsValidUrl(url));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1024, true)]
    [InlineData(2147483648L, true)] // 2 GB
    [InlineData(2147483649L, false)] // > 2 GB
    [InlineData(-1, false)]
    public void IsFileSizeAllowed_ValidatesCorrectly(long size, bool expected)
    {
        Assert.Equal(expected, PathValidator.IsFileSizeAllowed(size));
    }
}
