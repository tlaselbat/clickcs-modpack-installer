using ClickCSValheimLauncher.Models;
using ClickCSValheimLauncher.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ClickCSValheimLauncher.Tests;

public class ManifestValidationTests
{
    private readonly ManifestService _svc;

    public ManifestValidationTests()
    {
        var logger = new Mock<ILogger<ManifestService>>();
        var httpClient = new HttpClient();
        var settingsLogger = new Mock<ILogger<SettingsService>>();
        var settingsService = new SettingsService(settingsLogger.Object);
        _svc = new ManifestService(logger.Object, httpClient, settingsService);
    }

    private static ModpackManifest MakeValidManifest()
    {
        return new ModpackManifest
        {
            SchemaVersion = 1,
            ModpackId = "test-pack",
            ModpackName = "Test Pack",
            ModpackVersion = "1.0.0",
            Files = new List<ManifestFile>
            {
                new()
                {
                    RelativePath = "BepInEx/plugins/MyMod.dll",
                    Sha256 = "abc123def456abc123def456abc123def456abc123def456abc123def456abcd",
                    SizeBytes = 1024,
                    DownloadUrl = "https://example.com/mods/MyMod.dll"
                }
            }
        };
    }

    [Fact]
    public void ValidManifest_Passes()
    {
        var manifest = MakeValidManifest();
        Assert.True(_svc.ValidateManifest(manifest));
    }

    [Fact]
    public void MissingModpackId_Fails()
    {
        var m = MakeValidManifest();
        m.ModpackId = "";
        Assert.False(_svc.ValidateManifest(m));
    }

    [Fact]
    public void MissingModpackVersion_Fails()
    {
        var m = MakeValidManifest();
        m.ModpackVersion = "";
        Assert.False(_svc.ValidateManifest(m));
    }

    [Fact]
    public void WrongSchemaVersion_Fails()
    {
        var m = MakeValidManifest();
        m.SchemaVersion = 99;
        Assert.False(_svc.ValidateManifest(m));
    }

    [Fact]
    public void OverlongModpackId_Fails()
    {
        var m = MakeValidManifest();
        m.ModpackId = new string('x', 129);
        Assert.False(_svc.ValidateManifest(m));
    }

    [Theory]
    [InlineData("../evil.dll")]
    [InlineData("C:/Windows/System32/test.dll")]
    [InlineData("/absolute/path/file.dll")]
    [InlineData("BepInEx/plugins/../../evil.dll")]
    public void UnsafeFilePaths_AreRejected(string path)
    {
        var m = MakeValidManifest();
        m.Files[0].RelativePath = path;
        Assert.False(_svc.ValidateManifest(m));
    }

    [Fact]
    public void DuplicateRelativePaths_AreRejected()
    {
        var m = MakeValidManifest();
        m.Files.Add(new ManifestFile
        {
            RelativePath = "BepInEx/plugins/MyMod.dll",
            Sha256 = "abc123def456abc123def456abc123def456abc123def456abc123def456abcd",
            SizeBytes = 1024,
            DownloadUrl = "https://example.com/mods/MyMod.dll"
        });
        Assert.False(_svc.ValidateManifest(m));
    }

    [Fact]
    public void DuplicatePathsDifferentSeparators_AreRejected()
    {
        var m = MakeValidManifest();
        m.Files.Add(new ManifestFile
        {
            RelativePath = "BepInEx\\plugins\\MyMod.dll",
            Sha256 = "abc123def456abc123def456abc123def456abc123def456abc123def456abcd",
            SizeBytes = 1024,
            DownloadUrl = "https://example.com/mods/MyMod2.dll"
        });
        Assert.False(_svc.ValidateManifest(m));
    }

    [Fact]
    public void InvalidSha256Format_Fails()
    {
        var m = MakeValidManifest();
        m.Files[0].Sha256 = "not-a-valid-hash";
        Assert.False(_svc.ValidateManifest(m));
    }

    [Fact]
    public void InvalidDownloadUrl_Fails()
    {
        var m = MakeValidManifest();
        m.Files[0].DownloadUrl = "ftp://example.com/file.dll";
        Assert.False(_svc.ValidateManifest(m));
    }

    [Fact]
    public void NegativeFileSize_Fails()
    {
        var m = MakeValidManifest();
        m.Files[0].SizeBytes = -1;
        Assert.False(_svc.ValidateManifest(m));
    }

    [Fact]
    public void OversizedFile_Fails()
    {
        var m = MakeValidManifest();
        m.Files[0].SizeBytes = 3L * 1024 * 1024 * 1024; // 3 GB
        Assert.False(_svc.ValidateManifest(m));
    }

    [Fact]
    public void NtfsAdsInFilePath_Fails()
    {
        var m = MakeValidManifest();
        m.Files[0].RelativePath = "BepInEx/plugins/mod.dll:hidden";
        Assert.False(_svc.ValidateManifest(m));
    }

    [Fact]
    public void UnsafeRemovalPath_Fails()
    {
        var m = MakeValidManifest();
        m.Removals.Add(new ManifestRemoval
        {
            RelativePath = "../../../evil.dll",
            Reason = "test"
        });
        Assert.False(_svc.ValidateManifest(m));
    }

    [Fact]
    public void NtfsAdsInRemovalPath_Fails()
    {
        var m = MakeValidManifest();
        m.Removals.Add(new ManifestRemoval
        {
            RelativePath = "file.dll:Zone.Identifier",
            Reason = "test"
        });
        Assert.False(_svc.ValidateManifest(m));
    }

    [Fact]
    public void InvalidChangelogUrl_Fails()
    {
        var m = MakeValidManifest();
        m.ChangelogUrl = "not-a-url";
        Assert.False(_svc.ValidateManifest(m));
    }

    [Fact]
    public void ValidChangelogUrl_Passes()
    {
        var m = MakeValidManifest();
        m.ChangelogUrl = "https://example.com/changelog.md";
        Assert.True(_svc.ValidateManifest(m));
    }
}
