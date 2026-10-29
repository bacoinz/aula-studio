using System.Windows.Data;
using System.Windows.Markup;

namespace AulaStudio.Localization;

/// <summary><c>{l:Tr Nav_Screen}</c> — a live binding to a localized string.</summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class TrExtension(string key) : MarkupExtension
{
    public string Key { get; set; } = key;

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{Key}]") { Source = Loc.Instance, Mode = BindingMode.OneWay }.ProvideValue(serviceProvider);
}
