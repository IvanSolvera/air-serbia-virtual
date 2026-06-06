using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace AirSerbiaVirtua.Acars.Desktop.Settings;

/// <summary>
/// Persists the refresh token for Auto Login, encrypted with Windows DPAPI
/// (CurrentUser scope) so only this Windows account on this machine can read it.
/// The access token is never persisted — it is short-lived and re-issued on
/// restore. File: %LOCALAPPDATA%/AirSerbiaVirtua/session.token
/// </summary>
public static class SessionTokenStore
{
    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AirSerbiaVirtua", "session.token");

    public static void Save(string refreshToken)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var protectedBytes = ProtectedData.Protect(
                Encoding.UTF8.GetBytes(refreshToken),
                optionalEntropy: null,
                DataProtectionScope.CurrentUser);
            File.WriteAllBytes(FilePath, protectedBytes);
        }
        catch
        {
            // Persisting the session is best-effort; login itself already succeeded.
        }
    }

    public static string? Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return null;
            var raw = ProtectedData.Unprotect(
                File.ReadAllBytes(FilePath),
                optionalEntropy: null,
                DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(raw);
        }
        catch
        {
            // Corrupt / foreign-profile blob — treat as no stored session.
            return null;
        }
    }

    public static void Delete()
    {
        try
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
        }
        catch
        {
            // Best-effort.
        }
    }
}
