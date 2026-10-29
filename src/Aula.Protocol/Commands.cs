namespace Aula.Protocol;

/// <summary>
/// Builders for the 64-byte configuration reports. Each layout is taken from the vendor app's
/// decompiled senders; the function names are noted so the source can be re-checked.
/// </summary>
public static class Commands
{
    public const int ReportSize = 64;

    public static byte[] Begin() => Cmd(0x04, 0x18);
    public static byte[] Apply() => Cmd(0x04, 0x02);
    public static byte[] Finalize() => Cmd(0x04, 0xF0);

    /// <summary>LCD image header (FUN_00422b50): byte2 = slot, bytes 8–9 = page count LE.</summary>
    public static byte[] LcdHeader(int slot, int pageCount)
    {
        var r = Cmd(0x04, 0x72);
        r[2] = (byte)slot;
        r[8] = (byte)pageCount;
        r[9] = (byte)(pageCount >> 8);
        return r;
    }

    /// <summary>Clock sync init (FUN_00423b10).</summary>
    public static byte[] ClockInit()
    {
        var r = Cmd(0x04, 0x28);
        r[8] = 0x01;
        return r;
    }

    /// <summary>Clock data (FUN_00423b10): slot, 0x5A, Y%2000, M, D, h, m, s, 0, weekday, …, AA 55.</summary>
    public static byte[] ClockData(DateTime now, int slot = 1)
    {
        var r = new byte[ReportSize];
        r[1] = (byte)slot;
        r[2] = 0x5A;
        r[3] = (byte)(now.Year % 2000);
        r[4] = (byte)now.Month;
        r[5] = (byte)now.Day;
        r[6] = (byte)now.Hour;
        r[7] = (byte)now.Minute;
        r[8] = (byte)now.Second;
        r[10] = (byte)now.DayOfWeek;
        Trailer(r, 62);
        return r;
    }

    /// <summary>Lighting init (FUN_0042b040; byte2 stays 0 there, unlike some third-party notes).</summary>
    public static byte[] LightingInit()
    {
        var r = Cmd(0x04, 0x13);
        r[8] = 0x01;
        return r;
    }

    /// <summary>Main backlight mode data (FUN_0042b040).</summary>
    public static byte[] LightingData(LightingSettings s)
    {
        s.Validate();
        var r = new byte[ReportSize];
        r[0] = (byte)s.Effect;
        if (s.Effect != LightingEffect.Off)
        {
            r[1] = s.Red;
            r[2] = s.Green;
            r[3] = s.Blue;
            r[8] = (byte)(s.Colorful ? 1 : 0);
            r[9] = (byte)s.Brightness;
            r[10] = (byte)s.Speed;
            r[11] = (byte)s.Direction;
        }
        Trailer(r, 14);
        return r;
    }

    /// <summary>
    /// Light-bar/side "LED layer" data (FUN_00433fd0): the lighting data report with only the mode byte set.
    /// The vendor hides this for the F108 Pro (sidelight="0"); whether the firmware honours it is checked on hardware.
    /// </summary>
    public static byte[] ZoneData(ZoneMode mode)
    {
        var r = new byte[ReportSize];
        r[0] = (byte)mode;
        Trailer(r, 14);
        return r;
    }

    /// <summary>Switches the backlight to the per-key "user light" mode (FUN_0044b800): mode 0x80, brightness in byte 9.</summary>
    public static byte[] UserLightModeData(int brightness)
    {
        var r = new byte[ReportSize];
        r[0] = UserLightMode;
        r[9] = (byte)Math.Clamp(brightness, 0, LightingSettings.MaxBrightness);
        Trailer(r, 14);
        return r;
    }

    public const byte UserLightMode = 0x80;

    /// <summary>Per-key table init (FUN_0044b910): byte 8 = 9 for RGB, 3 for on/off.</summary>
    public static byte[] PerKeyInit()
    {
        var r = Cmd(0x04, 0x23);
        r[8] = 0x09;
        return r;
    }

    public const int PerKeyTableSize = 0x240;

    /// <summary>
    /// Per-key RGB table (FUN_0044b910): 4 bytes per light index — index, R, G, B — then AA 55 at the end.
    /// Light indices run 1–123 on the F108 Pro; slot 0 is unused.
    /// </summary>
    public static byte[] PerKeyTable(IReadOnlyDictionary<int, (byte R, byte G, byte B)> colors)
    {
        var t = new byte[PerKeyTableSize];
        foreach (var (index, (r, g, b)) in colors)
        {
            if (index is < 1 or > (PerKeyTableSize - 2) / 4 - 1)
                throw new ArgumentOutOfRangeException(nameof(colors), $"Light index {index} is outside the table.");
            t[index * 4] = (byte)index;
            t[index * 4 + 1] = r;
            t[index * 4 + 2] = g;
            t[index * 4 + 3] = b;
        }
        Trailer(t, PerKeyTableSize - 2);
        return t;
    }

    /// <summary>Key remap init (FUN_004185e0): 04 11 for the normal layer, 04 27 for the FN layer; byte 8 = 9.</summary>
    public static byte[] KeyMapInit(KeyLayer layer)
    {
        var r = Cmd(0x04, layer == KeyLayer.Fn ? (byte)0x27 : (byte)0x11);
        r[8] = 0x09;
        return r;
    }

    public const int KeyMapTableSize = 0x240;
    public const int MaxKeyIndex = (KeyMapTableSize - 2) / 4 - 1;

    /// <summary>Key remap table (FUN_004185e0): one 4-byte action per key index, AA 55 at the end. Zeros keep the default.</summary>
    public static byte[] KeyMapTable(IReadOnlyDictionary<int, KeyAction> actions)
    {
        var t = new byte[KeyMapTableSize];
        foreach (var (index, a) in actions)
        {
            if (index is < 1 or > MaxKeyIndex)
                throw new ArgumentOutOfRangeException(nameof(actions), $"Key index {index} is outside the table.");
            t[index * 4] = a.B0;
            t[index * 4 + 1] = a.B1;
            t[index * 4 + 2] = a.B2;
            t[index * 4 + 3] = a.B3;
        }
        Trailer(t, KeyMapTableSize - 2);
        return t;
    }

    /// <summary>Live per-key frame init (FUN_00432920): 04 20, byte 8 = 8 for RGB keyboards.</summary>
    public static byte[] RealtimeInit()
    {
        var r = Cmd(0x04, 0x20);
        r[8] = 0x08;
        return r;
    }

    public const int RealtimeFrameSize = 0x200;

    /// <summary>
    /// Live per-key frame (FUN_00432920): packed [light, R, G, B] entries, one per lit key, zero-padded to 512 bytes.
    /// Unlike the per-key table, entries are sequential rather than placed at light·4.
    /// </summary>
    public static byte[] RealtimeFrame(IReadOnlyDictionary<int, (byte R, byte G, byte B)> colors)
    {
        if (colors.Count > RealtimeFrameSize / 4)
            throw new ArgumentException("Too many keys for one frame.", nameof(colors));
        var f = new byte[RealtimeFrameSize];
        var p = 0;
        foreach (var (index, (r, g, b)) in colors.OrderBy(kv => kv.Key))
        {
            if (index is < 1 or > 255) throw new ArgumentOutOfRangeException(nameof(colors), $"Light index {index} is invalid.");
            f[p] = (byte)index;
            f[p + 1] = r;
            f[p + 2] = g;
            f[p + 3] = b;
            p += 4;
        }
        return f;
    }

    /// <summary>Starts a macro upload (FUN_0042d630); it takes the place of 04 18.</summary>
    public static byte[] MacroInit() => Cmd(0x04, 0x19);

    /// <summary>Macro data header (FUN_0042d630): byte 8 = number of 64-byte reports that follow.</summary>
    public static byte[] MacroHeader(int reportCount)
    {
        var r = Cmd(0x04, 0x15);
        r[8] = (byte)reportCount;
        return r;
    }

    /// <summary>Device options init (FUN_00414230): 04 17 with bytes 2 and 8 = 1.</summary>
    public static byte[] OptionsInit()
    {
        var r = Cmd(0x04, 0x17);
        r[2] = 0x01;
        r[8] = 0x01;
        return r;
    }

    /// <summary>Device options data (FUN_00414230): 00 01 altTab altF4 win fnSwitch sleep 00 response … AA 55.</summary>
    public static byte[] OptionsData(DeviceOptions o)
    {
        o.Validate();
        var r = new byte[ReportSize];
        r[1] = 0x01;
        // Checkbox order in FUN_00413910: 0x678 = Alt+Tab, 0x67c = Alt+F4, 0x680 = Win.
        r[2] = (byte)(o.DisableAltTab ? 1 : 0);
        r[3] = (byte)(o.DisableAltF4 ? 1 : 0);
        r[4] = (byte)(o.DisableWinKey ? 1 : 0);
        r[5] = (byte)(o.FnToggle ? 1 : 0);
        r[6] = (byte)o.Sleep;
        r[8] = (byte)o.ResponseLevel;
        Trailer(r, 62);
        return r;
    }

    private static byte[] Cmd(byte a, byte b)
    {
        var r = new byte[ReportSize];
        r[0] = a;
        r[1] = b;
        return r;
    }

    /// <summary>The 0x55AA marker, stored little-endian (AA 55). The keyboard ignores 55 AA.</summary>
    private static void Trailer(byte[] r, int offset)
    {
        r[offset] = 0xAA;
        r[offset + 1] = 0x55;
    }
}
