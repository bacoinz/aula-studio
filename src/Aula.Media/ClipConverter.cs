using System.Globalization;
using System.Text.RegularExpressions;
using Aula.Protocol;

namespace Aula.Media;

public enum FitMode
{
    /// <summary>Fill the screen, cutting off what falls outside the crop window.</summary>
    Fill,
    /// <summary>Show the whole picture with black bars.</summary>
    Fit,
}

/// <param name="Crop">Crop window in source pixels (Fill mode); null centres the largest 16:9 window.</param>
/// <param name="Fps">Frames per second; null keeps a GIF's own timing, or picks the best rate for videos.</param>
public sealed record ClipOptions(
    double Start = 0,
    double? End = null,
    FitMode Mode = FitMode.Fill,
    CropRect? Crop = null,
    double? Fps = null);

public readonly record struct CropRect(int X, int Y, int Width, int Height)
{
    /// <summary>The largest 240:135 window centred in a source of the given size.</summary>
    public static CropRect Centered(int sourceWidth, int sourceHeight, double zoom = 1)
    {
        var aspect = LcdImage.Width / (double)LcdImage.Height;
        double w = sourceWidth, h = w / aspect;
        if (h > sourceHeight) { h = sourceHeight; w = h * aspect; }
        w /= zoom; h /= zoom;
        return new CropRect((int)((sourceWidth - w) / 2), (int)((sourceHeight - h) / 2), (int)w, (int)h);
    }
}

/// <summary>A clip ready for the screen: up to 141 frames of 240×135 RGB24 plus their display times.</summary>
public sealed class Clip(IReadOnlyList<byte[]> rgb24Frames, IReadOnlyList<int> delaysMs)
{
    public const int Rgb24FrameBytes = LcdImage.Width * LcdImage.Height * 3;

    public IReadOnlyList<byte[]> Frames { get; } = rgb24Frames;
    public IReadOnlyList<int> DelaysMs { get; } = delaysMs;
    public int TotalMs => DelaysMs.Sum();

    public LcdImage ToLcdImage() => LcdImage.Create(Frames.Select(ToRgb565).ToList(), DelaysMs);

    public static byte[] ToRgb565(byte[] rgb24)
    {
        var output = new byte[LcdImage.FrameBytes];
        for (int i = 0, o = 0; i < rgb24.Length; i += 3, o += 2)
        {
            var p = LcdImage.Rgb565(rgb24[i], rgb24[i + 1], rgb24[i + 2]);
            output[o] = (byte)p;
            output[o + 1] = (byte)(p >> 8);
        }
        return output;
    }
}

public static partial class ClipConverter
{
    /// <summary>Highest frame rate offered; the screen gains little above this.</summary>
    public const double MaxFps = 30;

    /// <summary>
    /// The frame rate used for a range: the source's own rate when it fits in 141 frames,
    /// otherwise the highest rate that does.
    /// </summary>
    public static double AutoFps(MediaInfo info, double start, double end)
    {
        var seconds = Math.Max(0.05, end - start);
        var fitting = LcdImage.MaxFrames / seconds;
        return Math.Round(Math.Min(Math.Min(info.FrameRate, MaxFps), fitting), 2);
    }

    /// <summary>Whether the source's own frame timing can be kept (animations that already fit).</summary>
    public static bool KeepsNativeTiming(MediaInfo info, ClipOptions o) =>
        o.Fps is null && info.Kind == MediaKind.Animation
        && info.EstimateFrames(o.Start, o.End ?? info.DurationSeconds) <= LcdImage.MaxFrames;

    public static async Task<Clip> ConvertAsync(MediaInfo info, ClipOptions o, CancellationToken cancel = default)
    {
        var exe = Ffmpeg.FfmpegPath ?? throw new MediaException("ffmpeg not found.");
        var end = Math.Min(o.End ?? info.DurationSeconds, info.DurationSeconds);
        var args = new List<string> { "-hide_banner", "-nostdin" };

        if (info.Kind != MediaKind.Image)
        {
            args.AddRange(["-ss", F(o.Start)]);
            if (end > o.Start) args.AddRange(["-t", F(end - o.Start)]);
        }
        args.AddRange(["-i", info.Path]);

        double? fps = info.Kind switch
        {
            MediaKind.Image => null,
            _ when KeepsNativeTiming(info, o) => null,
            _ => o.Fps ?? AutoFps(info, o.Start, end),
        };

        var filters = new List<string>();
        if (fps is { } rate) filters.Add($"fps={F(rate)}");
        if (o.Mode == FitMode.Fill)
        {
            var c = o.Crop ?? CropRect.Centered(info.Width, info.Height);
            filters.Add($"crop={c.Width}:{c.Height}:{c.X}:{c.Y}");
            filters.Add($"scale={LcdImage.Width}:{LcdImage.Height}:flags=lanczos");
        }
        else
        {
            filters.Add($"scale={LcdImage.Width}:{LcdImage.Height}:force_original_aspect_ratio=decrease:flags=lanczos");
            filters.Add($"pad={LcdImage.Width}:{LcdImage.Height}:(ow-iw)/2:(oh-ih)/2:color=black");
        }
        // Transparent pixels (GIF/PNG) become black instead of their hidden colour.
        filters.AddRange(["format=rgba", "premultiply=inplace=1", "format=rgb24", "showinfo"]);

        args.AddRange(["-vf", string.Join(',', filters), "-fps_mode", "passthrough",
            "-frames:v", LcdImage.MaxFrames.ToString(CultureInfo.InvariantCulture),
            "-f", "rawvideo", "-pix_fmt", "rgb24", "pipe:1"]);

        var result = await Ffmpeg.RunAsync(exe, args, cancel);
        var count = result.Stdout.Length / Clip.Rgb24FrameBytes;
        if (count == 0) throw new MediaException("No frames were decoded from this range.");

        var frames = new List<byte[]>(count);
        for (var i = 0; i < count; i++)
            frames.Add(result.Stdout.AsSpan(i * Clip.Rgb24FrameBytes, Clip.Rgb24FrameBytes).ToArray());

        return new Clip(frames, Delays(result.Stderr, count, fps));
    }

    /// <summary>One source frame as PNG, at most <paramref name="maxWidth"/> wide, for the crop editor.</summary>
    public static async Task<byte[]> GrabFrameAsync(MediaInfo info, double seconds, int maxWidth, CancellationToken cancel = default)
    {
        var exe = Ffmpeg.FfmpegPath ?? throw new MediaException("ffmpeg not found.");
        var args = new List<string> { "-hide_banner", "-nostdin", "-v", "error" };
        if (info.Kind != MediaKind.Image) args.AddRange(["-ss", F(Math.Max(0, seconds))]);
        args.AddRange(["-i", info.Path, "-frames:v", "1",
            "-vf", $"scale='min({maxWidth},iw)':-2,format=rgba,premultiply=inplace=1",
            "-f", "image2pipe", "-c:v", "png", "pipe:1"]);
        var result = await Ffmpeg.RunAsync(exe, args, cancel);
        if (result.Stdout.Length == 0) throw new MediaException("Could not read a frame at this time.");
        return result.Stdout;
    }

    /// <summary>Per-frame display time from showinfo: the frame's own duration, else the gap to the next one.</summary>
    private static List<int> Delays(string stderr, int count, double? fps)
    {
        if (count == 1) return [1000];
        if (fps is { } rate) return Enumerable.Repeat(Ms(1 / rate), count).ToList();

        var infos = ShowInfo().Matches(stderr)
            .Select(m => (Pts: D(m.Groups["pts"].Value), Dur: D(m.Groups["dur"].Value)))
            .ToList();
        var delays = new List<int>(count);
        for (var i = 0; i < count; i++)
        {
            double seconds = 0;
            if (i < infos.Count && infos[i].Dur > 0) seconds = infos[i].Dur;
            else if (i + 1 < infos.Count) seconds = infos[i + 1].Pts - infos[i].Pts;
            delays.Add(seconds > 0 ? Ms(seconds) : 100);
        }
        return delays;
    }

    private static int Ms(double seconds) =>
        Math.Clamp((int)Math.Round(seconds * 1000), 2, LcdImage.MaxFrameDelayMs);

    private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    private static double D(string s) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;

    [GeneratedRegex(@"pts_time:(?<pts>[-\d.]+)\s+duration:\s*\S+\s+duration_time:(?<dur>[-\d.]+)")]
    private static partial Regex ShowInfo();
}
