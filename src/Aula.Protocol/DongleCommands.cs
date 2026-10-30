namespace Aula.Protocol;

/// <summary>
/// Builders for the dongle's reports: report ID 0, command, data length, index, then up to 28 bytes.
/// The payloads match the wired ones, shifted by the header; the functions named are the vendor's senders.
/// </summary>
public static class DongleCommands
{
    public const int ChunkSize = 28;
    public const int MusicBands = 21;

    /// <summary>Levels are row counts: the vendor's music frames are 6 rows tall.</summary>
    public const int MusicLevels = 6;

    private const byte SmallLength = 0x10;

    /// <summary>Battery query (FUN_004357d0); the reply holds the percentage at index 3.</summary>
    public static byte[] Battery()
    {
        var r = new byte[DongleTransport.ReportLength - 1];
        r[1] = 0x20;
        r[2] = 0x01;
        return r;
    }

    /// <summary>Effect (FUN_0042b1e0).</summary>
    public static byte[] Lighting(LightingSettings s) => Small(0x05, Commands.LightingData(s));

    /// <summary>Per-key mode (FUN_0044bd80).</summary>
    public static byte[] UserLightMode(int brightness) => Small(0x05, Commands.UserLightModeData(brightness));

    /// <summary>Clock (FUN_00423c40).</summary>
    public static byte[] Clock(DateTime now) => SmallWithTrailer(0x0C, Commands.ClockData(now));

    /// <summary>Options (FUN_004144a0).</summary>
    public static byte[] Options(DeviceOptions o) => SmallWithTrailer(0x07, Commands.OptionsData(o));

    /// <summary>The per-key table in chunks (FUN_0044bd80).</summary>
    public static IEnumerable<byte[]> PerKeyTable(IReadOnlyDictionary<int, (byte R, byte G, byte B)> colors) =>
        Chunks(0x14, Commands.PerKeyTable(colors));

    /// <summary>A remap table in chunks: command 10 for the normal layer, 12 for FN (FUN_00418e80).</summary>
    public static IEnumerable<byte[]> KeyMap(KeyLayer layer, IReadOnlyDictionary<int, KeyAction> actions) =>
        Chunks(layer == KeyLayer.Fn ? (byte)0x12 : (byte)0x10, Commands.KeyMapTable(actions));

    /// <summary>
    /// The macro buffer in chunks (FUN_0042dbb0). As the vendor does, the last chunk's length field holds
    /// what is left over from the data before the padding, and an extra chunk carries the end marker.
    /// </summary>
    public static IEnumerable<byte[]> Macros(IReadOnlyList<IReadOnlyList<MacroEvent>> macros)
    {
        var (buffer, used) = MacroSet.BuildForDongle(macros);
        var count = buffer.Length / ChunkSize;
        for (var i = 0; i < count; i++)
            yield return Chunk(0x09, i == count - 1 ? used % ChunkSize : ChunkSize, i, buffer.AsSpan(i * ChunkSize, ChunkSize));
    }

    /// <summary>A music frame (FUN_00432b60): the firmware draws the levels with its own effect.</summary>
    public static byte[] Music(IReadOnlyList<float> levels, byte foreground, byte background, byte brightness)
    {
        if (levels.Count != MusicBands) throw new ArgumentException($"Music frames have {MusicBands} bands.", nameof(levels));
        var r = new byte[DongleTransport.ReportLength - 1];
        r[1] = 0x0B;
        r[2] = 0x1C;
        r[4] = foreground;
        r[5] = background;
        r[6] = brightness;
        for (var i = 0; i < MusicBands; i++)
            r[7 + i] = (byte)Math.Clamp((int)MathF.Round(levels[i] * MusicLevels), 0, MusicLevels);
        return r;
    }

    private static byte[] SmallWithTrailer(byte command, byte[] data)
    {
        var copy = data[..SmallLength];
        copy[14] = 0xAA;
        copy[15] = 0x55;
        return Small(command, copy);
    }

    private static byte[] Small(byte command, byte[] data) => Chunk(command, SmallLength, 0, data.AsSpan(0, SmallLength));

    private static IEnumerable<byte[]> Chunks(byte command, byte[] payload)
    {
        for (int i = 0, offset = 0; offset < payload.Length; i++, offset += ChunkSize)
        {
            var length = Math.Min(ChunkSize, payload.Length - offset);
            yield return Chunk(command, length, i, payload.AsSpan(offset, length));
        }
    }

    private static byte[] Chunk(byte command, int length, int index, ReadOnlySpan<byte> data)
    {
        var r = new byte[DongleTransport.ReportLength - 1];
        r[1] = command;
        r[2] = (byte)length;
        r[3] = (byte)index;
        data.CopyTo(r.AsSpan(4));
        return r;
    }
}
