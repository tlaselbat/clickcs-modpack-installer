using ClickCSValheimLauncher.Helpers;
using Xunit;

namespace ClickCSValheimLauncher.Tests;

public class HashHelperTests
{
    [Fact]
    public async Task ComputeSha256_ProducesConsistentHash()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "hello world");
            var hash1 = await HashHelper.ComputeSha256Async(tempFile);
            var hash2 = await HashHelper.ComputeSha256Async(tempFile);

            Assert.Equal(hash1, hash2);
            Assert.Equal(64, hash1.Length);
            Assert.Matches("^[a-f0-9]{64}$", hash1);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ComputeSha256_DifferentContent_DifferentHash()
    {
        var file1 = Path.GetTempFileName();
        var file2 = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file1, "content A");
            await File.WriteAllTextAsync(file2, "content B");
            var hash1 = await HashHelper.ComputeSha256Async(file1);
            var hash2 = await HashHelper.ComputeSha256Async(file2);

            Assert.NotEqual(hash1, hash2);
        }
        finally
        {
            File.Delete(file1);
            File.Delete(file2);
        }
    }

    [Fact]
    public void VerifyHash_CaseInsensitive()
    {
        Assert.True(HashHelper.VerifyHash(
            "abc123def456abc123def456abc123def456abc123def456abc123def456abcd",
            "ABC123DEF456ABC123DEF456ABC123DEF456ABC123DEF456ABC123DEF456ABCD"));
    }

    [Fact]
    public void VerifyHash_Mismatch_ReturnsFalse()
    {
        Assert.False(HashHelper.VerifyHash(
            "abc123def456abc123def456abc123def456abc123def456abc123def456abcd",
            "fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffe"));
    }

    [Fact]
    public async Task ComputeSha256_FromStream_MatchesFile()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "stream test content");
            var fileHash = await HashHelper.ComputeSha256Async(tempFile);

            using var stream = File.OpenRead(tempFile);
            var streamHash = await HashHelper.ComputeSha256Async(stream);

            Assert.Equal(fileHash, streamHash);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
