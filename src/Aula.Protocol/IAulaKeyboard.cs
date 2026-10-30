namespace Aula.Protocol;

/// <summary>What the app can ask of the keyboard, over USB or through the 2.4 GHz dongle.</summary>
public interface IAulaKeyboard : IDisposable
{
    /// <summary>The vendor keeps the screen to USB (screen="0" under menu_wireless).</summary>
    bool SupportsScreen { get; }

    void SyncClock(DateTime now);
    void SetLighting(LightingSettings settings);
    void SetPerKeyColors(IReadOnlyDictionary<int, (byte R, byte G, byte B)> colors, int brightness);
    void SetOptions(DeviceOptions options);
    void SetKeyMap(KeyLayer layer, IReadOnlyDictionary<int, KeyAction> actions);
    void SetMacros(IReadOnlyList<IReadOnlyList<MacroEvent>> macros);
    void UploadLcdImage(LcdImage image, IProgress<double>? progress = null, CancellationToken cancel = default);

    /// <summary>One music frame. USB shows <paramref name="colors"/>; the dongle sends the band levels (0–1) instead.</summary>
    void SendMusicFrame(IReadOnlyDictionary<int, (byte R, byte G, byte B)> colors, IReadOnlyList<float> levels);
}

/// <summary>Finds and opens the keyboard over whichever link is present. USB wins when both are.</summary>
public static class AulaConnection
{
    public static KeyboardLink Detect() =>
        WindowsHidTransport.Find() is not null ? KeyboardLink.Usb
        : DongleTransport.Find() is not null ? KeyboardLink.Dongle
        : KeyboardLink.None;

    public static IAulaKeyboard Open() => Detect() switch
    {
        KeyboardLink.Usb => AulaKeyboard.OpenUsb(),
        KeyboardLink.Dongle => AulaDongle.Open(),
        _ => throw new AulaException("AULA F108 Pro not found."),
    };
}

public enum KeyboardLink
{
    None,
    Usb,
    Dongle,
}
