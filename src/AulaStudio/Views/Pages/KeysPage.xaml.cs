using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Aula.Protocol;
using AulaStudio.Localization;
using AulaStudio.Services;
using Wpf.Ui.Controls;
using MouseAction = Aula.Protocol.MouseAction;

namespace AulaStudio.Views.Pages;

public partial class KeysPage : Page
{
    private enum Kind { Default, Key, Mouse, Media, Macro, Disabled }

    private static readonly KeyActions.Choice<Kind>[] Kinds =
    [
        new(Kind.Default, "KeyType_Default"), new(Kind.Key, "KeyType_Key"), new(Kind.Mouse, "KeyType_Mouse"),
        new(Kind.Media, "KeyType_Media"), new(Kind.Macro, "KeyType_Macro"), new(Kind.Disabled, "KeyType_Disabled"),
    ];

    private KeyLayer _layer = KeyLayer.Normal;
    private LayoutKey? _key;
    private bool _loading;

    // Keys held in the capture box: modifiers so far, and whether a normal key completed the combination.
    private KeyModifiers _held;
    private bool _comboDone;

    public KeysPage()
    {
        InitializeComponent();
        foreach (var (nameKey, action) in KeyActions.Shortcuts)
        {
            var b = new Wpf.Ui.Controls.Button { Margin = new Thickness(0, 0, 6, 6), Tag = action, Padding = new Thickness(10, 4, 10, 4) };
            b.SetBinding(ContentControl.ContentProperty, new System.Windows.Data.Binding($"[{nameKey}]") { Source = Loc.Instance });
            b.Click += (_, _) => SetAction((KeyAction)b.Tag);
            ShortcutButtons.Children.Add(b);
        }
        Board.SelectionChanged += OnKeySelected;
        Loaded += (_, _) => Refresh();
        Loc.Instance.PropertyChanged += (_, _) => Refresh();
    }

    private Dictionary<int, uint> Map => App.Settings.KeyMapFor(_layer);

    private IReadOnlyList<string> MacroNames => App.Settings.Macros.Select(m => m.Name).ToList();

    private KeyAction ActionOf(LayoutKey key) =>
        Map.TryGetValue(key.KeyIndex, out var v) ? KeyAction.FromUInt32(v) : KeyAction.Default;

    /// <summary>The FN key itself can't be remapped, and some keys have no FN layer entry.</summary>
    private bool CanSet(LayoutKey key) => key.Name != "Fn" && !(_layer == KeyLayer.Fn && key.FnLayerDisabled);

    // ---------- Display ----------

    private void Refresh()
    {
        _loading = true;
        ActionType.ItemsSource = null;
        ActionType.ItemsSource = Kinds;
        MouseChoice.ItemsSource = null;
        MouseChoice.ItemsSource = KeyActions.Mouse;
        MediaChoice.ItemsSource = null;
        MediaChoice.ItemsSource = KeyActions.Media;
        ModeChoice.ItemsSource = null;
        ModeChoice.ItemsSource = KeyActions.PlayModes;
        MacroChoice.ItemsSource = App.Settings.Macros.Select((m, i) => $"{i + 1}. {m.Name}").ToList();
        _loading = false;

        Board.ClearCaptions();
        Board.ClearColors();
        var names = MacroNames;
        foreach (var key in KeyboardLayout.Keys)
        {
            var a = ActionOf(key);
            if (a.IsDefault) continue;
            Board.SetCaption(key.LightIndex, KeyActions.Caption(a, names));
            Board.SetColor(key.LightIndex, System.Windows.Media.Color.FromRgb(0x00, 0x78, 0xD4));
        }
        Summary.Text = Loc.T("Keys_Summary", Map.Count);
        LoadEditor();
    }

    private void OnLayerChanged(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized) return;
        _layer = LayerFn.IsChecked == true ? KeyLayer.Fn : KeyLayer.Normal;
        Refresh();
    }

    private void OnKeySelected()
    {
        var index = Board.Selected.FirstOrDefault();
        _key = KeyboardLayout.Keys.FirstOrDefault(k => k.LightIndex == index);
        LoadEditor();
    }

    private void LoadEditor()
    {
        if (_key is null)
        {
            NoKey.Text = Loc.T("Keys_PickKey");
            NoKey.Visibility = Visibility.Visible;
            Editor.Visibility = Visibility.Collapsed;
            return;
        }
        if (!CanSet(_key))
        {
            NoKey.Text = Loc.T("Keys_CannotSet", _key.Label);
            NoKey.Visibility = Visibility.Visible;
            Editor.Visibility = Visibility.Collapsed;
            return;
        }
        NoKey.Visibility = Visibility.Collapsed;
        Editor.Visibility = Visibility.Visible;

        var prefix = _layer == KeyLayer.Fn ? "FN + " : "";
        var label = _key.Label.Length > 0 ? _key.Label : _key.Name;
        KeyTitle.Text = Loc.T("Keys_KeyTitle", prefix + label);

        var a = ActionOf(_key);
        _loading = true;
        var kind = a.IsDefault ? Kind.Default
            : a == KeyAction.Disabled ? Kind.Disabled
            : a.Type switch { 0x01 => Kind.Mouse, 0x03 => Kind.Media, 0x06 => Kind.Macro, _ => Kind.Key };
        ActionType.SelectedItem = Kinds.First(k => k.Value == kind);
        CaptureText.Text = kind == Kind.Key ? KeyActions.KeyCombo((KeyModifiers)a.B1, a.B2) : Loc.T("Keys_CaptureHint");
        MouseChoice.SelectedItem = KeyActions.Mouse.FirstOrDefault(c => KeyAction.Mouse(c.Value) == a);
        MediaChoice.SelectedItem = KeyActions.Media.FirstOrDefault(c => a.Type == 0x03 && c.Value == (a.B1 | a.B2 << 8));
        MacroChoice.SelectedIndex = kind == Kind.Macro && a.B1 < App.Settings.Macros.Count ? a.B1 : -1;
        ModeChoice.SelectedItem = KeyActions.PlayModes.FirstOrDefault(c => kind == Kind.Macro && (byte)c.Value == a.B2)
                                  ?? KeyActions.PlayModes[0];
        RepeatCount.Value = kind == Kind.Macro ? Math.Max((int)a.B3, 1) : 2;
        _loading = false;
        ShowPanels(kind);
    }

    private void ShowPanels(Kind kind)
    {
        static Visibility V(bool on) => on ? Visibility.Visible : Visibility.Collapsed;
        KeyPanel.Visibility = V(kind == Kind.Key);
        MousePanel.Visibility = V(kind == Kind.Mouse);
        MediaPanel.Visibility = V(kind == Kind.Media);
        MacroPanel.Visibility = V(kind == Kind.Macro);
        NoMacros.Visibility = V(App.Settings.Macros.Count == 0);
        RepeatCount.Visibility = V((ModeChoice.SelectedItem as KeyActions.Choice<MacroPlayMode>)?.Value == MacroPlayMode.Repeat);
    }

    // ---------- Editing ----------

    private void SetAction(KeyAction a)
    {
        if (_key is null) return;
        if (a.IsDefault) Map.Remove(_key.KeyIndex);
        else Map[_key.KeyIndex] = a.ToUInt32();
        App.Settings.Save();
        Refresh();
    }

    private void OnActionTypeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || ActionType.SelectedItem is not KeyActions.Choice<Kind> choice) return;
        ShowPanels(choice.Value);
        switch (choice.Value)
        {
            case Kind.Default: SetAction(KeyAction.Default); break;
            case Kind.Disabled: SetAction(KeyAction.Disabled); break;
            case Kind.Mouse: SetAction(KeyAction.Mouse(MouseAction.Left)); break;
            case Kind.Media: SetAction(KeyAction.Consumer(KeyActions.Media[0].Value)); break;
            case Kind.Macro:
                if (App.Settings.Macros.Count > 0) SetAction(KeyAction.Macro(0));
                break;
            case Kind.Key:
                // Wait for the user to press something.
                CaptureText.Text = Loc.T("Keys_CaptureHint");
                Capture.Focus();
                break;
        }
    }

    private void OnChoiceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        if (sender == MouseChoice && MouseChoice.SelectedItem is KeyActions.Choice<MouseAction> m)
            SetAction(KeyAction.Mouse(m.Value));
        else if (sender == MediaChoice && MediaChoice.SelectedItem is KeyActions.Choice<ushort> c)
            SetAction(KeyAction.Consumer(c.Value));
        else if (sender == MacroChoice || sender == ModeChoice)
            SetMacroAction();
    }

    private void OnRepeatChanged(object sender, RoutedEventArgs e)
    {
        if (!_loading) SetMacroAction();
    }

    private void SetMacroAction()
    {
        if (MacroChoice.SelectedIndex < 0) return;
        var mode = (ModeChoice.SelectedItem as KeyActions.Choice<MacroPlayMode>)?.Value ?? MacroPlayMode.Once;
        var repeat = mode == MacroPlayMode.Repeat ? (int)(RepeatCount.Value ?? 1) : 1;
        SetAction(KeyAction.Macro(MacroChoice.SelectedIndex, mode, repeat));
    }

    private void OnResetKey(object sender, RoutedEventArgs e) => SetAction(KeyAction.Default);

    private void OnResetLayer(object sender, RoutedEventArgs e)
    {
        Map.Clear();
        App.Settings.Save();
        Refresh();
    }

    // ---------- Capture box ----------

    private void OnCaptureClick(object sender, MouseButtonEventArgs e) => Capture.Focus();

    private void OnCaptureFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        Capture.SetResourceReference(Border.BorderBrushProperty, "AccentFillColorDefaultBrush");
        CaptureText.Text = Loc.T("Keys_CaptureListening");
        _held = KeyModifiers.None;
        _comboDone = false;
    }

    private void OnCaptureBlur(object sender, KeyboardFocusChangedEventArgs e)
    {
        Capture.SetResourceReference(Border.BorderBrushProperty, "ControlStrokeColorDefaultBrush");
        if (_key is not null) LoadEditor();
    }

    private void OnCaptureKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        if (e.IsRepeat || HidKeys.FromKeyEvent(e) is not { } hid) return;
        if (HidKeys.IsModifier(hid))
        {
            _held |= (KeyModifiers)(1 << (hid - 0xE0));
            _comboDone = false;
            CaptureText.Text = KeyActions.KeyCombo(_held, 0) + " + …";
            return;
        }
        _comboDone = true;
        Commit(KeyAction.Key(hid, _held));
    }

    private void OnCaptureKeyUp(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        if (HidKeys.FromKeyEvent(e) is not { } hid) return;
        if (!HidKeys.IsModifier(hid))
        {
            // Print Screen only reports its release.
            if (!_comboDone && hid == 0x46) Commit(KeyAction.Key(hid, _held));
            return;
        }
        // Releasing modifiers without pressing another key maps to the modifiers alone.
        if (!_comboDone && _held != KeyModifiers.None) Commit(KeyAction.Key(0, _held));
        _held &= ~(KeyModifiers)(1 << (hid - 0xE0));
    }

    private void Commit(KeyAction a)
    {
        _comboDone = true;
        SetAction(a);
        Keyboard.ClearFocus();
    }

    // ---------- Sending ----------

    private async void OnApply(object sender, RoutedEventArgs e)
    {
        var layer = _layer;
        var actions = Map.ToDictionary(kv => kv.Key, kv => KeyAction.FromUInt32(kv.Value));
        // Keys that play macros need the macros on the keyboard too.
        var usesMacros = App.Settings.KeyMap.Values.Concat(App.Settings.FnKeyMap.Values)
            .Any(v => KeyAction.FromUInt32(v).Type == 0x06);
        var macros = App.Settings.Macros.Select(m => (IReadOnlyList<MacroEvent>)m.Events).ToList();
        ApplyButton.IsEnabled = false;
        try
        {
            await App.Keyboard.RunAsync(kb =>
            {
                if (usesMacros) kb.SetMacros(macros);
                kb.SetKeyMap(layer, actions);
            });
            Notify.Success(Loc.T("Keys_Applied"));
        }
        catch (Exception ex) when (ex is AulaException or ArgumentException)
        {
            Notify.Error(ex.Message);
        }
        finally
        {
            ApplyButton.IsEnabled = true;
        }
    }
}
