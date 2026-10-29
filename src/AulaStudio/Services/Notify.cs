using Wpf.Ui;
using Wpf.Ui.Controls;

namespace AulaStudio.Services;

/// <summary>Short toast-style messages at the bottom of the main window.</summary>
public static class Notify
{
    private static readonly SnackbarService Snackbar = new();

    public static void Attach(SnackbarPresenter presenter) => Snackbar.SetSnackbarPresenter(presenter);

    public static void Success(string title, string message = "") =>
        Snackbar.Show(title, message, ControlAppearance.Success, new SymbolIcon(SymbolRegular.Checkmark24), TimeSpan.FromSeconds(3));

    public static void Error(string message) =>
        Snackbar.Show(Localization.Loc.T("Msg_Error"), message, ControlAppearance.Danger, new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(6));
}
