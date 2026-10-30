namespace Aula.Protocol;

/// <summary>The keyboard through the 2.4 GHz dongle. The vendor sends these without a begin or apply step.</summary>
public sealed class AulaDongle(DongleTransport transport) : IAulaKeyboard
{
    private static readonly TimeSpan ChunkDelay = TimeSpan.FromMilliseconds(2);

    // The vendor's first music effect, at full brightness; foreground/background come from its combo boxes.
    private const byte MusicForeground = 0;
    private const byte MusicBackground = 0;
    private const byte MusicBrightness = 5;

    public bool SupportsScreen => false;

    public static AulaDongle Open() => new(DongleTransport.Open());

    /// <summary>Battery percentage, or null when the keyboard doesn't answer (off, asleep or on another link).</summary>
    public int? ReadBattery() =>
        transport.Exchange(DongleCommands.Battery()) is { Length: > 3 } reply && reply[3] != 0 ? Math.Min((int)reply[3], 100) : null;

    public void SyncClock(DateTime now) => Send(DongleCommands.Clock(now));

    public void SetLighting(LightingSettings settings) => Send(DongleCommands.Lighting(settings));

    public void SetPerKeyColors(IReadOnlyDictionary<int, (byte R, byte G, byte B)> colors, int brightness)
    {
        Send(DongleCommands.UserLightMode(brightness));
        SendAll(DongleCommands.PerKeyTable(colors));
    }

    public void SetOptions(DeviceOptions options) => Send(DongleCommands.Options(options));

    public void SetKeyMap(KeyLayer layer, IReadOnlyDictionary<int, KeyAction> actions) =>
        SendAll(DongleCommands.KeyMap(layer, actions));

    public void SetMacros(IReadOnlyList<IReadOnlyList<MacroEvent>> macros) => SendAll(DongleCommands.Macros(macros));

    public void UploadLcdImage(LcdImage image, IProgress<double>? progress = null, CancellationToken cancel = default) =>
        throw new AulaException("The screen can only be changed over the USB cable.");

    public void SendMusicFrame(IReadOnlyDictionary<int, (byte R, byte G, byte B)> colors, IReadOnlyList<float> levels) =>
        Send(DongleCommands.Music(levels, MusicForeground, MusicBackground, MusicBrightness));

    private void SendAll(IEnumerable<byte[]> reports)
    {
        foreach (var report in reports)
        {
            Send(report);
            Thread.Sleep(ChunkDelay);
        }
    }

    private void Send(byte[] report)
    {
        if (transport.Exchange(report) is null)
            throw new AulaException("The keyboard didn't answer. Check it's on and in 2.4 GHz mode.");
    }

    public void Dispose() => transport.Dispose();
}
