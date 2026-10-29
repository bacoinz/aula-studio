using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Aula.Protocol;
using AulaStudio.Controls;
using AulaStudio.Localization;
using AulaStudio.Services;
using Wpf.Ui.Controls;
using TextBlock = Wpf.Ui.Controls.TextBlock;

namespace AulaStudio.Views.Pages;

public partial class LightingPage : Page
{
    public sealed record EffectItem(LightingEffect Effect, SymbolRegular Symbol)
    {
        public string Name => Loc.T($"Effect_{Effect}");
    }

    private static readonly EffectItem[] EffectList =
    [
        new(LightingEffect.Static, SymbolRegular.Lightbulb24),
        new(LightingEffect.Breath, SymbolRegular.HeartPulse24),
        new(LightingEffect.Rolling, SymbolRegular.ArrowCircleRight24),
        new(LightingEffect.Spectrum, SymbolRegular.ColorBackground24),
        new(LightingEffect.Colourful, SymbolRegular.Color24),
        new(LightingEffect.Flowing, SymbolRegular.Drop24),
        new(LightingEffect.Scrolling, SymbolRegular.ArrowRight24),
        new(LightingEffect.Rotating, SymbolRegular.ArrowRotateClockwise24),
        new(LightingEffect.Outward, SymbolRegular.ArrowExpand24),
        new(LightingEffect.Ripples, SymbolRegular.Target24),
        new(LightingEffect.Explode, SymbolRegular.Flash24),
        new(LightingEffect.Launch, SymbolRegular.Rocket24),
        new(LightingEffect.Pulsating, SymbolRegular.Pulse24),
        new(LightingEffect.Glittering, SymbolRegular.Sparkle24),
        new(LightingEffect.Falling, SymbolRegular.WeatherRain24),
        new(LightingEffect.Tilt, SymbolRegular.ArrowSwap24),
        new(LightingEffect.Shuttle, SymbolRegular.ArrowBidirectionalLeftRight24),
        new(LightingEffect.SingleOn, SymbolRegular.CursorClick24),
        new(LightingEffect.SingleOff, SymbolRegular.Cursor24),
        new(LightingEffect.Off, SymbolRegular.Prohibited24),
    ];

    private readonly DispatcherTimer _applyTimer = new() { Interval = TimeSpan.FromMilliseconds(300) };
    private bool _loading = true;

    public LightingPage()
    {
        InitializeComponent();
        _applyTimer.Tick += async (_, _) =>
        {
            _applyTimer.Stop();
            await ApplyEffectAsync();
        };

        Effects.ItemsSource = EffectList;
        LoadEffect(App.Settings.Lighting);
        EffectColor.ColorChanged += _ => OnEffectOptionChanged(this, new RoutedEventArgs());

        foreach (var (index, hex) in App.Settings.PerKeyColors)
            if (ColorSwatches.TryParse(hex, out var c)) Board.SetColor(index, c);
        KeyBrightness.Value = App.Settings.PerKeyBrightness;
        Board.SelectionChanged += UpdateSelectionCount;
        UpdateSelectionCount();

        BuildGuides();
        Loc.Instance.PropertyChanged += (_, _) =>
        {
            // Effect names are plain strings in the list; rebind to pick up the new language.
            var selected = Effects.SelectedItem;
            _loading = true;
            Effects.ItemsSource = null;
            Effects.ItemsSource = EffectList;
            Effects.SelectedItem = selected;
            _loading = false;
            UpdateSelectionCount();
            BuildGuides();
        };
        _loading = false;
    }

    // ---------- Effects ----------

    private void LoadEffect(LightingSettings s)
    {
        Effects.SelectedItem = EffectList.FirstOrDefault(e => e.Effect == s.Effect);
        EffectColor.Color = Color.FromRgb(s.Red, s.Green, s.Blue);
        Rainbow.IsChecked = s.Colorful;
        Brightness.Value = s.Brightness;
        Speed.Value = s.Speed;
        DirectionReverse.IsChecked = s.Direction == 1;
        DirectionNormal.IsChecked = s.Direction == 0;
        ShowControlsFor(s.Effect);
    }

    private LightingSettings CurrentEffect()
    {
        var c = EffectColor.Color;
        return new LightingSettings
        {
            Effect = (Effects.SelectedItem as EffectItem)?.Effect ?? LightingEffect.Rolling,
            Red = c.R, Green = c.G, Blue = c.B,
            Colorful = Rainbow.IsChecked == true,
            Brightness = (int)Brightness.Value,
            Speed = (int)Speed.Value,
            Direction = DirectionReverse.IsChecked == true ? 1 : 0,
        };
    }

    /// <summary>Shows only the controls the firmware uses for this effect (vendor config_func).</summary>
    private void ShowControlsFor(LightingEffect effect)
    {
        var controls = LightingEffects.ControlsFor(effect);
        static Visibility V(bool on) => on ? Visibility.Visible : Visibility.Collapsed;
        ColorRow.Visibility = V(controls.HasFlag(LightingControls.ColorAndColorful) || controls.HasFlag(LightingControls.RgbSliders));
        RainbowRow.Visibility = V(controls.HasFlag(LightingControls.ColorAndColorful));
        BrightnessRow.Visibility = V(controls.HasFlag(LightingControls.Brightness));
        SpeedRow.Visibility = V(controls.HasFlag(LightingControls.Speed));
        DirectionRow.Visibility = V(controls.HasFlag(LightingControls.DirectionLeftRight) || controls.HasFlag(LightingControls.DirectionUpDown));
        NoControls.Visibility = V(controls == LightingControls.None);
        EffectColor.IsEnabled = Rainbow.IsChecked != true;
    }

    private void OnEffectChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Effects.SelectedItem is not EffectItem item) return;
        ShowControlsFor(item.Effect);
        if (!_loading) ScheduleApply();
    }

    private void OnEffectOptionChanged(object sender, RoutedEventArgs e)
    {
        if (_loading || !IsInitialized) return;
        EffectColor.IsEnabled = Rainbow.IsChecked != true;
        ScheduleApply();
    }

    private void ScheduleApply()
    {
        _applyTimer.Stop();
        _applyTimer.Start();
    }

    private async Task ApplyEffectAsync()
    {
        var settings = CurrentEffect();
        App.Settings.Lighting = settings;
        App.Settings.Save();
        if (!App.Keyboard.IsConnected) return;
        try
        {
            await App.Keyboard.RunAsync(kb => kb.SetLighting(settings));
        }
        catch (AulaException ex)
        {
            Notify.Error(ex.Message);
        }
    }

    // ---------- Per key ----------

    private void UpdateSelectionCount() => SelectionCount.Text = Loc.T("PerKey_Selected", Board.Selected.Count);

    private void OnPaint(object sender, RoutedEventArgs e)
    {
        foreach (var i in Board.Selected) Board.SetColor(i, KeyColor.Color);
        SavePerKey();
    }

    private void OnErase(object sender, RoutedEventArgs e)
    {
        foreach (var i in Board.Selected) Board.SetColor(i, null);
        SavePerKey();
    }

    private void OnSelectAll(object sender, RoutedEventArgs e) => Board.SelectAll();

    private void OnClearSelection(object sender, RoutedEventArgs e) => Board.ClearSelection();

    private void OnPresetGaming(object sender, RoutedEventArgs e)
    {
        Board.ClearColors();
        var wasd = new HashSet<string> { "W", "A", "S", "D" };
        var arrows = new HashSet<string> { "↑", "↓", "←", "→" };
        foreach (var k in KeyboardLayout.Keys)
        {
            if (wasd.Contains(k.Name)) Board.SetColor(k.LightIndex, Color.FromRgb(255, 0, 0));
            else if (arrows.Contains(k.Name)) Board.SetColor(k.LightIndex, Color.FromRgb(0, 64, 255));
            else Board.SetColor(k.LightIndex, Color.FromRgb(40, 40, 40));
        }
        SavePerKey();
    }

    private void OnPresetRows(object sender, RoutedEventArgs e)
    {
        Color[] rows = [Color.FromRgb(255, 0, 0), Color.FromRgb(255, 128, 0), Color.FromRgb(255, 255, 0),
            Color.FromRgb(0, 255, 0), Color.FromRgb(0, 128, 255), Color.FromRgb(160, 0, 255)];
        foreach (var k in KeyboardLayout.Keys) Board.SetColor(k.LightIndex, rows[Math.Clamp(k.Row, 0, rows.Length - 1)]);
        SavePerKey();
    }

    private void OnPresetClear(object sender, RoutedEventArgs e)
    {
        Board.ClearColors();
        SavePerKey();
    }

    private void SavePerKey()
    {
        App.Settings.PerKeyColors = Board.Colors.ToDictionary(kv => kv.Key, kv => ColorSwatches.Hex(kv.Value));
        App.Settings.Save();
    }

    private async void OnApplyKeys(object sender, RoutedEventArgs e)
    {
        var colors = Board.Colors.ToDictionary(kv => kv.Key, kv => (kv.Value.R, kv.Value.G, kv.Value.B));
        var brightness = (int)KeyBrightness.Value;
        App.Settings.PerKeyBrightness = brightness;
        SavePerKey();
        ApplyKeys.IsEnabled = false;
        try
        {
            await App.Keyboard.RunAsync(kb => kb.SetPerKeyColors(colors, brightness));
            Notify.Success(Loc.T("PerKey_Applied"));
        }
        catch (AulaException ex)
        {
            Notify.Error(ex.Message);
        }
        finally
        {
            ApplyKeys.IsEnabled = true;
        }
    }

    // ---------- Zones guide ----------

    private void BuildGuides()
    {
        Fill(SidesGuide,
            ("Zones_Effect", ["Key_RShift"]),
            ("Zones_Color", ["/?"]),
            ("Zones_Brightness", ["Key_RAlt"]));
        Fill(BarGuide,
            ("Zones_Effect", ["Key_LShift"]),
            ("Zones_Color", ["Z"]),
            ("Zones_Brightness", ["Key_LAlt"]),
            ("Zones_Speed", ["Key_LCtrl"]),
            ("Zones_Power", ["B"]));
    }

    /// <summary>Rows of "action — [FN] + [key]"; keys starting with Key_ are localized.</summary>
    private void Fill(StackPanel panel, params (string Action, string[] Keys)[] rows)
    {
        while (panel.Children.Count > 1) panel.Children.RemoveAt(1);
        foreach (var (action, keys) in rows)
        {
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            row.Children.Add(new TextBlock { Text = Loc.T(action), Width = 220, VerticalAlignment = VerticalAlignment.Center });
            var combo = new StackPanel { Orientation = Orientation.Horizontal };
            combo.Children.Add(Keycap("FN"));
            foreach (var key in keys)
            {
                combo.Children.Add(new TextBlock { Text = "+", Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center });
                combo.Children.Add(Keycap(key.StartsWith("Key_") ? Loc.T(key) : key));
            }
            row.Children.Add(combo);
            panel.Children.Add(row);
        }
    }

    private Border Keycap(string text) => new()
    {
        Style = (Style)Resources["Keycap"],
        Child = new TextBlock { Text = text, FontFamily = new FontFamily("Segoe UI Semibold") },
    };
}
