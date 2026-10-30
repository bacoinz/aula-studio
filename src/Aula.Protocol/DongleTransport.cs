using Aula.Protocol.Native;
using Microsoft.Win32.SafeHandles;

namespace Aula.Protocol;

/// <summary>
/// The 2.4 GHz dongle's vendor interface (MI_03). Unlike USB it uses output/input reports of 33 bytes:
/// report ID 0, 31 bytes of data and a checksum in the last byte (FUN_0044f4e0).
/// </summary>
public sealed class DongleTransport : IDisposable
{
    public const ushort VendorId = 0x05AC;
    public const ushort ProductId = 0x024F;
    public const int ReportLength = 33;

    private static readonly TimeSpan ReplyTimeout = TimeSpan.FromMilliseconds(500);

    private readonly SafeFileHandle _handle;
    private readonly FileStream _stream;
    private readonly int _inputLength;

    private DongleTransport(HidDeviceInfo info)
    {
        _handle = info.Open(overlapped: true);
        _stream = new FileStream(_handle, FileAccess.ReadWrite, 0, isAsync: true);
        _inputLength = info.InputReportLength;
    }

    public static HidDeviceInfo? Find() =>
        HidDeviceInfo.Enumerate(VendorId, ProductId).FirstOrDefault(d =>
            d.UsagePage >= 0xFF00 && d.OutputReportLength == ReportLength && d.Path.Contains("mi_03", StringComparison.OrdinalIgnoreCase));

    public static DongleTransport Open() =>
        new(Find() ?? throw new AulaException("AULA F108 Pro dongle not found."));

    /// <summary>Adds the checksum: the low byte of the sum of every byte before it.</summary>
    public static byte[] Seal(ReadOnlySpan<byte> report)
    {
        var sealedReport = new byte[ReportLength];
        report[..Math.Min(report.Length, ReportLength - 1)].CopyTo(sealedReport);
        byte sum = 0;
        for (var i = 0; i < ReportLength - 1; i++) sum += sealedReport[i];
        sealedReport[ReportLength - 1] = sum;
        return sealedReport;
    }

    /// <summary>Sends a report and waits for the reply that echoes its first three data bytes, or null if none came.</summary>
    public byte[]? Exchange(ReadOnlySpan<byte> report)
    {
        var sealedReport = Seal(report);
        _stream.Write(sealedReport);

        var buffer = new byte[_inputLength];
        using var cts = new CancellationTokenSource(ReplyTimeout);
        try
        {
            while (true)
            {
                var read = _stream.ReadAsync(buffer, 0, buffer.Length, cts.Token).GetAwaiter().GetResult();
                if (read < 4) continue;
                // Like the vendor app, drop the leading report ID 0 when present.
                var reply = buffer[0] == 0 ? buffer[1..read] : buffer[..read];
                if (reply[0] == sealedReport[1] && reply[1] == sealedReport[2] && reply[2] == sealedReport[3]) return reply;
            }
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    public void Dispose()
    {
        _stream.Dispose();
        _handle.Dispose();
    }
}
