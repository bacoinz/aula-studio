using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aula.Protocol;

namespace AulaStudio.Services;

/// <summary>A saved keyboard setup: lighting, key remaps, macros and device options.</summary>
public sealed class Profile
{
    public string Name { get; set; } = "";
    public DateTime Saved { get; set; } = DateTime.Now;

    public LightingSettings Lighting { get; set; } = new();
    public bool PerKeyActive { get; set; }
    public Dictionary<int, string> PerKeyColors { get; set; } = [];
    public int PerKeyBrightness { get; set; } = LightingSettings.MaxBrightness;
    public Dictionary<int, uint> KeyMap { get; set; } = [];
    public Dictionary<int, uint> FnKeyMap { get; set; } = [];
    public List<MacroDefinition> Macros { get; set; } = [];
    public DeviceOptions Device { get; set; } = new();

    public const string FileExtension = ".aulaprofile";

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Copies the current setup. Collections are cloned through JSON so later edits don't leak in.</summary>
    public static Profile Capture(AppSettings s, string name) => Clone(new Profile
    {
        Name = name,
        Lighting = s.Lighting,
        PerKeyActive = s.PerKeyActive,
        PerKeyColors = s.PerKeyColors,
        PerKeyBrightness = s.PerKeyBrightness,
        KeyMap = s.KeyMap,
        FnKeyMap = s.FnKeyMap,
        Macros = s.Macros,
        Device = s.Device,
    });

    /// <summary>Makes this profile the current setup (on the PC side only; see <see cref="Send"/>).</summary>
    public void ApplyTo(AppSettings s)
    {
        var copy = Clone(this);
        s.Lighting = copy.Lighting;
        s.PerKeyActive = copy.PerKeyActive;
        s.PerKeyColors = copy.PerKeyColors;
        s.PerKeyBrightness = copy.PerKeyBrightness;
        s.KeyMap = copy.KeyMap;
        s.FnKeyMap = copy.FnKeyMap;
        s.Macros = copy.Macros;
        s.Device = copy.Device;
    }

    /// <summary>Sends the whole setup: options, macros, both key layers, then the lighting.</summary>
    public void Send(IAulaKeyboard kb)
    {
        kb.SetOptions(Device);
        kb.SetMacros(Macros.Select(m => (IReadOnlyList<MacroEvent>)m.Events).ToList());
        kb.SetKeyMap(KeyLayer.Normal, KeyMap.ToDictionary(kv => kv.Key, kv => KeyAction.FromUInt32(kv.Value)));
        kb.SetKeyMap(KeyLayer.Fn, FnKeyMap.ToDictionary(kv => kv.Key, kv => KeyAction.FromUInt32(kv.Value)));
        if (PerKeyActive)
        {
            var colors = new Dictionary<int, (byte, byte, byte)>();
            foreach (var (index, hex) in PerKeyColors)
                if (hex.Length == 7 && hex[0] == '#')
                    colors[index] = (Convert.ToByte(hex[1..3], 16), Convert.ToByte(hex[3..5], 16), Convert.ToByte(hex[5..7], 16));
            kb.SetPerKeyColors(colors, PerKeyBrightness);
        }
        else
        {
            kb.SetLighting(Lighting);
        }
    }

    public void Export(string path) => File.WriteAllText(path, JsonSerializer.Serialize(this, Json));

    public static Profile Import(string path) =>
        JsonSerializer.Deserialize<Profile>(File.ReadAllText(path), Json)
        ?? throw new InvalidDataException("The profile file is empty.");

    private static Profile Clone(Profile p) => JsonSerializer.Deserialize<Profile>(JsonSerializer.Serialize(p, Json), Json)!;
}
