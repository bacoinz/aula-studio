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
