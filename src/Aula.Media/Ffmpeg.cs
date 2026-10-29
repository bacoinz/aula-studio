using System.Diagnostics;
using System.Text;

namespace Aula.Media;

/// <summary>Locates and runs ffmpeg/ffprobe. A copy next to the app wins over one on PATH.</summary>
public static class Ffmpeg
{
    public static string? FfmpegPath => Find("ffmpeg.exe");
    public static string? FfprobePath => Find("ffprobe.exe");
    public static bool IsAvailable => FfmpegPath is not null && FfprobePath is not null;

    private static string? Find(string exe)
    {
        var bundled = Path.Combine(AppContext.BaseDirectory, "ffmpeg", exe);
        if (File.Exists(bundled)) return bundled;
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            try
            {
                var candidate = Path.Combine(dir.Trim(), exe);
                if (File.Exists(candidate)) return candidate;
            }
            catch (ArgumentException)
            {
                // Malformed PATH entry.
            }
        }
        return null;
    }

    public sealed record Result(byte[] Stdout, string Stderr);

    /// <summary>Runs a tool, collecting stdout as bytes and stderr as text.</summary>
    public static async Task<Result> RunAsync(string exe, IEnumerable<string> args, CancellationToken cancel)
    {
        var psi = new ProcessStartInfo(exe)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var process = Process.Start(psi) ?? throw new MediaException($"Could not start {Path.GetFileName(exe)}.");
        using var stdout = new MemoryStream();
        var copy = process.StandardOutput.BaseStream.CopyToAsync(stdout, cancel);
        var stderr = process.StandardError.ReadToEndAsync(cancel);
        try
        {
            await Task.WhenAll(copy, stderr, process.WaitForExitAsync(cancel));
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            throw;
        }
        if (process.ExitCode != 0)
            throw new MediaException(LastLine(await stderr) ?? $"{Path.GetFileName(exe)} failed ({process.ExitCode}).");
        return new Result(stdout.ToArray(), await stderr);
    }

    private static string? LastLine(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault();
}

public sealed class MediaException(string message) : Exception(message);
