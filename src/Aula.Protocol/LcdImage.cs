namespace Aula.Protocol;

/// <summary>
/// The upload buffer for the 240×135 TFT screen, built exactly like the vendor app (FUN_00422b50):
/// a 256-byte header, then RGB565 little-endian frames, padded with 0xFF to whole 4096-byte pages.
/// </summary>
public sealed class LcdImage
{
    public const int Width = 240;
    public const int Height = 135;
    public const int FrameBytes = Width * Height * 2;
    public const int HeaderBytes = 256;
    public const int PageSize = 4096;

    /// <summary>
    /// Hard limit from the vendor config (gif_maxframes). Larger uploads overflow the image slot
    /// and permanently overwrite the keyboard's menu graphics in flash.
    /// </summary>
    public const int MaxFrames = 141;

    /// <summary>The header stores each delay as one byte in 2 ms steps.</summary>
    public const int MaxFrameDelayMs = 255 * 2;

    private readonly byte[] _buffer;

    public int FrameCount { get; }
    public int PageCount => _buffer.Length / PageSize;
    public ReadOnlyMemory<byte> Buffer => _buffer;

    private LcdImage(byte[] buffer, int frameCount)
    {
        _buffer = buffer;
        FrameCount = frameCount;
    }

    /// <param name="frames">Each frame is 240×135 RGB565 little-endian (64,800 bytes).</param>
    /// <param name="delaysMs">Display time of each frame in milliseconds (2–510).</param>
    public static LcdImage Create(IReadOnlyList<byte[]> frames, IReadOnlyList<int> delaysMs)
    {
        if (frames.Count == 0)
            throw new AulaSafetyException("An image needs at least one frame.");
        if (frames.Count > MaxFrames)
            throw new AulaSafetyException($"{frames.Count} frames exceeds the keyboard's limit of {MaxFrames}; uploading it would corrupt the screen menus.");
        if (delaysMs.Count != frames.Count)
            throw new ArgumentException("Each frame needs exactly one delay.", nameof(delaysMs));
        for (var i = 0; i < frames.Count; i++)
        {
            if (frames[i].Length != FrameBytes)
                throw new AulaSafetyException($"Frame {i} is {frames[i].Length} bytes; frames must be exactly {Width}×{Height} RGB565 ({FrameBytes} bytes).");
        }

        var used = HeaderBytes + frames.Count * FrameBytes;
        var buffer = new byte[PagesFor(frames.Count) * PageSize];
        Array.Fill(buffer, (byte)0xFF);

        buffer[0] = (byte)frames.Count;
        for (var i = 0; i < frames.Count; i++)
            buffer[1 + i] = EncodeDelay(delaysMs[i]);

        var offset = HeaderBytes;
        foreach (var frame in frames)
        {
            frame.CopyTo(buffer, offset);
            offset += FrameBytes;
        }
        System.Diagnostics.Debug.Assert(offset == used);
        return new LcdImage(buffer, frames.Count);
    }

    /// <summary>A single solid-colour frame; the safest possible upload.</summary>
    public static LcdImage Solid(byte r, byte g, byte b)
    {
        var frame = new byte[FrameBytes];
        var pixel = Rgb565(r, g, b);
        for (var i = 0; i < frame.Length; i += 2)
        {
            frame[i] = (byte)pixel;
            frame[i + 1] = (byte)(pixel >> 8);
        }
        return Create([frame], [100]);
    }

    public static int PagesFor(int frameCount) =>
        (HeaderBytes + frameCount * FrameBytes + PageSize - 1) / PageSize;

    public ReadOnlySpan<byte> Page(int index) => _buffer.AsSpan(index * PageSize, PageSize);

    /// <summary>Vendor encoding: delay / 2, never below 1.</summary>
    public static byte EncodeDelay(int delayMs) => (byte)Math.Clamp(delayMs / 2, 1, 255);

    public static ushort Rgb565(byte r, byte g, byte b) =>
        (ushort)(((r & 0xF8) << 8) | ((g & 0xFC) << 3) | (b >> 3));
}
