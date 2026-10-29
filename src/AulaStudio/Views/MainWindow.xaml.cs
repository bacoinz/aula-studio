using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using AulaStudio.Localization;
using AulaStudio.Services;
using AulaStudio.Views.Pages;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace AulaStudio.Views;

public partial class MainWindow : FluentWindow
{
    private static readonly Brush Connected = new SolidColorBrush(Color.FromRgb(0x0F, 0x7B, 0x0F));
    private static readonly Brush Disconnected = new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xA0));
    private static readonly Brush Busy = new SolidColorBrush(Color.FromRgb(0x00, 0x5F, 0xB8));

    public MainWindow()
    {
        InitializeComponent();
        Icon = AppIcon.Create(64);
        SystemThemeWatcher.Watch(this, WindowBackdropType.Mica);
        Navigation.SetPageProviderService(new PageProvider());
        Notify.Attach(Snackbar);

        App.Keyboard.PropertyChanged += (_, _) => UpdateStatus();
        Loc.Instance.PropertyChanged += (_, _) => UpdateStatus();
        UpdateStatus();
        Loaded += (_, _) => Navigation.Navigate(typeof(ScreenPage));
    }

    private void UpdateStatus()
    {
        var kb = App.Keyboard;
        StatusText.Text = Loc.T(kb.StatusKey);
        StatusDot.Fill = kb.IsBusy ? Busy : kb.IsConnected ? Connected : Disconnected;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (App.Settings.CloseToTray)
        {
            e.Cancel = true;
            Hide();
        }
        else
        {
            App.Current.RequestExit();
        }
    }

    /// <summary>Creates each page once and keeps it, so state survives switching sections.</summary>
    private sealed class PageProvider : INavigationViewPageProvider
    {
        private readonly Dictionary<Type, object> _pages = [];

        public object? GetPage(Type pageType)
        {
            if (!_pages.TryGetValue(pageType, out var page))
                _pages[pageType] = page = Activator.CreateInstance(pageType)!;
            return page;
        }
    }
}
