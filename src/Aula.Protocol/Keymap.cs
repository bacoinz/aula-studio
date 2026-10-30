namespace Aula.Protocol;

/// <summary>Which key layer a remap table targets: the normal layer (04 11) or the FN layer (04 27).</summary>
public enum KeyLayer
{
    Normal,
    Fn,
}

/// <summary>Modifier bits as the firmware stores them (HID boot-report order, FUN_00451b90).</summary>
[Flags]
public enum KeyModifiers : byte
{
    None = 0,
    LeftCtrl = 0x01,
    LeftShift = 0x02,
    LeftAlt = 0x04,
    LeftWin = 0x08,
    RightCtrl = 0x10,
    RightShift = 0x20,
    RightAlt = 0x40,
    RightWin = 0x80,
}

public enum MouseAction : byte
{
    Left,
    Right,
    Middle,
    Back,
    Forward,
    WheelUp,
    WheelDown,
}

/// <summary>Macro playback modes, as the vendor's mode combo item data (FUN_0040f8b0).</summary>
public enum MacroPlayMode : byte
{
    Once = 0,
    Repeat = 1,
    UntilPressedAgain = 2,
    WhileHeld = 3,
}

/// <summary>
/// One 4-byte entry of the key remap table (FUN_004185e0). All zeros means "keep the key's default".
/// </summary>
public readonly record struct KeyAction(byte B0, byte B1, byte B2, byte B3)
{
    public static KeyAction Default => default;

    /// <summary>Vendor type 1, "Disable Key".</summary>
    public static KeyAction Disabled => new(0x05, 0x03, 0, 0);

    public bool IsDefault => this == default;

    /// <summary>A key or combination: type 2, modifier bits, HID usage. A lone modifier goes in the bits only.</summary>
    public static KeyAction Key(byte hidUsage, KeyModifiers modifiers = KeyModifiers.None)
    {
        if (hidUsage is >= 0xE0 and <= 0xE7)
            return new(0x02, (byte)((byte)modifiers | 1 << (hidUsage - 0xE0)), 0, 0);
        return new(0x02, (byte)modifiers, hidUsage, 0);
    }

    public static KeyAction Mouse(MouseAction action) => action switch
    {
        MouseAction.Left => new(0x01, 0x01, 0x01, 0),
        MouseAction.Right => new(0x01, 0x01, 0x02, 0),
        MouseAction.Middle => new(0x01, 0x01, 0x04, 0),
        MouseAction.Back => new(0x01, 0x01, 0x08, 0),
        MouseAction.Forward => new(0x01, 0x01, 0x10, 0),
        MouseAction.WheelUp => new(0x01, 0x03, 0x01, 0),
        MouseAction.WheelDown => new(0x01, 0x03, 0xFF, 0),
        _ => throw new ArgumentOutOfRangeException(nameof(action)),
    };

    /// <summary>A HID consumer-page usage (media keys, calculator, browser…): type 3, usage LE16.</summary>
    public static KeyAction Consumer(ushort usage) => new(0x03, (byte)usage, (byte)(usage >> 8), 0);

    /// <summary>Plays macro slot <paramref name="index"/>: type 6, slot, mode, repeat count.</summary>
    public static KeyAction Macro(int index, MacroPlayMode mode = MacroPlayMode.Once, int repeat = 1)
    {
        if (index is < 0 or >= MacroSet.MaxMacros) throw new ArgumentOutOfRangeException(nameof(index));
        return new(0x06, (byte)index, (byte)mode, (byte)Math.Clamp(repeat, 1, 255));
    }

    public byte Type => B0;

    public uint ToUInt32() => (uint)(B0 | B1 << 8 | B2 << 16 | B3 << 24);

    public static KeyAction FromUInt32(uint v) => new((byte)v, (byte)(v >> 8), (byte)(v >> 16), (byte)(v >> 24));
}

public enum MacroEventKind
{
    KeyDown,
    KeyUp,
    MouseDown,
    MouseUp,
}

/// <summary>One recorded input, followed by a pause before the next.</summary>
public sealed record MacroEvent(MacroEventKind Kind, byte Code, int DelayMs)
{
    /// <summary>The firmware's shortest pause (FUN_0042d630 clamps to 10 ms).</summary>
    public const int MinDelayMs = 10;
    public const int MaxDelayMs = 0xFFFF;
}

/// <summary>Builds the macro buffer sent with 04 15 (FUN_0042d630, wired variant).</summary>
public static class MacroSet
{
    public const int MaxMacros = 100;
    public const int BufferCapacity = 0xE00;
    private const int TableSize = MaxMacros * 4;
    private const int MacroHeader = 8;

    /// <summary>Mouse button codes used inside macros: left 1, right 2, middle 4.</summary>
    public static byte MouseCode(MouseAction button) => button switch
    {
        MouseAction.Left => 1,
        MouseAction.Right => 2,
        MouseAction.Middle => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(button), "Macros record only left, right and middle buttons."),
    };

    /// <summary>Bytes a macro takes in the buffer: an 8-byte header plus two entries (input, pause) per event.</summary>
    public static int SizeOf(IReadOnlyList<MacroEvent> events) => events.Count == 0 ? 0 : MacroHeader + events.Count * 8;

    /// <summary>
    /// 400-byte offset table (one LE16 offset per slot, FFFFFFFF for an empty slot), then each macro:
    /// LE16 entry count, 6 zero bytes, entries [b0 b1 code type]. Padded to whole 64-byte reports
    /// with an extra report when it ends exactly on one, and AA 55 in the last two bytes.
    /// </summary>
    public static byte[] Build(IReadOnlyList<IReadOnlyList<MacroEvent>> macros)
    {
        var buf = new byte[BufferCapacity];
        var offset = Fill(macros, buf);
        var reports = offset / Commands.ReportSize + (offset % Commands.ReportSize == 0 ? 1 : 2);
        return Finish(buf, reports * Commands.ReportSize, macros);
    }

    /// <summary>The same contents in whole 28-byte chunks (FUN_0042dbb0), and how many bytes the macros use.</summary>
    public static (byte[] Buffer, int Used) BuildForDongle(IReadOnlyList<IReadOnlyList<MacroEvent>> macros)
    {
        var buf = new byte[BufferCapacity];
        var offset = Fill(macros, buf);
        var chunks = offset / DongleCommands.ChunkSize + (offset % DongleCommands.ChunkSize == 0 ? 1 : 2);
        return (Finish(buf, chunks * DongleCommands.ChunkSize, macros), offset);
    }

    private static byte[] Finish(byte[] buf, int length, IReadOnlyList<IReadOnlyList<MacroEvent>> macros)
    {
        if (length > BufferCapacity)
            throw new ArgumentException("The macros don't fit in the keyboard's macro memory.", nameof(macros));
        var result = buf.AsSpan(0, length).ToArray();
        result[length - 2] = 0xAA;
        result[length - 1] = 0x55;
        return result;
    }

    private static int Fill(IReadOnlyList<IReadOnlyList<MacroEvent>> macros, byte[] buf)
    {
        if (macros.Count > MaxMacros) throw new ArgumentException($"At most {MaxMacros} macros.", nameof(macros));
        var offset = TableSize;
        for (var slot = 0; slot < macros.Count; slot++)
        {
            var events = macros[slot];
            if (events.Count == 0)
            {
                BitConverter.TryWriteBytes(buf.AsSpan(slot * 4), uint.MaxValue);
                continue;
            }
            if (offset + SizeOf(events) + 2 > BufferCapacity)
                throw new ArgumentException("The macros don't fit in the keyboard's macro memory.", nameof(macros));
            buf[slot * 4] = (byte)offset;
            buf[slot * 4 + 1] = (byte)(offset >> 8);
            var entries = events.Count * 2;
            buf[offset] = (byte)entries;
            buf[offset + 1] = (byte)(entries >> 8);
            var p = offset + MacroHeader;
            foreach (var e in events)
            {
                buf[p + 2] = e.Code;
                buf[p + 3] = e.Kind switch
                {
                    MacroEventKind.KeyDown => 0xB0,
                    MacroEventKind.KeyUp => 0x30,
                    MacroEventKind.MouseDown => 0x90,
                    _ => 0x10,
                };
                var delay = Math.Clamp(e.DelayMs, MacroEvent.MinDelayMs, MacroEvent.MaxDelayMs);
                buf[p + 4] = (byte)delay;
                buf[p + 5] = (byte)(delay >> 8);
                buf[p + 7] = 0x50;
                p += 8;
            }
            offset = p;
        }
        return offset;
    }

    /// <summary>Bytes still free for more events, so the editor can warn before the memory is full.</summary>
    public static int FreeBytes(IReadOnlyList<IReadOnlyList<MacroEvent>> macros) =>
        BufferCapacity - Commands.ReportSize - TableSize - macros.Sum(SizeOf);
}
