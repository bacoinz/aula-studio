namespace Aula.Protocol;

/// <summary>
/// High-level operations on the wired F108 Pro. Every sequence reproduces the vendor app's order,
/// readbacks and 35 ms pacing (cmd_delaytime), since the firmware ignores commands when either is skipped.
/// </summary>
public sealed class AulaKeyboard(IAulaTransport transport, TimeSpan? commandDelay = null, Action<TimeSpan>? sleep = null) : IDisposable
{
    public static readonly TimeSpan DefaultCommandDelay = TimeSpan.FromMilliseconds(35);
    public static readonly TimeSpan LcdAckTimeout = TimeSpan.FromMilliseconds(300);

    /// <summary>Measured on a wired F108 Pro: 2231 pages took 329 s (the USB endpoint's own pace).</summary>
    public static readonly TimeSpan LcdTimePerPage = TimeSpan.FromMilliseconds(148);

    public static TimeSpan EstimateUpload(int frameCount) =>
        LcdTimePerPage * LcdImage.PagesFor(frameCount) + TimeSpan.FromSeconds(1);

    private readonly TimeSpan _delay = commandDelay ?? DefaultCommandDelay;
    private readonly Action<TimeSpan> _sleep = sleep ?? Thread.Sleep;

    /// <summary>Receives every report sent and read, for diagnostics.</summary>
    public event Action<string, byte[]>? Trace;

    public static AulaKeyboard OpenUsb() => new(WindowsHidTransport.Open());

    public void SyncClock(DateTime now)
    {
        Send(Commands.Begin(), readback: true);
        Send(Commands.ClockInit(), readback: true);
        Send(Commands.ClockData(now), readback: true);
        Send(Commands.Apply(), readback: true);
    }

    public void SetLighting(LightingSettings settings)
    {
        var data = Commands.LightingData(settings);
        Send(Commands.Begin(), readback: true);
        Send(Commands.LightingInit(), readback: true);
        Send(data, readback: false);
        Send(Commands.Apply(), readback: true);
        Send(Commands.Finalize(), readback: false);
    }

    /// <summary>Light bar / side LEDs (vendor LED-layer sequence, FUN_00433fd0).</summary>
    public void SetZone(ZoneMode mode)
    {
        Send(Commands.Begin(), readback: true);
        Send(Commands.LightingInit(), readback: true);
        Send(Commands.ZoneData(mode), readback: false);
        Send(Commands.Apply(), readback: true);
        Send(Commands.Finalize(), readback: false);
    }

    /// <summary>Gives each key its own colour (FUN_0044b800 then FUN_0044b910). Keys left out stay dark.</summary>
    public void SetPerKeyColors(IReadOnlyDictionary<int, (byte R, byte G, byte B)> colors, int brightness)
    {
        var table = Commands.PerKeyTable(colors);

        Send(Commands.Begin(), readback: true);
        Send(Commands.LightingInit(), readback: true);
        Send(Commands.UserLightModeData(brightness), readback: false);
        Send(Commands.Apply(), readback: true);
        Send(Commands.Finalize(), readback: false);

        Send(Commands.Begin(), readback: true);
        Send(Commands.PerKeyInit(), readback: true);
        SendLarge(table);
        Send(Commands.Apply(), readback: true);
        Send(Commands.Finalize(), readback: true);
    }

    /// <summary>Uploads an image to the screen. Writes the keyboard's flash; aborts on the first missing acknowledgement.</summary>
    public void UploadLcdImage(LcdImage image, IProgress<double>? progress = null, CancellationToken cancel = default)
    {
        // LcdImage already enforces the frame limit; re-check the page count it implies before touching flash.
        if (image.FrameCount > LcdImage.MaxFrames || image.PageCount != LcdImage.PagesFor(image.FrameCount))
            throw new AulaSafetyException("LCD buffer does not match a valid image; nothing was sent.");

        Send(Commands.Begin(), readback: true);
        Send(Commands.LcdHeader(slot: 1, image.PageCount), readback: true);
        for (var i = 0; i < image.PageCount; i++)
        {
            cancel.ThrowIfCancellationRequested();
            var ack = transport.WriteLcdPage(image.Page(i), LcdAckTimeout);
            if (ack is null)
                throw new AulaException($"The keyboard stopped acknowledging at page {i + 1} of {image.PageCount}.");
            Trace?.Invoke($"lcd ack {i + 1}/{image.PageCount}", ack);
            progress?.Report((i + 1) / (double)image.PageCount);
        }
        Send(Commands.Apply(), readback: true);
    }

    /// <summary>Mirrors FUN_0044edc0: pause, send, and optionally pause and read the report back.</summary>
    private void Send(byte[] report, bool readback)
    {
        _sleep(_delay);
        transport.SetFeature(report);
        Trace?.Invoke("set", report);
        if (!readback) return;
        _sleep(_delay);
        var reply = transport.GetFeature(report);
        Trace?.Invoke("get", reply);
    }

    /// <summary>
    /// Mirrors FUN_0044edc0's long-payload branch: one pause, then each 64-byte slice followed by a pause,
    /// retrying a rejected slice up to 10 times. The vendor reads nothing back here.
    /// </summary>
    private void SendLarge(byte[] payload)
    {
        if (payload.Length % Commands.ReportSize != 0)
            throw new ArgumentException("Long payloads are whole 64-byte reports.", nameof(payload));
        _sleep(_delay);
        for (var offset = 0; offset < payload.Length; offset += Commands.ReportSize)
        {
            var slice = payload.AsSpan(offset, Commands.ReportSize).ToArray();
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    transport.SetFeature(slice);
                    Trace?.Invoke($"set+{offset}", slice);
                    break;
                }
                catch (AulaException) when (attempt < 10)
                {
                    _sleep(_delay);
                }
            }
            _sleep(_delay);
        }
    }

    public void Dispose() => transport.Dispose();
}
