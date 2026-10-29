using System.Runtime.InteropServices;
using System.Windows.Input;

namespace AulaStudio.Services;

/// <summary>
/// Turns a pressed key into the HID usage the keyboard firmware expects. Goes through the scan code,
/// so the physical key is what counts, whatever the Windows layout (PT, US…) prints on it.
/// </summary>
public static class HidKeys
{
    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint code, uint mapType);

    private const uint VkToVscEx = 4;

    // Scan code set 1 → HID usage (keyboard page). Extended (E0-prefixed) codes are offset by 0x100.
    private static readonly Dictionary<int, byte> ScanToHid = BuildScanTable();

    private static Dictionary<int, byte> BuildScanTable()
    {
        var t = new Dictionary<int, byte> { [0x01] = 0x29, [0x0C] = 0x2D, [0x0D] = 0x2E, [0x0E] = 0x2A, [0x0F] = 0x2B,
            [0x1A] = 0x2F, [0x1B] = 0x30, [0x1C] = 0x28, [0x1D] = 0xE0, [0x27] = 0x33, [0x28] = 0x34, [0x29] = 0x35,
            [0x2A] = 0xE1, [0x2B] = 0x31, [0x33] = 0x36, [0x34] = 0x37, [0x35] = 0x38, [0x36] = 0xE5, [0x37] = 0x55,
            [0x38] = 0xE2, [0x39] = 0x2C, [0x3A] = 0x39, [0x45] = 0x53, [0x46] = 0x47, [0x47] = 0x5F, [0x48] = 0x60,
            [0x49] = 0x61, [0x4A] = 0x56, [0x4B] = 0x5C, [0x4C] = 0x5D, [0x4D] = 0x5E, [0x4E] = 0x57, [0x4F] = 0x59,
            [0x50] = 0x5A, [0x51] = 0x5B, [0x52] = 0x62, [0x53] = 0x63, [0x56] = 0x64, [0x57] = 0x44, [0x58] = 0x45,
            [0x76] = 0x73,
            [0x11C] = 0x58, [0x11D] = 0xE4, [0x135] = 0x54, [0x137] = 0x46, [0x138] = 0xE6, [0x147] = 0x4A,
            [0x148] = 0x52, [0x149] = 0x4B, [0x14B] = 0x50, [0x14D] = 0x4F, [0x14F] = 0x4D, [0x150] = 0x51,
            [0x151] = 0x4E, [0x152] = 0x49, [0x153] = 0x4C, [0x15B] = 0xE3, [0x15C] = 0xE7, [0x15D] = 0x65 };
        for (var i = 0; i < 10; i++) t[0x02 + i] = (byte)(0x1E + i);          // 1 … 0
        byte[] q = [0x14, 0x1A, 0x08, 0x15, 0x17, 0x1C, 0x18, 0x0C, 0x12, 0x13]; // Q … P
        for (var i = 0; i < q.Length; i++) t[0x10 + i] = q[i];
        byte[] a = [0x04, 0x16, 0x07, 0x09, 0x0A, 0x0B, 0x0D, 0x0E, 0x0F];       // A … L
        for (var i = 0; i < a.Length; i++) t[0x1E + i] = a[i];
        byte[] z = [0x1D, 0x1B, 0x06, 0x19, 0x05, 0x11, 0x10];                   // Z … M
        for (var i = 0; i < z.Length; i++) t[0x2C + i] = z[i];
        for (var i = 0; i < 10; i++) t[0x3B + i] = (byte)(0x3A + i);          // F1 … F10
        for (var i = 0; i < 11; i++) t[0x64 + i] = (byte)(0x68 + i);          // F13 … F23
        return t;
    }

    /// <summary>The HID usage for a WPF key event, or null when the key has none (e.g. IME keys).</summary>
    public static byte? FromKeyEvent(KeyEventArgs e)
    {
        var key = e.Key switch
        {
            Key.System => e.SystemKey,
            Key.ImeProcessed => e.ImeProcessedKey,
            _ => e.Key,
        };
        return FromKey(key);
    }

    public static byte? FromKey(Key key)
    {
        // Keys whose scan codes Windows reports ambiguously.
        switch (key)
        {
            case Key.Pause: return 0x48;
            case Key.NumLock: return 0x53;
            case Key.PrintScreen: return 0x46;
            case Key.RightAlt: return 0xE6;
            case Key.RightCtrl: return 0xE4;
        }
        var vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        if (vk == 0) return null;
        var sc = MapVirtualKey(vk, VkToVscEx);
        var index = (int)(sc & 0xFF) | ((sc & 0xFF00) == 0xE000 ? 0x100 : 0);
        return ScanToHid.TryGetValue(index, out var hid) ? hid : null;
    }

    public static bool IsModifier(byte hid) => hid is >= 0xE0 and <= 0xE7;

    private static readonly Dictionary<byte, string> Extra = new()
    {
        [0x28] = "Enter", [0x53] = "NumLock", [0x2C] = "Space", [0x64] = "ISO \\", [0x58] = "Num Enter",
        [0xE0] = "Ctrl", [0xE1] = "Shift", [0xE2] = "Alt", [0xE3] = "Win",
        [0xE4] = "Ctrl Dir.", [0xE5] = "Shift Dir.", [0xE6] = "AltGr", [0xE7] = "Win Dir.",
    };

    /// <summary>A short name for a HID usage, using the F108 Pro's own keycap labels where it has the key.</summary>
    public static string Name(byte hid)
    {
        if (Extra.TryGetValue(hid, out var n)) return n;
        if (hid is >= 0x68 and <= 0x73) return $"F{hid - 0x68 + 13}";
        var key = KeyboardLayout.Keys.FirstOrDefault(k => k.Code == hid);
        if (key is null) return $"0x{hid:X2}";
        // Numpad keys share labels with the main block; mark them.
        var label = key.Label.Length > 0 ? key.Label : key.Name;
        return key.Col >= 17 && hid is >= 0x54 and <= 0x63 ? $"Num {label}" : label;
    }
}
