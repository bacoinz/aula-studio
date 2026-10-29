using System.Globalization;
using Aula.Protocol;
using Aula.Protocol.Native;

// Hardware test harness for the F108 Pro. Commands are ordered from harmless to flash-writing.
return Run(args);

static int Run(string[] args)
{
    if (args.Length == 0)
    {
        Console.WriteLine("""
            aula-cli list                                  List the keyboard's HID interfaces
            aula-cli clock-sync [--trace]                  Set the screen clock to this PC's time
            aula-cli light <effect> [--color RRGGBB] [--brightness 0-5] [--speed 0-5]
                           [--direction 0|1] [--colorful] [--trace]
            aula-cli lcd-solid RRGGBB --yes [--trace]      Upload one solid frame (writes flash)
            aula-cli lcd-delay-test [--ms 500] --yes       Upload 10 coloured frames to check timing (writes flash)
            Effects: off, static, breath, rolling, ... or a number 0-19
            """);
        return 0;
    }

    try
    {
        switch (args[0])
        {
            case "list":
                foreach (var d in HidDeviceInfo.Enumerate(WindowsHidTransport.VendorId, WindowsHidTransport.ProductId))
                    Console.WriteLine($"page {d.UsagePage:X4} usage {d.Usage:X4}  in {d.InputReportLength,4}  out {d.OutputReportLength,4}  feat {d.FeatureReportLength,3}  {d.Path}");
                Console.WriteLine(WindowsHidTransport.Find() is null ? "Vendor interfaces: NOT found" : "Vendor interfaces: found");
                return 0;

            case "clock-sync":
                using (var kb = Open(args))
                    kb.SyncClock(DateTime.Now);
                Console.WriteLine($"Clock set to {DateTime.Now:yyyy-MM-dd HH:mm:ss}.");
                return 0;

            case "light":
                var settings = ParseLighting(args);
                using (var kb = Open(args))
                    kb.SetLighting(settings);
                Console.WriteLine($"Lighting set: {settings}");
                return 0;

            case "lcd-solid":
            {
                if (!ConfirmedFlashWrite(args)) return 2;
                var (r, g, b) = ParseColor(args[1]);
                Upload(LcdImage.Solid(r, g, b), args);
                return 0;
            }

            case "lcd-delay-test":
            {
                // Ten distinct colours, each meant to show for the given time: one full cycle should take 10× that.
                if (!ConfirmedFlashWrite(args)) return 2;
                var ms = int.Parse(Option(args, "--ms") ?? "500");
                uint[] colors = [0xFF0000, 0xFF8000, 0xFFFF00, 0x00FF00, 0x00FFFF, 0x0000FF, 0x8000FF, 0xFF00FF, 0xFFFFFF, 0x404040];
                var frames = colors.Select(c => SolidFrame((byte)(c >> 16), (byte)(c >> 8), (byte)c)).ToList();
                Upload(LcdImage.Create(frames, frames.Select(_ => ms).ToList()), args);
                Console.WriteLine($"Each colour should show for {ms} ms; a full cycle should take {ms * 10 / 1000.0:0.#} s.");
                return 0;
            }

            default:
                Console.Error.WriteLine($"Unknown command '{args[0]}'.");
                return 1;
        }
    }
    catch (AulaException e)
    {
        Console.Error.WriteLine($"Error: {e.Message}");
        return 1;
    }
}

static bool ConfirmedFlashWrite(string[] args)
{
    if (args.Contains("--yes")) return true;
    Console.Error.WriteLine("This writes the keyboard's flash. Re-run with --yes to confirm.");
    return false;
}

static void Upload(LcdImage image, string[] args)
{
    using var kb = Open(args);
    var last = -1;
    kb.UploadLcdImage(image, new Progress<double>(p =>
    {
        var pct = (int)(p * 100);
        if (pct / 25 != last / 25) Console.WriteLine($"  {pct}%");
        last = pct;
    }));
    Console.WriteLine($"Uploaded {image.FrameCount} frame(s) in {image.PageCount} pages.");
}

static byte[] SolidFrame(byte r, byte g, byte b) => LcdImage.Solid(r, g, b).Buffer.Slice(LcdImage.HeaderBytes, LcdImage.FrameBytes).ToArray();

static AulaKeyboard Open(string[] args)
{
    var kb = AulaKeyboard.OpenUsb();
    if (args.Contains("--trace"))
        kb.Trace += (kind, data) => Console.WriteLine($"  {kind,-14} {Convert.ToHexString(data, 0, Math.Min(16, data.Length))}");
    return kb;
}

static LightingSettings ParseLighting(string[] args)
{
    if (args.Length < 2) throw new AulaException("Name an effect.");
    var effect = int.TryParse(args[1], out var n)
        ? (LightingEffect)n
        : Enum.Parse<LightingEffect>(args[1], ignoreCase: true);
    var (r, g, b) = ParseColor(Option(args, "--color") ?? "FFFFFF");
    return new LightingSettings
    {
        Effect = effect, Red = r, Green = g, Blue = b,
        Brightness = int.Parse(Option(args, "--brightness") ?? "5"),
        Speed = int.Parse(Option(args, "--speed") ?? "3"),
        Direction = int.Parse(Option(args, "--direction") ?? "0"),
        Colorful = args.Contains("--colorful"),
    };
}

static string? Option(string[] args, string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

static (byte, byte, byte) ParseColor(string hex)
{
    var v = int.Parse(hex.TrimStart('#'), NumberStyles.HexNumber);
    return ((byte)(v >> 16), (byte)(v >> 8), (byte)v);
}
