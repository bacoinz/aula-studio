using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aula.Protocol;

namespace AulaStudio.Services;

/// <summary>
/// User preferences, stored in settings.json. A portable copy (portable.txt next to the exe) keeps its
/// data in a "data" folder beside the exe; otherwise it lives in %APPDATA%\AulaStudio.
/// </summary>
public sealed class AppSettings
{
    private static readonly string UserFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AulaStudio");

    public static bool IsPortable { get; } = File.Exists(Path.Combine(AppContext.BaseDirectory, "portable.txt"));

    public static string Folder { get; } = IsPortable ? Path.Combine(AppContext.BaseDirectory, "data") : UserFolder;

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

    /// <summary>Sleep, debounce, FN mode and key locks, as last sent.</summary>
    public DeviceOptions Device { get; set; } = new();

    /// <summary>Per-key colours by light index, as #RRGGBB.</summary>
    public Dictionary<int, string> PerKeyColors { get; set; } = [];
    public int PerKeyBrightness { get; set; } = LightingSettings.MaxBrightness;

    /// <summary>Whether the per-key colours (rather than the effect) were sent last.</summary>
    public bool PerKeyActive { get; set; }

    /// <summary>Key remaps by key index, as the 4-byte action packed into a uint. Missing keys keep their default.</summary>
    public Dictionary<int, uint> KeyMap { get; set; } = [];
    public Dictionary<int, uint> FnKeyMap { get; set; } = [];

    /// <summary>Macros in slot order; a key's macro action refers to the slot number.</summary>
    public List<MacroDefinition> Macros { get; set; } = [];

    public List<Profile> Profiles { get; set; } = [];

    public MusicSettings Music { get; set; } = new();

    public Dictionary<int, uint> KeyMapFor(KeyLayer layer) => layer == KeyLayer.Fn ? FnKeyMap : KeyMap;

    public static AppSettings Load()
    {
        var firstPortableRun = IsPortable && !File.Exists(FilePath);
        if (firstPortableRun) ImportUserFolder();
        var settings = Read();
        // A portable copy is opened now and then; closing it should really close it.
        if (firstPortableRun)
        {
            settings.CloseToTray = false;
            try
            {
                settings.Save();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // A read-only folder: the app still runs, and just asks again next time.
            }
        }
        return settings;
    }

    private static AppSettings Read()
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

    /// <summary>Copies what an earlier non-portable run saved, since the keyboard can't report it back.</summary>
    private static void ImportUserFolder()
    {
        try
        {
            if (!Directory.Exists(UserFolder)) return;
            var library = Path.Combine(UserFolder, "library");
            var files = Directory.Exists(library)
                ? Directory.GetFiles(library, "*", SearchOption.AllDirectories)
                : [];
            foreach (var source in files)
            {
                var target = Path.Combine(Folder, Path.GetRelativePath(UserFolder, source));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(source, target, overwrite: true);
            }
            // Last, so an interrupted copy is retried on the next run.
            var settings = Path.Combine(UserFolder, "settings.json");
            if (!File.Exists(settings)) return;
            Directory.CreateDirectory(Folder);
            File.Copy(settings, FilePath, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Nothing to lose: the app starts with default settings.
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

public sealed class MacroDefinition
{
    public string Name { get; set; } = "";
    public List<MacroEvent> Events { get; set; } = [];
}
