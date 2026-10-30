using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Aula.Protocol;
using AulaStudio.Controls;
using AulaStudio.Localization;
using AulaStudio.Services;
using Wpf.Ui.Controls;

namespace AulaStudio.Views.Pages;

public partial class MusicPage : Page
{
    public sealed record EffectItem(MusicEffect Effect, SymbolRegular Symbol)
    {
        public string Name => Loc.T($"Music_Effect{Effect}");
    }

    private static readonly EffectItem[] EffectList =
    [
        new(MusicEffect.Bars, SymbolRegular.DataHistogram24),
        new(MusicEffect.Pulse, SymbolRegular.HeartPulse24),
        new(MusicEffect.Ripple, SymbolRegular.Target24),
    ];

    private readonly DispatcherTimer _preview = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private bool _loading = true;

    public MusicPage()
    {
        InitializeComponent();
        Effects.ItemsSource = EffectList;
        var s = App.Settings.Music;
        Effects.SelectedItem = EffectList.First(e => e.Effect == s.Effect);
        ColorHeat.IsChecked = s.ColorMode == MusicColorMode.Heat;
        ColorRainbow.IsChecked = s.ColorMode == MusicColorMode.Rainbow;
        ColorSingle.IsChecked = s.ColorMode == MusicColorMode.Single;
        if (ColorSwatches.TryParse(s.Color, out var c)) SingleColor.Color = c;
        SingleColor.ColorChanged += _ => OnSettingChanged(this, new RoutedEventArgs());
        Sensitivity.Value = s.Sensitivity;
        _loading = false;
        UpdateSingleColor();

        _preview.Tick += (_, _) => ShowFrame();
        App.Music.PropertyChanged += (_, _) => UpdateState();
        App.Keyboard.PropertyChanged += (_, _) => UpdateState();
        Loaded += (_, _) => UpdateState();
        Unloaded += (_, _) => _preview.Stop();
        Loc.Instance.PropertyChanged += (_, _) =>
        {
            var selected = Effects.SelectedItem;
            _loading = true;
            Effects.ItemsSource = null;
            Effects.ItemsSource = EffectList;
            Effects.SelectedItem = selected;
            _loading = false;
            UpdateState();
        };
    }

    private void UpdateState()
    {
        var running = App.Music.IsRunning;
        StartButton.Content = Loc.T(running ? "Music_Stop" : "Music_Start");
        StartIcon.Symbol = running ? SymbolRegular.Stop24 : SymbolRegular.Play24;
        StartButton.IsEnabled = running || App.Keyboard.IsConnected;
        State.Text = Loc.T(running ? "Music_StateRunning" : App.Keyboard.IsConnected ? "Music_StateIdle" : "Music_StateNoKeyboard");
        // Over the dongle the firmware draws the music itself, so the page's effects don't reach the keyboard.
        if (App.Keyboard.Link == KeyboardLink.Dongle) State.Text += " " + Loc.T("Music_DongleNote");
        if (running && IsLoaded) _preview.Start();
        else
        {
            _preview.Stop();
            Preview.ClearColors();
        }
    }

    private void ShowFrame()
    {
        var frame = App.Music.Visualizer.LastFrame;
        foreach (var key in KeyboardLayout.Keys)
            Preview.SetColor(key.LightIndex, frame.TryGetValue(key.LightIndex, out var c) ? Color.FromRgb(c.R, c.G, c.B) : null);
    }

    private async void OnStartStop(object sender, RoutedEventArgs e)
    {
        StartButton.IsEnabled = false;
        try
        {
            if (App.Music.IsRunning) await App.Music.StopAsync();
            else await App.Music.StartAsync();
        }
        catch (Exception ex) when (ex is AulaException or System.Runtime.InteropServices.COMException or InvalidOperationException)
        {
            Notify.Error(ex.Message);
        }
        finally
        {
            UpdateState();
        }
    }

    private void UpdateSingleColor() => SingleColor.Visibility = ColorSingle.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

    private void OnSettingChanged(object sender, RoutedEventArgs e)
    {
        if (_loading || !IsInitialized) return;
        UpdateSingleColor();
        var s = App.Settings.Music;
        s.Effect = (Effects.SelectedItem as EffectItem)?.Effect ?? MusicEffect.Bars;
        s.ColorMode = ColorRainbow.IsChecked == true ? MusicColorMode.Rainbow
            : ColorSingle.IsChecked == true ? MusicColorMode.Single : MusicColorMode.Heat;
        s.Color = ColorSwatches.Hex(SingleColor.Color);
        s.Sensitivity = (int)Sensitivity.Value;
        App.Settings.Save();
        // The visualizer reads these on every frame, so a running session picks them up at once.
        App.Music.Visualizer.Settings = s;
    }
}
