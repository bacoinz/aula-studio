using System.IO;
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
    private static readonly TimeSpan BatteryInterval = TimeSpan.FromSeconds(15);

    private DateTime _lastBatteryAsk;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly DispatcherTimer _timer;
    private bool _polling;
    private CancellationTokenSource? _stream;
    private Task? _streamTask;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusKey))]
    private bool _isConnected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusKey))]
    private bool _isBusy;

    /// <summary>The link last seen, which can be the dongle even while the keyboard itself doesn't answer.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusKey))]
    private KeyboardLink _link;

    /// <summary>Battery percentage, known only over the dongle.</summary>
    [ObservableProperty]
    private int? _battery;

    public bool SupportsScreen => Link == KeyboardLink.Usb;

    /// <summary>Raised on the UI thread when the keyboard appears.</summary>
    public event Action? Connected;

    /// <summary>Raised when a live stream ends, whether stopped or broken off by an error (then non-null).</summary>
    public event Action<Exception?>? StreamEnded;

    [ObservableProperty]
    private bool _isStreaming;

    public string StatusKey => IsBusy ? "Status_Busy"
        : IsConnected ? Link == KeyboardLink.Dongle ? "Status_ConnectedDongle" : "Status_Connected"
        : Link == KeyboardLink.Dongle ? "Status_DongleNoKeyboard" : "Status_Disconnected";

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
            var link = await Task.Run(AulaConnection.Detect);
            Link = link;
            var present = link == KeyboardLink.Usb || (link == KeyboardLink.Dongle && await AskDongleAsync());
            if (link != KeyboardLink.Dongle) Battery = null;
            if (present == IsConnected) return;
            IsConnected = present;
            if (present) Connected?.Invoke();
        }
        finally
        {
            _polling = false;
        }
    }

    /// <summary>
    /// The dongle is present whether or not the keyboard is on, so ask the keyboard itself, as often as the vendor (15 s).
    /// Until that answer changes, and while something else uses the keyboard, the last one stands.
    /// </summary>
    private async Task<bool> AskDongleAsync()
    {
        if (IsConnected && DateTime.UtcNow - _lastBatteryAsk < BatteryInterval) return true;
        if (IsStreaming || !await _gate.WaitAsync(0)) return IsConnected;
        try
        {
            _lastBatteryAsk = DateTime.UtcNow;
            Battery = await Task.Run(() =>
            {
                using var dongle = AulaDongle.Open();
                return dongle.ReadBattery();
            });
            return Battery is not null;
        }
        catch (Exception e) when (e is AulaException or IOException)
        {
            Battery = null;
            return false;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Runs <paramref name="work"/> against a freshly opened keyboard. Stops any live stream first.</summary>
    public async Task RunAsync(Action<IAulaKeyboard> work)
    {
        if (!IsConnected) throw new AulaException(Localization.Loc.T("Msg_NotConnected"));
        await StopStreamAsync();
        await _gate.WaitAsync();
        IsBusy = true;
        try
        {
            await Task.Run(() =>
            {
                using var keyboard = AulaConnection.Open();
                work(keyboard);
            });
        }
        finally
        {
            IsBusy = false;
            _gate.Release();
        }
    }

    /// <summary>
    /// Keeps the keyboard open and runs <paramref name="loop"/> on a background thread until stopped,
    /// for live frames (music mode). Any other operation stops the stream before it runs.
    /// </summary>
    public async Task StartStreamAsync(Action<IAulaKeyboard, CancellationToken> loop)
    {
        if (!IsConnected) throw new AulaException(Localization.Loc.T("Msg_NotConnected"));
        await StopStreamAsync();
        await _gate.WaitAsync();
        var cts = new CancellationTokenSource();
        _stream = cts;
        IsStreaming = true;
        var dispatcher = Dispatcher.CurrentDispatcher;
        _streamTask = Task.Run(() =>
        {
            Exception? error = null;
            try
            {
                using var keyboard = AulaConnection.Open();
                loop(keyboard, cts.Token);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                // Anything else would fault the task unseen; report it like a device error.
                error = e;
            }
            finally
            {
                _gate.Release();
            }
            dispatcher.BeginInvoke(() =>
            {
                if (_stream == cts)
                {
                    _stream = null;
                    IsStreaming = false;
                }
                cts.Dispose();
                StreamEnded?.Invoke(error);
            });
        });
    }

    public async Task StopStreamAsync()
    {
        if (_stream is not { } cts || _streamTask is not { } task) return;
        _stream = null;
        IsStreaming = false;
        cts.Cancel();
        await task;
    }

    public void Dispose()
    {
        _timer.Stop();
        _gate.Dispose();
    }
}
