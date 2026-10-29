using System.Windows.Threading;
using Aula.Protocol;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AulaStudio.Services;

/// <summary>
/// Watches for the wired keyboard and runs operations on it one at a time, off the UI thread.
/// The keyboard is opened per operation, so unplugging it never leaves a stale handle behind.
/// </summary>
public sealed partial class KeyboardService : ObservableObject, IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly DispatcherTimer _timer;
    private bool _polling;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusKey))]
    private bool _isConnected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusKey))]
    private bool _isBusy;

    /// <summary>Raised on the UI thread when the keyboard appears.</summary>
    public event Action? Connected;

    public string StatusKey => IsBusy ? "Status_Busy" : IsConnected ? "Status_Connected" : "Status_Disconnected";

    public KeyboardService()
    {
        _timer = new DispatcherTimer { Interval = PollInterval };
        _timer.Tick += async (_, _) => await PollAsync();
    }

    public async Task StartAsync()
    {
        await PollAsync();
        _timer.Start();
    }

    private async Task PollAsync()
    {
        if (_polling) return;
        _polling = true;
        try
        {
            var present = await Task.Run(() => WindowsHidTransport.Find() is not null);
            if (present == IsConnected) return;
            IsConnected = present;
            if (present) Connected?.Invoke();
        }
        finally
        {
            _polling = false;
        }
    }

    /// <summary>Runs <paramref name="work"/> against a freshly opened keyboard.</summary>
    public async Task RunAsync(Action<AulaKeyboard> work)
    {
        if (!IsConnected) throw new AulaException(Localization.Loc.T("Msg_NotConnected"));
        await _gate.WaitAsync();
        IsBusy = true;
        try
        {
            await Task.Run(() =>
            {
                using var keyboard = AulaKeyboard.OpenUsb();
                work(keyboard);
            });
        }
        finally
        {
            IsBusy = false;
            _gate.Release();
        }
    }

    public void Dispose()
    {
        _timer.Stop();
        _gate.Dispose();
    }
}
