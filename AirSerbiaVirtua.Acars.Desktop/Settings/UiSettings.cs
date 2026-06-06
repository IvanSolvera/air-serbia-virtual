using System.IO;
using System.Text.Json;

namespace AirSerbiaVirtua.Acars.Desktop.Settings;

/// <summary>
/// Small persisted UI preferences file (%LOCALAPPDATA%/AirSerbiaVirtua/ui-settings.json).
/// Holds the "Remember me" callsign and the login toggles — never the password.
/// Auto Login is stored but only takes effect once refresh-token persistence
/// (DPAPI) lands; until then it simply survives restarts as a preference.
/// </summary>
public sealed class UiSettings
{
    public string? SavedCallsign { get; set; }
    public bool RememberMe { get; set; } = true;
    public bool AutoLogin { get; set; }

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AirSerbiaVirtua", "ui-settings.json");

    public static UiSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<UiSettings>(File.ReadAllText(FilePath)) ?? new UiSettings();
        }
        catch
        {
            // Corrupt settings are not worth crashing the client over — start fresh.
        }
        return new UiSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Json));
        }
        catch
        {
            // Best-effort: a read-only disk must not break login.
        }
    }
}
