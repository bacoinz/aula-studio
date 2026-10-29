using Aula.Media;
using Aula.Protocol;

namespace Aula.Media.Tests;

/// <summary>Integration tests: generate real files with ffmpeg, then convert them.</summary>
public sealed class ClipConverterTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aula-media-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private async Task<string> Generate(string name, params string[] args)
    {
        var path = Path.Combine(_dir, name);
        await Ffmpeg.RunAsync(Ffmpeg.FfmpegPath!, ["-v", "error", .. args, path], CancellationToken.None);
        return path;
    }

    [Fact]
    public async Task Gif_keeps_its_own_frame_timing()
    {
        // 12 frames at 5 fps → 200 ms each.
        var gif = await Generate("a.gif", "-f", "lavfi", "-i", "testsrc=size=320x180:rate=5:duration=2.4");
        var info = await MediaInfo.ProbeAsync(gif);
        Assert.Equal(MediaKind.Animation, info.Kind);

        var clip = await ClipConverter.ConvertAsync(info, new ClipOptions());
        Assert.Equal(12, clip.Frames.Count);
        Assert.All(clip.DelaysMs, d => Assert.Equal(200, d));
        Assert.All(clip.Frames, f => Assert.Equal(Clip.Rgb24FrameBytes, f.Length));
    }

    [Fact]
    public async Task Long_video_is_resampled_to_fit_141_frames()
    {
        var mp4 = await Generate("v.mp4", "-f", "lavfi", "-i", "testsrc=size=640x360:rate=30:duration=10", "-pix_fmt", "yuv420p");
        var info = await MediaInfo.ProbeAsync(mp4);
        Assert.Equal(MediaKind.Video, info.Kind);
        Assert.Equal(14.1, ClipConverter.AutoFps(info, 0, 10), 1);

        var clip = await ClipConverter.ConvertAsync(info, new ClipOptions());
        Assert.InRange(clip.Frames.Count, 138, LcdImage.MaxFrames);
        Assert.InRange(clip.TotalMs, 9500, 10500);
        Assert.Equal(clip.Frames.Count, clip.ToLcdImage().FrameCount);
    }

    [Fact]
    public async Task Range_and_fps_are_respected()
    {
        var mp4 = await Generate("r.mp4", "-f", "lavfi", "-i", "testsrc=size=640x360:rate=30:duration=10", "-pix_fmt", "yuv420p");
        var info = await MediaInfo.ProbeAsync(mp4);
        var clip = await ClipConverter.ConvertAsync(info, new ClipOptions(Start: 2, End: 4, Fps: 10));
        Assert.InRange(clip.Frames.Count, 19, 21);
        Assert.All(clip.DelaysMs, d => Assert.Equal(100, d));
    }

    [Fact]
    public async Task Never_more_than_141_frames_even_when_asked()
    {
        var mp4 = await Generate("m.mp4", "-f", "lavfi", "-i", "testsrc=size=320x180:rate=30:duration=10", "-pix_fmt", "yuv420p");
        var info = await MediaInfo.ProbeAsync(mp4);
        var clip = await ClipConverter.ConvertAsync(info, new ClipOptions(Fps: 30));
        Assert.Equal(LcdImage.MaxFrames, clip.Frames.Count);
    }

    [Fact]
    public async Task Still_image_becomes_one_frame()
    {
        var png = await Generate("i.png", "-f", "lavfi", "-i", "color=c=red:size=800x600", "-frames:v", "1");
        var info = await MediaInfo.ProbeAsync(png);
        Assert.Equal(MediaKind.Image, info.Kind);

        var clip = await ClipConverter.ConvertAsync(info, new ClipOptions(Mode: FitMode.Fit));
        Assert.Single(clip.Frames);
        // Fit mode letterboxes 4:3 into 16:9: the left edge is black, the centre red.
        var f = clip.Frames[0];
        var row = 67 * LcdImage.Width * 3;
        Assert.Equal(0, f[row]);
        Assert.True(f[row + 120 * 3] > 200);
    }

    [Fact]
    public void Centered_crop_has_screen_aspect()
    {
        var c = CropRect.Centered(1920, 1080);
        Assert.Equal(new CropRect(0, 0, 1920, 1080), c);
        var tall = CropRect.Centered(1080, 1920);
        Assert.Equal(1080, tall.Width);
        Assert.Equal(607, tall.Height);
    }
}
