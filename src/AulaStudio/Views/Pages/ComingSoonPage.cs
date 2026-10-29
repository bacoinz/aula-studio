using System.Windows;
using System.Windows.Controls;
using AulaStudio.Localization;
using Wpf.Ui.Controls;
using TextBlock = Wpf.Ui.Controls.TextBlock;

namespace AulaStudio.Views.Pages;

/// <summary>Placeholder for sections built in later phases.</summary>
public abstract class ComingSoonPage : Page
{
    protected ComingSoonPage(string titleKey, SymbolRegular symbol)
    {
        // Pages don't pick up the theme's text colour on their own; children inherit this.
        SetResourceReference(ForegroundProperty, "TextFillColorPrimaryBrush");
        var title = new TextBlock { FontTypography = FontTypography.Title, Margin = new Thickness(0, 0, 0, 24) };
        title.SetBinding(System.Windows.Controls.TextBlock.TextProperty, Tr(titleKey));

        var heading = new TextBlock { FontTypography = FontTypography.Subtitle, HorizontalAlignment = HorizontalAlignment.Center };
        heading.SetBinding(System.Windows.Controls.TextBlock.TextProperty, Tr("ComingSoon_Title"));
        var body = new TextBlock { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 0) };
        body.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        body.SetBinding(System.Windows.Controls.TextBlock.TextProperty, Tr("ComingSoon_Body"));

        var empty = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        empty.Children.Add(new SymbolIcon(symbol) { FontSize = 48, Margin = new Thickness(0, 0, 0, 12) });
        empty.Children.Add(heading);
        empty.Children.Add(body);

        var root = new DockPanel { Margin = new Thickness(36, 24, 36, 24) };
        DockPanel.SetDock(title, Dock.Top);
        root.Children.Add(title);
        root.Children.Add(empty);
        Content = root;
    }

    private static System.Windows.Data.Binding Tr(string key) => new($"[{key}]") { Source = Loc.Instance };
}

public sealed class ScreenPage() : ComingSoonPage("Nav_Screen", SymbolRegular.Tv24);
public sealed class LightingPage() : ComingSoonPage("Nav_Lighting", SymbolRegular.Lightbulb24);
public sealed class KeysPage() : ComingSoonPage("Nav_Keys", SymbolRegular.Keyboard24);
public sealed class MacrosPage() : ComingSoonPage("Nav_Macros", SymbolRegular.Record24);
public sealed class ProfilesPage() : ComingSoonPage("Nav_Profiles", SymbolRegular.Folder24);
