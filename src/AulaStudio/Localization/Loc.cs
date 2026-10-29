using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Windows.Data;

namespace AulaStudio.Localization;

/// <summary>
/// Runtime-switchable strings. XAML binds through <see cref="TrExtension"/>; changing
/// <see cref="Culture"/> refreshes every bound text without reopening windows.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    // Declared before Instance: static initializers run in source order.
    public static readonly IReadOnlyList<CultureInfo> Supported = [new("pt-PT"), new("en")];

    private static readonly ResourceManager Resources = new("AulaStudio.Localization.Strings", typeof(Loc).Assembly);

    public static Loc Instance { get; } = new();

    private CultureInfo _culture = Supported[0];

    public CultureInfo Culture
    {
        get => _culture;
        set
        {
            if (Equals(_culture, value)) return;
            _culture = value;
            CultureInfo.CurrentUICulture = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Culture)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(Binding.IndexerName));
        }
    }

    public string this[string key] => Resources.GetString(key, _culture) ?? $"[{key}]";

    public static string T(string key) => Instance[key];

    public static string T(string key, params object[] args) => string.Format(Instance.Culture, Instance[key], args);

    public event PropertyChangedEventHandler? PropertyChanged;
}
