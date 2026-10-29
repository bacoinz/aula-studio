using System.Windows.Controls;
using AulaStudio.Localization;
using Wpf.Ui.Tray.Controls;

namespace AulaStudio.Services;

/// <summary>System-tray icon so the app can keep the screen clock in sync while its window is closed.</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;

    public TrayIcon(Action open, Func<bool, Task> syncClock, Action exit)
    {
        var menu = new ContextMenu();
        menu.Items.Add(Item("Tray_Open", open));
        menu.Items.Add(Item("Tray_SyncClock", () => _ = syncClock(false)));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Tray_Exit", exit));

        _icon = new NotifyIcon
        {
            Icon = AppIcon.Create(32),
            TooltipText = Loc.T("AppTitle"),
            Menu = menu,
            MenuOnRightClick = true,
            FocusOnLeftClick = false,
        };
#pragma warning disable CS8622 // WPF-UI's RoutedNotifyIconEvent has mismatched nullability annotations.
        _icon.LeftDoubleClick += (_, _) => open();
        _icon.LeftClick += (_, _) => open();
#pragma warning restore CS8622
        _icon.Register();
    }

    private static MenuItem Item(string key, Action action)
    {
        var item = new MenuItem();
        item.SetBinding(HeaderedItemsControl.HeaderProperty,
            new System.Windows.Data.Binding($"[{key}]") { Source = Loc.Instance });
        item.Click += (_, _) => action();
        return item;
    }

    public void Dispose() => _icon.Unregister();
}
