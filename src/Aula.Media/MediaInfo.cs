using System.Globalization;
using System.Text.Json;

namespace Aula.Media;

public enum MediaKind { Image, Animation, Video }

/// <summary>What ffprobe reports about a source file.</summary>
public sealed record MediaInfo(string Path, MediaKind Kind, int Width, int Height, double DurationSeconds, double FrameRate)
{
    /// <summary>Approximate frame count over a range, used to pick a frame rate before converting.</summary>
    public int EstimateFrames(double start, double end) =>
        Kind == MediaKind.Image ? 1 : Math.Max(1, (int)Math.Round((end - start) * FrameRate));

    private static readonly HashSet<string> StillCodecs = ["png", "mjpeg", "bmp", "tiff", "webp_pipe", "jpegls"];

    public static async Task<MediaInfo> ProbeAsync(string path, CancellationToken cancel = default)
    {
        var exe = Ffmpeg.FfprobePath ?? throw new MediaException("ffprobe not found.");
        var result = await Ffmpeg.RunAsync(exe,
        [
            "-v", "error", "-select_streams", "v:0",
            "-show_entries", "stream=codec_name,width,height,avg_frame_rate,r_frame_rate,nb_frames:format=duration,format_name",
            "-of", "json", path,
        ], cancel);

        using var doc = JsonDocument.Parse(result.Stdout);
        var streams = doc.RootElement.GetProperty("streams");
        if (streams.GetArrayLength() == 0) throw new MediaException("The file has no picture.");
        var s = streams[0];
        var codec = s.GetProperty("codec_name").GetString() ?? "";
        var width = s.GetProperty("width").GetInt32();
        var height = s.GetProperty("height").GetInt32();
        var rate = ParseRate(Str(s, "avg_frame_rate")) is > 0 and var avg ? avg : ParseRate(Str(s, "r_frame_rate"));
        var duration = doc.RootElement.TryGetProperty("format", out var f) ? ParseDouble(Str(f, "duration")) : 0;
        var frames = int.TryParse(Str(s, "nb_frames"), out var n) ? n : 0;

        var kind = codec == "gif" || codec == "apng" || (codec == "webp" && frames > 1) ? MediaKind.Animation
            : StillCodecs.Contains(codec) || duration <= 0 || frames == 1 ? MediaKind.Image
            : MediaKind.Video;
        if (kind == MediaKind.Animation && frames == 1) kind = MediaKind.Image;
        if (kind != MediaKind.Image && rate <= 0) rate = frames > 0 && duration > 0 ? frames / duration : 10;

        return new MediaInfo(path, kind, width, height, kind == MediaKind.Image ? 0 : duration, kind == MediaKind.Image ? 0 : rate);
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) ? v.ValueKind == JsonValueKind.String ? v.GetString() : v.ToString() : null;

    private static double ParseDouble(string? s) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;

    private static double ParseRate(string? s)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        var parts = s.Split('/');
        if (parts.Length == 2 && double.TryParse(parts[1], CultureInfo.InvariantCulture, out var d) && d != 0)
            return ParseDouble(parts[0]) / d;
        return ParseDouble(s);
    }
}
