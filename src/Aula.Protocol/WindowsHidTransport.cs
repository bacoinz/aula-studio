using System.Runtime.InteropServices;
using Aula.Protocol.Native;
using Microsoft.Win32.SafeHandles;

namespace Aula.Protocol;

/// <summary>Transport over the Windows HID stack, like the vendor app (HidD_SetFeature / WriteFile / ReadFile).</summary>
public sealed class WindowsHidTransport : IAulaTransport
{
    public const ushort VendorId = 0x0C45;
    public const ushort ProductId = 0x800A;
    public const ushort ConfigUsagePage = 0xFF13;
    public const ushort LcdUsagePage = 0xFF68;

    private readonly SafeFileHandle _config;
    private readonly SafeFileHandle _lcdHandle;
    private readonly FileStream _lcd;
    private readonly int _lcdOutputLength;
    private readonly int _lcdInputLength;

    private WindowsHidTransport(HidDeviceInfo config, HidDeviceInfo lcd)
    {
        _config = config.Open(overlapped: false);
        _lcdHandle = lcd.Open(overlapped: true);
        _lcd = new FileStream(_lcdHandle, FileAccess.ReadWrite, 0, isAsync: true);
        _lcdOutputLength = lcd.OutputReportLength;
        _lcdInputLength = lcd.InputReportLength;
    }

    /// <summary>Finds the wired keyboard's vendor interfaces, or returns null when it isn't connected.</summary>
    public static (HidDeviceInfo Config, HidDeviceInfo Lcd)? Find()
    {
        var all = HidDeviceInfo.Enumerate(VendorId, ProductId);
        var config = all.FirstOrDefault(d => d.UsagePage == ConfigUsagePage && d.FeatureReportLength == 65);
        var lcd = all.FirstOrDefault(d => d.UsagePage == LcdUsagePage && d.OutputReportLength == LcdImage.PageSize + 1);
        return config is null || lcd is null ? null : (config, lcd);
    }

    public static WindowsHidTransport Open()
    {
        var found = Find() ?? throw new AulaException("AULA F108 Pro not found on USB.");
        return new WindowsHidTransport(found.Config, found.Lcd);
    }

    public void SetFeature(ReadOnlySpan<byte> report)
    {
        var buffer = WithReportId(report, 65);
        if (!HidNative.HidD_SetFeature(_config, buffer, buffer.Length))
            throw new AulaException($"Feature report rejected (Win32 error {Marshal.GetLastWin32Error()}).");
    }

    public byte[] GetFeature(ReadOnlySpan<byte> request)
    {
        var buffer = WithReportId(request, 65);
        if (!HidNative.HidD_GetFeature(_config, buffer, buffer.Length))
            throw new AulaException($"Feature readback failed (Win32 error {Marshal.GetLastWin32Error()}).");
        return buffer[1..];
    }

    public byte[]? WriteLcdPage(ReadOnlySpan<byte> page, TimeSpan ackTimeout)
    {
        if (page.Length != LcdImage.PageSize)
            throw new ArgumentException($"LCD pages are exactly {LcdImage.PageSize} bytes.", nameof(page));
        _lcd.Write(WithReportId(page, _lcdOutputLength));

        var ack = new byte[_lcdInputLength];
        using var cts = new CancellationTokenSource(ackTimeout);
        try
        {
            var read = _lcd.ReadAsync(ack, 0, ack.Length, cts.Token).GetAwaiter().GetResult();
            // Like the vendor app, drop the leading report ID 0 when present.
            return read > 0 && ack[0] == 0 ? ack[1..read] : ack[..read];
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    private static byte[] WithReportId(ReadOnlySpan<byte> data, int length)
    {
        var buffer = new byte[length];
        data[..Math.Min(data.Length, length - 1)].CopyTo(buffer.AsSpan(1));
        return buffer;
    }

    public void Dispose()
    {
        _lcd.Dispose();
        _lcdHandle.Dispose();
        _config.Dispose();
    }
}
