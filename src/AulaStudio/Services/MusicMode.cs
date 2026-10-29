using Aula.Protocol;
using Aula.Protocol.Native;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AulaStudio.Services;

/// <summary>
/// Runs music mode: listens to the PC's audio and streams per-key frames to the keyboard.
/// Lives for the whole app, so it keeps going while the user moves between pages.
/// </summary>
public sealed partial class MusicMode : ObservableObject
{
    private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(40);

    public MusicVisualizer Visualizer { get; } = new();

    [ObservableProperty]
    private bool _isRunning;

    public MusicMode()
    {
        // Another operation (an effect, a profile…) takes the keyboard over: music mode simply ends.
        App.Keyboard.StreamEnded += error =>
        {
            if (!IsRunning) return;
            Visualizer.Stop();
            IsRunning = false;
            if (error is not null) Notify.Error(error.Message);
        };
    }

    public async Task StartAsync()
    {
        if (IsRunning) return;
        Visualizer.Settings = App.Settings.Music;
        Visualizer.Start();
        IsRunning = true;
        try
        {
            await App.Keyboard.StartStreamAsync(Loop);
        }
        catch
        {
            Visualizer.Stop();
            IsRunning = false;
            throw;
        }
    }

    /// <summary>Stops and puts back the lighting the user had before.</summary>
    public async Task StopAsync()
    {
        if (!IsRunning) return;
        IsRunning = false;
        await App.Keyboard.StopStreamAsync();
        Visualizer.Stop();
        var s = App.Settings;
        var perKey = s.PerKeyActive
            ? s.PerKeyColors.Where(kv => kv.Value.Length == 7).ToDictionary(kv => kv.Key, kv => (
                Convert.ToByte(kv.Value[1..3], 16), Convert.ToByte(kv.Value[3..5], 16), Convert.ToByte(kv.Value[5..7], 16)))
            : null;
        await App.Keyboard.RunAsync(kb =>
        {
            if (perKey is not null) kb.SetPerKeyColors(perKey, s.PerKeyBrightness);
            else kb.SetLighting(s.Lighting);
        });
    }

    private void Loop(AulaKeyboard keyboard, CancellationToken cancel)
    {
        using var timer = new TimerResolution();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (!cancel.IsCancellationRequested)
        {
            var started = clock.Elapsed;
            var frame = Visualizer.Frame();
            // The firmware only updates the keys a frame lists (and drops out of live mode on an empty
            // one), so like the vendor send every key, dark ones as black.
            foreach (var key in KeyboardLayout.Keys) frame.TryAdd(key.LightIndex, (0, 0, 0));
            keyboard.SendRealtimeFrame(frame);
            var rest = FrameInterval - (clock.Elapsed - started);
            if (rest > TimeSpan.Zero) Thread.Sleep(rest);
        }
    }
}
