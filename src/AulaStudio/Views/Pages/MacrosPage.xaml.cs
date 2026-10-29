using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Aula.Protocol;
using AulaStudio.Localization;
using AulaStudio.Services;
using Wpf.Ui.Controls;
using MouseAction = Aula.Protocol.MouseAction;

namespace AulaStudio.Views.Pages;

public partial class MacrosPage : Page
{
    /// <summary>One row of the step list; edits to the pause write straight back to the macro.</summary>
    public sealed class Step(MacroEvent e, Action changed) : INotifyPropertyChanged
    {
        public MacroEvent Event { get; private set; } = e;

        public string Text => Event.Kind switch
        {
            MacroEventKind.KeyDown => $"↓  {HidKeys.Name(Event.Code)}",
            MacroEventKind.KeyUp => $"↑  {HidKeys.Name(Event.Code)}",
            MacroEventKind.MouseDown => $"↓  🖱 {MouseName(Event.Code)}",
            _ => $"↑  🖱 {MouseName(Event.Code)}",
        };

        public double? DelayMs
        {
            get => Event.DelayMs;
            set
            {
                var ms = (int)Math.Clamp(value ?? MacroEvent.MinDelayMs, MacroEvent.MinDelayMs, MacroEvent.MaxDelayMs);
                if (ms == Event.DelayMs) return;
                Event = Event with { DelayMs = ms };
                PropertyChanged?.Invoke(this, new(nameof(DelayMs)));
                changed();
            }
        }

        private static string MouseName(byte code) => code switch
        {
            1 => Loc.T("Mouse_Left"),
            2 => Loc.T("Mouse_Right"),
            _ => Loc.T("Mouse_Middle"),
        };

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private readonly ObservableCollection<Step> _steps = [];
    private readonly Stopwatch _clock = new();
    private MacroDefinition? _macro;
    private bool _recording;
    private bool _loading;

    public MacrosPage()
    {
        InitializeComponent();
        Steps.ItemsSource = _steps;
        Loaded += (_, _) => RefreshList();
        Unloaded += (_, _) => StopRecording();
        Loc.Instance.PropertyChanged += (_, _) =>
        {
            UpdateRecordButton();
            LoadSteps();
        };
        UpdateRecordButton();
    }

    private static List<MacroDefinition> Macros => App.Settings.Macros;

    // ---------- List ----------

    private void RefreshList()
    {
        var index = _macro is null ? -1 : Macros.IndexOf(_macro);
        _loading = true;
        List.ItemsSource = Macros.Select((m, i) => $"{i + 1}. {(m.Name.Length > 0 ? m.Name : Loc.T("Macros_Untitled"))}").ToList();
        List.SelectedIndex = index;
        _loading = false;
        DeleteMacro.IsEnabled = _macro is not null;
        Empty.Visibility = _macro is null ? Visibility.Visible : Visibility.Collapsed;
        Editor.Visibility = _macro is null ? Visibility.Collapsed : Visibility.Visible;
        UpdateMemory();
    }

    private void OnMacroSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        StopRecording();
        _macro = List.SelectedIndex >= 0 ? Macros[List.SelectedIndex] : null;
        _loading = true;
        NameBox.Text = _macro?.Name ?? "";
        _loading = false;
        LoadSteps();
        RefreshList();
    }

    private void OnNew(object sender, RoutedEventArgs e)
    {
        if (Macros.Count >= MacroSet.MaxMacros) return;
        StopRecording();
        _macro = new MacroDefinition { Name = Loc.T("Macros_DefaultName", Macros.Count + 1) };
        Macros.Add(_macro);
        App.Settings.Save();
        _loading = true;
        NameBox.Text = _macro.Name;
        _loading = false;
        LoadSteps();
        RefreshList();
        NameBox.Focus();
        NameBox.SelectAll();
    }

    private async void OnDelete(object sender, RoutedEventArgs e)
    {
        if (_macro is null) return;
        StopRecording();
        var confirm = new Wpf.Ui.Controls.MessageBox
        {
            Title = Loc.T("Macros_DeleteTitle"),
            Content = Loc.T("Macros_DeleteBody", _macro.Name),
            PrimaryButtonText = Loc.T("Macros_Delete"),
            CloseButtonText = Loc.T("Common_Cancel"),
        };
        if (await confirm.ShowDialogAsync() != Wpf.Ui.Controls.MessageBoxResult.Primary) return;
        var slot = Macros.IndexOf(_macro);
        Macros.RemoveAt(slot);
        // Keys pointing at later slots move down one; keys that played this macro go back to default.
        foreach (var map in new[] { App.Settings.KeyMap, App.Settings.FnKeyMap })
            foreach (var (key, value) in map.ToList())
            {
                var a = KeyAction.FromUInt32(value);
                if (a.Type != 0x06) continue;
                if (a.B1 == slot) map.Remove(key);
                else if (a.B1 > slot) map[key] = (a with { B1 = (byte)(a.B1 - 1) }).ToUInt32();
            }
        App.Settings.Save();
        _macro = null;
        LoadSteps();
        RefreshList();
    }

    private void OnNameChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading || _macro is null) return;
        _macro.Name = NameBox.Text.Trim();
        App.Settings.Save();
        RefreshList();
    }

    // ---------- Steps ----------

    private void LoadSteps()
    {
        _steps.Clear();
        if (_macro is null) return;
        foreach (var ev in _macro.Events) _steps.Add(new Step(ev, SaveSteps));
        UpdateMemory();
    }

    private void SaveSteps()
    {
        if (_macro is null) return;
        _macro.Events = _steps.Select(s => s.Event).ToList();
        App.Settings.Save();
        UpdateMemory();
    }

    private void Add(MacroEvent ev)
    {
        if (_macro is null) return;
        if (MacroSet.FreeBytes(Macros.Select(m => (IReadOnlyList<MacroEvent>)m.Events).ToList()) < 8)
        {
            StopRecording();
            Notify.Error(Loc.T("Macros_Full"));
            return;
        }
        _steps.Add(new Step(ev, SaveSteps));
        SaveSteps();
        Steps.ScrollIntoView(_steps[^1]);
    }

    private int DefaultDelayMs => (int)(DefaultDelay.Value ?? 30);

    private void OnAddClick(object sender, RoutedEventArgs e)
    {
        var code = MacroSet.MouseCode(Enum.Parse<MouseAction>((string)((FrameworkElement)sender).Tag));
        Add(new MacroEvent(MacroEventKind.MouseDown, code, DefaultDelayMs));
        Add(new MacroEvent(MacroEventKind.MouseUp, code, DefaultDelayMs));
    }

    private void OnRemoveStep(object sender, RoutedEventArgs e)
    {
        foreach (var s in Steps.SelectedItems.Cast<Step>().ToList()) _steps.Remove(s);
        SaveSteps();
    }

    private void OnClear(object sender, RoutedEventArgs e)
    {
        _steps.Clear();
        SaveSteps();
    }

    private void UpdateMemory()
    {
        var all = Macros.Select(m => (IReadOnlyList<MacroEvent>)m.Events).ToList();
        var total = MacroSet.BufferCapacity - Commands.ReportSize - MacroSet.MaxMacros * 4;
        var used = total - MacroSet.FreeBytes(all);
        Memory.Text = Loc.T("Macros_Memory", Math.Clamp(used * 100 / total, 0, 100), _steps.Count);
    }

    // ---------- Recording ----------

    private void OnRecord(object sender, RoutedEventArgs e)
    {
        if (_recording) StopRecording();
        else StartRecording();
    }

    private void StartRecording()
    {
        if (_macro is null || Window.GetWindow(this) is not { } window) return;
        _recording = true;
        window.PreviewKeyDown += OnRecordKeyDown;
        window.PreviewKeyUp += OnRecordKeyUp;
        _clock.Restart();
        Recording.IsOpen = true;
        // Something in the window must hold focus for key events to arrive; the page itself will do.
        Focusable = true;
        Focus();
        UpdateRecordButton();
    }

    private void StopRecording()
    {
        if (!_recording) return;
        _recording = false;
        if (Window.GetWindow(this) is { } window)
        {
            window.PreviewKeyDown -= OnRecordKeyDown;
            window.PreviewKeyUp -= OnRecordKeyUp;
        }
        Recording.IsOpen = false;
        UpdateRecordButton();
    }

    private void UpdateRecordButton()
    {
        RecordButton.Content = Loc.T(_recording ? "Macros_Stop" : "Macros_Record");
        RecordIcon.Symbol = _recording ? SymbolRegular.Stop24 : SymbolRegular.Record24;
    }

    private void OnRecordKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        if (!e.IsRepeat) Record(MacroEventKind.KeyDown, e);
    }

    private void OnRecordKeyUp(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        Record(MacroEventKind.KeyUp, e);
    }

    private void Record(MacroEventKind kind, KeyEventArgs e)
    {
        if (HidKeys.FromKeyEvent(e) is not { } hid) return;
        // The pause belongs to the previous step: it is the time until this input.
        if (_steps.Count > 0 && RecordDelays.IsChecked == true)
            _steps[^1].DelayMs = _clock.ElapsedMilliseconds;
        _clock.Restart();
        Add(new MacroEvent(kind, hid, DefaultDelayMs));
    }

    // ---------- Sending ----------

    private async void OnSend(object sender, RoutedEventArgs e)
    {
        StopRecording();
        var macros = Macros.Select(m => (IReadOnlyList<MacroEvent>)m.Events).ToList();
        SendButton.IsEnabled = false;
        try
        {
            await App.Keyboard.RunAsync(kb => kb.SetMacros(macros));
            Notify.Success(Loc.T("Macros_Sent"));
        }
        catch (Exception ex) when (ex is AulaException or ArgumentException)
        {
            Notify.Error(ex.Message);
        }
        finally
        {
            SendButton.IsEnabled = true;
        }
    }
}
