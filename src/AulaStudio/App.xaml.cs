using System.Globalization;
using System.Windows;
using AulaStudio.Localization;
using AulaStudio.Services;
using AulaStudio.Views;
using AulaStudio.Views.Pages;

namespace AulaStudio;

public partial class App : Application
{
    public static AppSettings Settings { get; private set; } = new();
    public static KeyboardService Keyboard { get; } = new();

    private static MusicMode? _music;
    public static MusicMode Music => _music ??= new MusicMode();

    /// <summary>Raised when a profile replaces the current setup, so pages reload what they show.</summary>
    public static event Action? ConfigurationReplaced;

    public static void RaiseConfigurationReplaced() => ConfigurationReplaced?.Invoke();

    private TrayIcon? _tray;
    private MainWindow? _window;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Settings = AppSettings.Load();
        Loc.Instance.Culture = Loc.Supported.FirstOrDefault(c => c.Name == Settings.Language) ?? Loc.Supported[0];
        Wpf.Ui.Appearance.ApplicationThemeManager.ApplySystemTheme();
        ApplyAccent();
        // Keep the app's own accent when Windows switches between light and dark.
        Wpf.Ui.Appearance.ApplicationThemeManager.Changed += (_, _) => ApplyAccent();
        if (e.Args.FirstOrDefault(System.IO.File.Exists) is { } file) ScreenPage.PendingFile = file;

        _window = new MainWindow();
        _window.Show();
        _tray = new TrayIcon(ShowWindow, SyncClockAsync, ExitApp);

        Keyboard.Connected += async () =>
        {
            if (Settings.AutoSyncClock) await SyncClockAsync(silent: true);
        };
        await Keyboard.StartAsync();
    }

    /// <summary>
    /// A fixed blue accent: the system accent can be grey, which makes primary buttons look disabled.
    /// </summary>
    private static void ApplyAccent() =>
        Wpf.Ui.Appearance.ApplicationAccentColorManager.Apply(
            System.Windows.Media.Color.FromRgb(0x00, 0x78, 0xD4),
            Wpf.Ui.Appearance.ApplicationThemeManager.GetAppTheme());

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
