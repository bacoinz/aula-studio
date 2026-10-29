using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Aula.Protocol;
using AulaStudio.Localization;
using AulaStudio.Services;
using Microsoft.Win32;
using Wpf.Ui.Controls;
using TextBlock = Wpf.Ui.Controls.TextBlock;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace AulaStudio.Views.Pages;

public partial class ProfilesPage : Page
{
    private bool _busy;

    public ProfilesPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Rebuild();
        Loc.Instance.PropertyChanged += (_, _) => Rebuild();
    }

    private static List<Profile> Profiles => App.Settings.Profiles;

    private void Rebuild()
    {
        List.Children.Clear();
        Empty.Visibility = Profiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var profile in Profiles) List.Children.Add(Card(profile));
    }

    private UIElement Card(Profile p)
    {
        var name = new TextBox { Text = p.Name, MinWidth = 220, Margin = new Thickness(0, 0, 0, 4) };
        name.LostFocus += (_, _) =>
        {
            var text = name.Text.Trim();
            if (text.Length == 0 || text == p.Name) return;
            p.Name = text;
            App.Settings.Save();
        };
        var details = new TextBlock { Text = Summary(p), FontSize = 12, TextWrapping = TextWrapping.Wrap };
        details.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");

        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        info.Children.Add(name);
        info.Children.Add(details);

        var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        buttons.Children.Add(Button("Profiles_Apply", SymbolRegular.Send24, () => ApplyAsync(p), primary: true));
        buttons.Children.Add(Button("Profiles_Update", SymbolRegular.ArrowSync24, () => UpdateAsync(p)));
        buttons.Children.Add(Button("Profiles_Export", SymbolRegular.ArrowExport24, () => Export(p)));
        buttons.Children.Add(Button("Profiles_Delete", SymbolRegular.Delete24, () => DeleteAsync(p)));

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(info);
        Grid.SetColumn(buttons, 1);
        grid.Children.Add(buttons);
        return new Card { Content = grid, Margin = new Thickness(0, 0, 0, 8), Padding = new Thickness(20, 14, 20, 14) };
    }

    private static Wpf.Ui.Controls.Button Button(string key, SymbolRegular icon, Func<Task> action, bool primary = false)
    {
        var b = new Wpf.Ui.Controls.Button
        {
            Content = Loc.T(key), Icon = new SymbolIcon(icon), Margin = new Thickness(8, 0, 0, 4),
            Appearance = primary ? ControlAppearance.Primary : ControlAppearance.Secondary,
        };
        b.Click += async (_, _) => await action();
        return b;
    }

    private static string Summary(Profile p)
    {
        var light = p.PerKeyActive ? Loc.T("Profiles_PerKey") : Loc.T($"Effect_{p.Lighting.Effect}");
        var keys = p.KeyMap.Count + p.FnKeyMap.Count;
        return Loc.T("Profiles_Summary", light, keys, p.Macros.Count, p.Saved.ToString("g", Loc.Instance.Culture));
    }

    // ---------- Actions ----------

    private void OnSaveCurrent(object sender, RoutedEventArgs e)
    {
        Profiles.Add(Profile.Capture(App.Settings, Loc.T("Profiles_DefaultName", Profiles.Count + 1)));
        App.Settings.Save();
        Rebuild();
        Notify.Success(Loc.T("Profiles_Saved"));
    }

    private async Task ApplyAsync(Profile p)
    {
        if (_busy) return;
        _busy = true;
        try
        {
            p.ApplyTo(App.Settings);
            App.Settings.Save();
            App.RaiseConfigurationReplaced();
            if (!App.Keyboard.IsConnected)
            {
                Notify.Success(Loc.T("Profiles_LoadedOffline", p.Name));
                return;
            }
            await App.Keyboard.RunAsync(p.Send);
            Notify.Success(Loc.T("Profiles_Applied", p.Name));
        }
        catch (Exception ex) when (ex is AulaException or ArgumentException)
        {
            Notify.Error(ex.Message);
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task UpdateAsync(Profile p)
    {
        if (!await ConfirmAsync("Profiles_UpdateTitle", Loc.T("Profiles_UpdateBody", p.Name), "Profiles_Update")) return;
        var fresh = Profile.Capture(App.Settings, p.Name);
        Profiles[Profiles.IndexOf(p)] = fresh;
        App.Settings.Save();
        Rebuild();
    }

    private Task Export(Profile p)
    {
        var dialog = new SaveFileDialog
        {
            FileName = string.Join("_", p.Name.Split(Path.GetInvalidFileNameChars())) + Profile.FileExtension,
            Filter = $"{Loc.T("Profiles_FileType")}|*{Profile.FileExtension}",
        };
        if (dialog.ShowDialog() == true)
        {
            p.Export(dialog.FileName);
            Notify.Success(Loc.T("Profiles_Exported"));
        }
        return Task.CompletedTask;
    }

    private void OnImport(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = $"{Loc.T("Profiles_FileType")}|*{Profile.FileExtension}" };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var p = Profile.Import(dialog.FileName);
            p.Device.Validate();
            p.Lighting.Validate();
            if (p.Name.Length == 0) p.Name = Path.GetFileNameWithoutExtension(dialog.FileName);
            Profiles.Add(p);
            App.Settings.Save();
            Rebuild();
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException or ArgumentException)
        {
            Notify.Error(Loc.T("Profiles_ImportFailed"));
        }
    }

    private async Task DeleteAsync(Profile p)
    {
        if (!await ConfirmAsync("Profiles_DeleteTitle", Loc.T("Profiles_DeleteBody", p.Name), "Profiles_Delete")) return;
        Profiles.Remove(p);
        App.Settings.Save();
        Rebuild();
    }

    private static async Task<bool> ConfirmAsync(string titleKey, string body, string actionKey)
    {
        var box = new Wpf.Ui.Controls.MessageBox
        {
            Title = Loc.T(titleKey), Content = body,
            PrimaryButtonText = Loc.T(actionKey), CloseButtonText = Loc.T("Common_Cancel"),
        };
        return await box.ShowDialogAsync() == Wpf.Ui.Controls.MessageBoxResult.Primary;
    }
}
