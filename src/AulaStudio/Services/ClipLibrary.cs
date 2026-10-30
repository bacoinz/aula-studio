using System.IO;
using System.Text.Json;
using System.Windows.Media.Imaging;
using Aula.Media;
using Aula.Protocol;

namespace AulaStudio.Services;

/// <summary>Recent screen uploads, kept ready to resend: %APPDATA%\AulaStudio\library\&lt;id&gt;\.</summary>
public static class ClipLibrary
{
    private const int Keep = 10;
    private static string Root => Path.Combine(AppSettings.Folder, "library");

    public sealed record Entry(string Folder, string Name, DateTime Saved, int Frames, int TotalMs)
    {
        public string Thumbnail => Path.Combine(Folder, "thumb.png");
    }

    private sealed record Meta(string Name, DateTime Saved, int[] DelaysMs);

    public static void Save(string name, Clip clip)
    {
        var folder = Path.Combine(Root, DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
        Directory.CreateDirectory(folder);
        using (var file = File.Create(Path.Combine(folder, "frames.rgb565")))
            foreach (var frame in clip.Frames) file.Write(Clip.ToRgb565(frame));
        File.WriteAllText(Path.Combine(folder, "meta.json"),
            JsonSerializer.Serialize(new Meta(name, DateTime.Now, clip.DelaysMs.ToArray())));
        using (var thumb = File.Create(Path.Combine(folder, "thumb.png")))
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(FrameBitmaps.FromRgb24(ThumbnailFrame(clip))));
            encoder.Save(thumb);
        }
        Prune();
    }

    /// <summary>The brightest of up to ten evenly spaced frames: animations often start or fade on black.</summary>
    private static byte[] ThumbnailFrame(Clip clip)
    {
        var step = Math.Max(1, clip.Frames.Count / 10);
        return Enumerable.Range(0, clip.Frames.Count).Where(i => i % step == 0)
            .Select(i => clip.Frames[i])
            .MaxBy(f => f.Where((_, i) => i % 7 == 0).Sum(b => (long)b))!;
    }

    public static IReadOnlyList<Entry> List()
    {
        if (!Directory.Exists(Root)) return [];
        var entries = new List<Entry>();
        foreach (var dir in Directory.GetDirectories(Root).OrderDescending())
        {
            try
            {
                var meta = JsonSerializer.Deserialize<Meta>(File.ReadAllText(Path.Combine(dir, "meta.json")));
                if (meta is not null) entries.Add(new Entry(dir, meta.Name, meta.Saved, meta.DelaysMs.Length, meta.DelaysMs.Sum()));
            }
            catch (Exception e) when (e is IOException or JsonException)
            {
                // Skip unreadable entries.
            }
        }
        return entries;
    }

    public static Clip Load(Entry entry)
    {
        var meta = JsonSerializer.Deserialize<Meta>(File.ReadAllText(Path.Combine(entry.Folder, "meta.json")))
                   ?? throw new IOException("Damaged library entry.");
        var data = File.ReadAllBytes(Path.Combine(entry.Folder, "frames.rgb565"));
        var frames = new List<byte[]>();
        for (var o = 0; o + LcdImage.FrameBytes <= data.Length; o += LcdImage.FrameBytes)
            frames.Add(FrameBitmaps.Rgb565ToRgb24(data.AsSpan(o, LcdImage.FrameBytes)));
        return new Clip(frames, meta.DelaysMs);
    }

    public static void Delete(Entry entry) => Directory.Delete(entry.Folder, recursive: true);

    private static void Prune()
    {
        foreach (var dir in Directory.GetDirectories(Root).OrderDescending().Skip(Keep))
        {
            try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
        }
    }
}
