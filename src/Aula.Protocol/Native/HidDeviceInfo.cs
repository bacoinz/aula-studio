using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Aula.Protocol.Native;

/// <summary>One HID top-level collection as Windows exposes it.</summary>
public sealed record HidDeviceInfo(
    string Path,
    ushort VendorId,
    ushort ProductId,
    ushort UsagePage,
    ushort Usage,
    int InputReportLength,
    int OutputReportLength,
    int FeatureReportLength)
{
    /// <summary>Lists every present HID collection matching the VID/PID.</summary>
    public static IReadOnlyList<HidDeviceInfo> Enumerate(ushort vendorId, ushort productId)
    {
        var result = new List<HidDeviceInfo>();
        HidNative.HidD_GetHidGuid(out var hidGuid);
        var set = HidNative.SetupDiGetClassDevs(ref hidGuid, IntPtr.Zero, IntPtr.Zero,
            HidNative.DIGCF_PRESENT | HidNative.DIGCF_DEVICEINTERFACE);
        if (set == new IntPtr(-1)) return result;
        try
        {
            for (var i = 0; ; i++)
            {
                var data = new HidNative.SP_DEVICE_INTERFACE_DATA { cbSize = Marshal.SizeOf<HidNative.SP_DEVICE_INTERFACE_DATA>() };
                if (!HidNative.SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref hidGuid, i, ref data)) break;
                var path = GetPath(set, ref data);
                if (path is null) continue;
                var info = Probe(path);
                if (info is not null && info.VendorId == vendorId && info.ProductId == productId)
                    result.Add(info);
            }
        }
        finally
        {
            HidNative.SetupDiDestroyDeviceInfoList(set);
        }
        return result;
    }

    private static string? GetPath(IntPtr set, ref HidNative.SP_DEVICE_INTERFACE_DATA data)
    {
        HidNative.SetupDiGetDeviceInterfaceDetail(set, ref data, IntPtr.Zero, 0, out var size, IntPtr.Zero);
        if (size <= 0) return null;
        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            // cbSize of SP_DEVICE_INTERFACE_DETAIL_DATA_W: 8 on x64, 6 on x86.
            Marshal.WriteInt32(buffer, IntPtr.Size == 8 ? 8 : 6);
            if (!HidNative.SetupDiGetDeviceInterfaceDetail(set, ref data, buffer, size, out _, IntPtr.Zero)) return null;
            return Marshal.PtrToStringUni(buffer + 4);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static HidDeviceInfo? Probe(string path)
    {
        // Zero access is enough for attributes and caps, even on collections Windows holds exclusively (keyboard/mouse).
        using var handle = HidNative.CreateFile(path, 0, HidNative.FILE_SHARE_READ | HidNative.FILE_SHARE_WRITE,
            IntPtr.Zero, HidNative.OPEN_EXISTING, 0, IntPtr.Zero);
        if (handle.IsInvalid) return null;
        var attrs = new HidNative.HIDD_ATTRIBUTES { Size = Marshal.SizeOf<HidNative.HIDD_ATTRIBUTES>() };
        if (!HidNative.HidD_GetAttributes(handle, ref attrs)) return null;
        if (!HidNative.HidD_GetPreparsedData(handle, out var preparsed)) return null;
        try
        {
            if (HidNative.HidP_GetCaps(preparsed, out var caps) != HidNative.HIDP_STATUS_SUCCESS) return null;
            return new HidDeviceInfo(path, attrs.VendorID, attrs.ProductID, caps.UsagePage, caps.Usage,
                caps.InputReportByteLength, caps.OutputReportByteLength, caps.FeatureReportByteLength);
        }
        finally
        {
            HidNative.HidD_FreePreparsedData(preparsed);
        }
    }

    internal SafeFileHandle Open(bool overlapped)
    {
        var handle = HidNative.CreateFile(Path, HidNative.GENERIC_READ | HidNative.GENERIC_WRITE,
            HidNative.FILE_SHARE_READ | HidNative.FILE_SHARE_WRITE, IntPtr.Zero, HidNative.OPEN_EXISTING,
            overlapped ? HidNative.FILE_FLAG_OVERLAPPED : 0, IntPtr.Zero);
        if (handle.IsInvalid)
            throw new AulaException($"Could not open HID device (Win32 error {Marshal.GetLastWin32Error()}).");
        return handle;
    }
}
