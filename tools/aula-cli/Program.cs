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
            aula-cli zone <mode> [--trace]                 Light bar / side mode: flowing, red, yellow, green,
                                                           iceblue, blue, pink, white, neon, off
            aula-cli perkey-rainbow [--trace]              Per-key colours across the whole keyboard
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

            case "zone":
            {
                var mode = Enum.Parse<ZoneMode>(args[1], ignoreCase: true);
                using (var kb = Open(args))
                    kb.SetZone(mode);
                Console.WriteLine($"Zone mode {mode} (0x{(byte)mode:X2}) sent.");
                return 0;
            }

            case "perkey-rainbow":
            {
                // Every light index 1–123 gets a hue by position, so mistakes in the table are easy to spot.
                var colors = new Dictionary<int, (byte, byte, byte)>();
                for (var i = 1; i <= 123; i++) colors[i] = Hue(i / 123.0 * 360);
                using (var kb = Open(args))
                    kb.SetPerKeyColors(colors, brightness: 5);
                Console.WriteLine("Per-key rainbow sent.");
                return 0;
            }

            case "perkey":
            {
                // perkey 37:FF0000 56:FF0000 … — only the listed light indices light up.
                var colors = new Dictionary<int, (byte, byte, byte)>();
                foreach (var spec in args.Skip(1).Where(a => a.Contains(':')))
                {
                    var parts = spec.Split(':');
                    colors[int.Parse(parts[0])] = ParseColor(parts[1]);
                }
                using (var kb = Open(args))
                    kb.SetPerKeyColors(colors, brightness: 5);
                Console.WriteLine($"Per-key colours sent for {colors.Count} keys.");
                return 0;
            }

            case "keymap":
            {
                // keymap [--fn] 115:02000400 … — 4-byte actions as hex; no pairs restores every default.
                var layer = args.Contains("--fn") ? KeyLayer.Fn : KeyLayer.Normal;
                var actions = new Dictionary<int, KeyAction>();
                foreach (var spec in args.Skip(1).Where(a => a.Contains(':')))
                {
                    var parts = spec.Split(':');
                    var b = Convert.FromHexString(parts[1]);
                    actions[int.Parse(parts[0])] = new KeyAction(b[0], b[1], b[2], b[3]);
                }
                using (var kb = Open(args))
                    kb.SetKeyMap(layer, actions);
                Console.WriteLine($"{layer} layer remap sent for {actions.Count} keys.");
                return 0;
            }

            case "options":
            {
                // options win=1 altf4=0 alttab=0 fn=0 sleep=0 resp=1 — unnamed options take the vendor defaults.
                int Arg(string name, int fallback) =>
                    args.Skip(1).Where(a => a.StartsWith(name + "=")).Select(a => int.Parse(a[(name.Length + 1)..])).DefaultIfEmpty(fallback).First();
                var options = new DeviceOptions
                {
                    DisableWinKey = Arg("win", 0) == 1, DisableAltF4 = Arg("altf4", 0) == 1, DisableAltTab = Arg("alttab", 0) == 1,
                    FnToggle = Arg("fn", 0) == 1, Sleep = (SleepTime)Arg("sleep", 0), ResponseLevel = Arg("resp", 1),
                };
                using (var kb = Open(args))
                    kb.SetOptions(options);
                Console.WriteLine($"Options sent: {options}");
                return 0;
            }

            case "macro-test":
            {
                // Macro slot 0 types "ab"; bind a key to it with: keymap <idx>:06000000
                MacroEvent E(MacroEventKind k, byte code) => new(k, code, 30);
                using (var kb = Open(args))
                    kb.SetMacros([[E(MacroEventKind.KeyDown, 0x04), E(MacroEventKind.KeyUp, 0x04),
                        E(MacroEventKind.KeyDown, 0x05), E(MacroEventKind.KeyUp, 0x05)]]);
                Console.WriteLine("Macro slot 0 sent.");
                return 0;
            }

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

static (byte, byte, byte) Hue(double h)
{
    double x = 1 - Math.Abs(h / 60 % 2 - 1);
    var (r, g, b) = (int)(h / 60) switch
    {
        0 => (1.0, x, 0.0), 1 => (x, 1.0, 0.0), 2 => (0.0, 1.0, x),
        3 => (0.0, x, 1.0), 4 => (x, 0.0, 1.0), _ => (1.0, 0.0, x),
    };
    return ((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
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
