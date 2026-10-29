using System.Runtime.InteropServices;

namespace Aula.Protocol.Native;

/// <summary>
/// Raises the Windows timer resolution to 1 ms while held. Without it every short pause in a live
/// stream rounds up to ~15 ms and frames drop to about 5 per second.
/// </summary>
public sealed class TimerResolution : IDisposable
{
    [DllImport("winmm.dll")]
    private static extern uint timeBeginPeriod(uint period);

    [DllImport("winmm.dll")]
    private static extern uint timeEndPeriod(uint period);

    private bool _active;

    public TimerResolution() => _active = timeBeginPeriod(1) == 0;

    public void Dispose()
    {
        if (!_active) return;
        timeEndPeriod(1);
        _active = false;
    }
}
