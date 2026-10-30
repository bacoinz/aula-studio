using NAudio.CoreAudioApi;
using NAudio.Dsp;
using NAudio.Wave;

namespace AulaStudio.Services;

public enum MusicEffect
{
    Bars,
    Pulse,
    Ripple,
}

public enum MusicColorMode
{
    Heat,
    Rainbow,
    Single,
}

/// <summary>Music mode choices, remembered between runs.</summary>
public sealed class MusicSettings
{
    public MusicEffect Effect { get; set; } = MusicEffect.Bars;
    public MusicColorMode ColorMode { get; set; } = MusicColorMode.Heat;
    public string Color { get; set; } = "#00C8FF";

    /// <summary>1–10; 5 suits normal listening volume.</summary>
    public int Sensitivity { get; set; } = 5;
}

/// <summary>
/// Listens to whatever the PC is playing (WASAPI loopback), splits it into frequency bands and
/// turns the levels into per-key colours for the F108 Pro's layout.
/// </summary>
public sealed class MusicVisualizer : IDisposable
{
    public const int Columns = 21;
    private const int FftLog2 = 11;
    private const int FftSize = 1 << FftLog2;
    private const int Rows = 6;

    private readonly object _lock = new();
    private readonly float[] _ring = new float[FftSize];
    private readonly float[] _levels = new float[Columns];
    private readonly (LayoutKey Key, int Column, int Height, double Distance)[] _keys;
    private WasapiLoopbackCapture? _capture;
    private int _ringPos;
    private int _sampleRate = 48000;
    private int _channels = 2;
    private double _hue;
    private double _peakDb = -100;

    private const double RangeDb = 24;
    private const double PeakFallPerFrame = 0.15;
    private const double SilenceDb = -45;

    public MusicSettings Settings { get; set; } = new();

    public MusicVisualizer()
    {
        var keys = KeyboardLayout.Keys;
        double minX = keys.Min(k => k.X), maxX = keys.Max(k => k.X);
        double cx = (minX + maxX) / 2, cy = keys.Average(k => k.Y);
        var maxDistance = keys.Max(k => Math.Sqrt(Math.Pow(k.X - cx, 2) + Math.Pow((k.Y - cy) * 2, 2)));
        _keys = keys.Select(k => (
            k,
            Math.Clamp((int)((k.X - minX) / (maxX - minX + 1) * Columns), 0, Columns - 1),
            Rows - 1 - Math.Clamp(k.Row, 0, Rows - 1),
            Math.Sqrt(Math.Pow(k.X - cx, 2) + Math.Pow((k.Y - cy) * 2, 2)) / maxDistance)).ToArray();
    }

    public bool IsListening => _capture is not null;

    /// <summary>The levels behind the last frame, which is all the dongle can send.</summary>
    public float[] LastLevels { get; private set; } = new float[Columns];

    /// <summary>The most recent frame, for the on-screen preview.</summary>
    public Dictionary<int, (byte R, byte G, byte B)> LastFrame { get; private set; } = [];

    public void Start()
    {
        if (_capture is not null) return;
        var device = new MMDeviceEnumerator().GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        _capture = new WasapiLoopbackCapture(device);
        _sampleRate = _capture.WaveFormat.SampleRate;
        _channels = _capture.WaveFormat.Channels;
        _capture.DataAvailable += OnData;
        _capture.StartRecording();
    }

    public void Stop()
    {
        if (_capture is null) return;
        _capture.DataAvailable -= OnData;
        _capture.StopRecording();
        _capture.Dispose();
        _capture = null;
        lock (_lock)
        {
            Array.Clear(_ring);
            Array.Clear(_levels);
        }
    }

    private void OnData(object? sender, WaveInEventArgs e)
    {
        // Loopback delivers 32-bit float frames; mix the channels down to mono.
        var frameBytes = 4 * _channels;
        lock (_lock)
        {
            for (var i = 0; i + frameBytes <= e.BytesRecorded; i += frameBytes)
            {
                float sum = 0;
                for (var c = 0; c < _channels; c++) sum += BitConverter.ToSingle(e.Buffer, i + c * 4);
                _ring[_ringPos] = sum / _channels;
                _ringPos = (_ringPos + 1) % FftSize;
            }
        }
    }

    /// <summary>Band levels 0–1, low to high frequency, smoothed (fast attack, slow fall).</summary>
    public float[] Analyse()
    {
        var fft = new Complex[FftSize];
        lock (_lock)
        {
            for (var i = 0; i < FftSize; i++)
            {
                var sample = _ring[(_ringPos + i) % FftSize];
                fft[i].X = (float)(sample * FastFourierTransform.HannWindow(i, FftSize));
            }
        }
        FastFourierTransform.FFT(true, FftLog2, fft);

        // Log-spaced bands from 40 Hz to 16 kHz.
        var db = new double[Columns];
        for (var b = 0; b < Columns; b++)
        {
            var lo = 40 * Math.Pow(400, b / (double)Columns);
            var hi = 40 * Math.Pow(400, (b + 1) / (double)Columns);
            int from = Math.Max(1, (int)(lo * FftSize / _sampleRate)), to = Math.Max(from + 1, (int)(hi * FftSize / _sampleRate));
            double peak = 0;
            for (var i = from; i < to && i < FftSize / 2; i++)
                peak = Math.Max(peak, Math.Sqrt(fft[i].X * fft[i].X + fft[i].Y * fft[i].Y));
            // Higher bands carry less energy in music; tilt them up so the bars look even.
            db[b] = 20 * Math.Log10(peak * (1 + b * 0.15) + 1e-9);
        }

        // Automatic level: follow the loudest band of the last few seconds, so the bars use their
        // whole height at any volume. Sensitivity moves the top of the scale down (taller bars).
        _peakDb = Math.Max(db.Max(), _peakDb - PeakFallPerFrame);
        var top = _peakDb - (Settings.Sensitivity - 5) * 2;
        var silent = _peakDb < SilenceDb;
        for (var b = 0; b < Columns; b++)
        {
            var level = silent ? 0 : (float)Math.Clamp((db[b] - (top - RangeDb)) / RangeDb, 0, 1);
            lock (_lock)
                _levels[b] = level > _levels[b] ? level : Math.Max(level, _levels[b] - 0.12f);
        }
        lock (_lock) return (float[])_levels.Clone();
    }

    /// <summary>The next frame of key colours by light index; keys left out are dark.</summary>
    public Dictionary<int, (byte R, byte G, byte B)> Frame()
    {
        var levels = Analyse();
        LastLevels = levels;
        _hue = (_hue + 0.004) % 1;
        var frame = new Dictionary<int, (byte, byte, byte)>();
        var bass = levels.Take(4).Average();
        var overall = levels.Average();

        foreach (var (key, column, height, distance) in _keys)
        {
            switch (Settings.Effect)
            {
                case MusicEffect.Bars:
                    var lit = levels[column] * Rows;
                    if (height < lit)
                        frame[key.LightIndex] = Color(height / (double)(Rows - 1), column / (double)Columns, 1);
                    break;
                case MusicEffect.Pulse:
                    if (bass > 0.05)
                        frame[key.LightIndex] = Color(bass, distance, bass);
                    break;
                case MusicEffect.Ripple:
                    if (distance < overall * 1.3)
                        frame[key.LightIndex] = Color(distance, distance, 1 - distance * 0.5);
                    break;
            }
        }
        LastFrame = frame;
        return frame;
    }

    /// <param name="heat">0–1 position for the green→red scale.</param>
    /// <param name="spread">0–1 position for the rainbow.</param>
    /// <param name="brightness">0–1 multiplier.</param>
    private (byte, byte, byte) Color(double heat, double spread, double brightness)
    {
        (double r, double g, double b) c = Settings.ColorMode switch
        {
            MusicColorMode.Heat => heat < 0.5 ? (heat * 2, 1, 0) : (1, 1 - (heat - 0.5) * 2, 0),
            MusicColorMode.Rainbow => Hsv((spread + _hue) % 1),
            _ => SingleColor(),
        };
        brightness = Math.Clamp(brightness, 0, 1);
        return ((byte)(c.r * 255 * brightness), (byte)(c.g * 255 * brightness), (byte)(c.b * 255 * brightness));
    }

    private (double, double, double) SingleColor()
    {
        var hex = Settings.Color;
        if (hex.Length != 7) return (0, 0.8, 1);
        return (Convert.ToByte(hex[1..3], 16) / 255.0, Convert.ToByte(hex[3..5], 16) / 255.0, Convert.ToByte(hex[5..7], 16) / 255.0);
    }

    private static (double, double, double) Hsv(double h)
    {
        var i = (int)(h * 6);
        var f = h * 6 - i;
        return (i % 6) switch
        {
            0 => (1, f, 0),
            1 => (1 - f, 1, 0),
            2 => (0, 1, f),
            3 => (0, 1 - f, 1),
            4 => (f, 0, 1),
            _ => (1, 0, 1 - f),
        };
    }

    public void Dispose() => Stop();
}
