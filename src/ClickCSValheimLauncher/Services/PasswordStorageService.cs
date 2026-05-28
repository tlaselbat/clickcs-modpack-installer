using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace ClickCSValheimLauncher.Services;

public class PasswordStorageService
{
    private const string CredentialPrefix = "ClickCSValheimLauncher_";
    private readonly ILogger<PasswordStorageService> _logger;

    public PasswordStorageService(ILogger<PasswordStorageService> logger)
    {
        _logger = logger;
    }

    public bool SavePassword(string profileId, string password)
    {
        try
        {
            if (TrySaveToCredentialManager(profileId, password))
                return true;

            return SaveWithDpapi(profileId, password);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save password for profile {ProfileId}", profileId);
            return false;
        }
    }

    public string? GetPassword(string profileId)
    {
        try
        {
            var password = TryGetFromCredentialManager(profileId);
            if (password != null)
                return password;

            return GetWithDpapi(profileId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve password for profile {ProfileId}", profileId);
            return null;
        }
    }

    public bool DeletePassword(string profileId)
    {
        try
        {
            var credName = CredentialPrefix + profileId;
            CredDelete(credName, 1, 0);

            var dpapiFile = GetDpapiFilePath(profileId);
            if (File.Exists(dpapiFile))
                File.Delete(dpapiFile);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete password for profile {ProfileId}", profileId);
            return false;
        }
    }

    private bool TrySaveToCredentialManager(string profileId, string password)
    {
        var credName = CredentialPrefix + profileId;
        var passwordBytes = Encoding.Unicode.GetBytes(password);

        var credential = new CREDENTIAL
        {
            Type = 1, // CRED_TYPE_GENERIC
            TargetName = credName,
            CredentialBlobSize = (uint)passwordBytes.Length,
            CredentialBlob = Marshal.AllocHGlobal(passwordBytes.Length),
            Persist = 2, // CRED_PERSIST_LOCAL_MACHINE
            UserName = "ClickCSValheimLauncher"
        };

        try
        {
            Marshal.Copy(passwordBytes, 0, credential.CredentialBlob, passwordBytes.Length);
            var result = CredWrite(ref credential, 0);
            if (result)
                _logger.LogDebug("Password saved to Credential Manager for profile {ProfileId}", profileId);
            return result;
        }
        finally
        {
            Marshal.FreeHGlobal(credential.CredentialBlob);
        }
    }

    private string? TryGetFromCredentialManager(string profileId)
    {
        var credName = CredentialPrefix + profileId;
        if (!CredRead(credName, 1, 0, out var credPtr))
            return null;

        try
        {
            var cred = Marshal.PtrToStructure<CREDENTIAL>(credPtr);
            if (cred.CredentialBlobSize == 0 || cred.CredentialBlob == IntPtr.Zero)
                return null;

            var passwordBytes = new byte[cred.CredentialBlobSize];
            Marshal.Copy(cred.CredentialBlob, passwordBytes, 0, (int)cred.CredentialBlobSize);
            return Encoding.Unicode.GetString(passwordBytes);
        }
        finally
        {
            CredFree(credPtr);
        }
    }

    private bool SaveWithDpapi(string profileId, string password)
    {
        var plainBytes = Encoding.UTF8.GetBytes(password);
        var encrypted = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
        var filePath = GetDpapiFilePath(profileId);

        var dir = Path.GetDirectoryName(filePath);
        if (dir != null)
            Directory.CreateDirectory(dir);

        File.WriteAllBytes(filePath, encrypted);
        _logger.LogDebug("Password saved with DPAPI for profile {ProfileId}", profileId);
        return true;
    }

    private string? GetWithDpapi(string profileId)
    {
        var filePath = GetDpapiFilePath(profileId);
        if (!File.Exists(filePath))
            return null;

        var encrypted = File.ReadAllBytes(filePath);
        var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(decrypted);
    }

    private static string GetDpapiFilePath(string profileId)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "ClickCS Valheim Launcher", "credentials", $"{profileId}.dat");
    }

    #region Windows Credential Manager P/Invoke

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredWrite(ref CREDENTIAL credential, uint flags);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredDelete(string target, uint type, uint flags);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern void CredFree(IntPtr credential);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIAL
    {
        public uint Flags;
        public uint Type;
        public string TargetName;
        public string Comment;
        public long LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string TargetAlias;
        public string UserName;
    }

    #endregion
}
