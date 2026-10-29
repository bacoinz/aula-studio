using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aula.Protocol;

namespace AulaStudio.Services;

/// <summary>User preferences, stored in %APPDATA%\AulaStudio\settings.json.</summary>
public sealed class AppSettings
{
    public static string Folder { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AulaStudio");

    private static string FilePath => Path.Combine(Folder, "settings.json");

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public string Language { get; set; } = "pt-PT";
    public bool AutoSyncClock { get; set; } = true;
    public bool CloseToTray { get; set; } = true;

    /// <summary>The keyboard can't report its state, so the app remembers what it last sent.</summary>
    public LightingSettings Lighting { get; set; } = new();

    /// <summary>Per-key colours by light index, as #RRGGBB.</summary>
    public Dictionary<int, string> PerKeyColors { get; set; } = [];
    public int PerKeyBrightness { get; set; } = LightingSettings.MaxBrightness;

    public static AppSettings Load()
    {
        try
        {
            return File.Exists(FilePath)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Json) ?? new()
                : new();
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            return new();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(Folder);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, Json));
        File.Move(temp, FilePath, overwrite: true);
    }
}
