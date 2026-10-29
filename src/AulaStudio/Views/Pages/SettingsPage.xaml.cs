using System.Windows;
using System.Windows.Controls;
using AulaStudio.Localization;

namespace AulaStudio.Views.Pages;

public partial class SettingsPage : Page
{
    private bool _loading = true;

    public SettingsPage()
    {
        InitializeComponent();
        foreach (var culture in Loc.Supported)
            LanguageBox.Items.Add(new ComboBoxItem { Content = Loc.T($"Language_{culture.TwoLetterISOLanguageName}"), Tag = culture });
        LanguageBox.SelectedIndex = Math.Max(0, Loc.Supported.ToList().IndexOf(Loc.Instance.Culture));
        AutoClockSwitch.IsChecked = App.Settings.AutoSyncClock;
        TraySwitch.IsChecked = App.Settings.CloseToTray;
        _loading = false;
    }

    private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || LanguageBox.SelectedItem is not ComboBoxItem { Tag: System.Globalization.CultureInfo culture }) return;
        Loc.Instance.Culture = culture;
        App.Settings.Language = culture.Name;
        App.Settings.Save();
    }

    private void OnAutoClockChanged(object sender, RoutedEventArgs e)
    {
        App.Settings.AutoSyncClock = AutoClockSwitch.IsChecked == true;
        App.Settings.Save();
    }

    private void OnTrayChanged(object sender, RoutedEventArgs e)
    {
        App.Settings.CloseToTray = TraySwitch.IsChecked == true;
        App.Settings.Save();
    }

    private async void OnSyncNow(object sender, RoutedEventArgs e) => await App.SyncClockAsync();
}
