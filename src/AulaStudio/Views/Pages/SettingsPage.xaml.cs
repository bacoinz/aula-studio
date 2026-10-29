using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Aula.Protocol;
using AulaStudio.Localization;
using AulaStudio.Services;

namespace AulaStudio.Views.Pages;

public partial class SettingsPage : Page
{
    private static readonly (SleepTime Value, string Key)[] SleepChoices =
    [
        (SleepTime.Never, "Device_SleepNever"), (SleepTime.OneMinute, "Device_Sleep1"),
        (SleepTime.FiveMinutes, "Device_Sleep5"), (SleepTime.ThirtyMinutes, "Device_Sleep30"),
    ];

    private readonly DispatcherTimer _applyTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private bool _loading = true;

    public SettingsPage()
    {
        InitializeComponent();
        _applyTimer.Tick += async (_, _) =>
        {
            _applyTimer.Stop();
            await ApplyDeviceAsync();
        };
        FillDeviceChoices();
        var d = App.Settings.Device;
        SleepBox.SelectedIndex = Array.FindIndex(SleepChoices, c => c.Value == d.Sleep);
        ResponseSlider.Value = d.ResponseLevel;
        FnBox.SelectedIndex = d.FnToggle ? 1 : 0;
        NoWin.IsChecked = d.DisableWinKey;
        NoAltF4.IsChecked = d.DisableAltF4;
        NoAltTab.IsChecked = d.DisableAltTab;
        UpdateResponseText();
        Loc.Instance.PropertyChanged += (_, _) =>
        {
            _loading = true;
            FillDeviceChoices();
            UpdateResponseText();
            _loading = false;
        };

        foreach (var culture in Loc.Supported)
            LanguageBox.Items.Add(new ComboBoxItem { Content = Loc.T($"Language_{culture.TwoLetterISOLanguageName}"), Tag = culture });
        LanguageBox.SelectedIndex = Math.Max(0, Loc.Supported.ToList().IndexOf(Loc.Instance.Culture));
        AutoClockSwitch.IsChecked = App.Settings.AutoSyncClock;
        TraySwitch.IsChecked = App.Settings.CloseToTray;
        _loading = false;
    }

    private void FillDeviceChoices()
    {
        var sleep = SleepBox.SelectedIndex;
        var fn = FnBox.SelectedIndex;
        SleepBox.ItemsSource = SleepChoices.Select(c => Loc.T(c.Key)).ToList();
        FnBox.ItemsSource = new[] { Loc.T("Device_FnHold"), Loc.T("Device_FnToggle") };
        SleepBox.SelectedIndex = sleep;
        FnBox.SelectedIndex = fn;
    }

    private void UpdateResponseText() =>
        Device_ResponseDesc.Text = Loc.T("Device_ResponseDesc") + " " + Loc.T($"Device_ResponseLevel{(int)ResponseSlider.Value}");

    private DeviceOptions CurrentDevice() => new()
    {
        Sleep = SleepChoices[Math.Max(0, SleepBox.SelectedIndex)].Value,
        ResponseLevel = (int)ResponseSlider.Value,
        FnToggle = FnBox.SelectedIndex == 1,
        DisableWinKey = NoWin.IsChecked == true,
        DisableAltF4 = NoAltF4.IsChecked == true,
        DisableAltTab = NoAltTab.IsChecked == true,
    };

    private void OnDeviceChanged(object sender, RoutedEventArgs e)
    {
        if (_loading || !IsInitialized) return;
        UpdateResponseText();
        _applyTimer.Stop();
        _applyTimer.Start();
    }

    private async Task ApplyDeviceAsync()
    {
        var options = CurrentDevice();
        App.Settings.Device = options;
        App.Settings.Save();
        if (!App.Keyboard.IsConnected) return;
        try
        {
            await App.Keyboard.RunAsync(kb => kb.SetOptions(options));
            Notify.Success(Loc.T("Device_Applied"));
        }
        catch (AulaException ex)
        {
            Notify.Error(ex.Message);
        }
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
