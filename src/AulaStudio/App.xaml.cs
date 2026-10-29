using System.Globalization;
using System.Windows;
using AulaStudio.Localization;
using AulaStudio.Services;
using AulaStudio.Views;

namespace AulaStudio;

public partial class App : Application
{
    public static AppSettings Settings { get; private set; } = new();
    public static KeyboardService Keyboard { get; } = new();

    private TrayIcon? _tray;
    private MainWindow? _window;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Settings = AppSettings.Load();
        Loc.Instance.Culture = Loc.Supported.FirstOrDefault(c => c.Name == Settings.Language) ?? Loc.Supported[0];
        Wpf.Ui.Appearance.ApplicationThemeManager.ApplySystemTheme();

        _window = new MainWindow();
        _window.Show();
        _tray = new TrayIcon(ShowWindow, SyncClockAsync, ExitApp);

        Keyboard.Connected += async () =>
        {
            if (Settings.AutoSyncClock) await SyncClockAsync(silent: true);
        };
        await Keyboard.StartAsync();
    }

    public void ShowWindow()
    {
        _window ??= new MainWindow();
        _window.Show();
        if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    public static async Task SyncClockAsync(bool silent = false)
    {
        try
        {
            await Keyboard.RunAsync(kb => kb.SyncClock(DateTime.Now));
            if (!silent) Notify.Success(Loc.T("Msg_ClockSynced"), DateTime.Now.ToString("HH:mm", CultureInfo.CurrentCulture));
        }
        catch (Exception ex) when (ex is Aula.Protocol.AulaException or System.IO.IOException)
        {
            if (!silent) Notify.Error(ex.Message);
        }
    }

    private void ExitApp()
    {
        _tray?.Dispose();
        Keyboard.Dispose();
        Shutdown();
    }

    public static new App Current => (App)Application.Current;

    public void RequestExit() => ExitApp();
}
