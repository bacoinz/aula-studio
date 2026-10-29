using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AulaStudio.Controls;

/// <summary>Preset colour swatches plus a hex box for any other colour.</summary>
public sealed class ColorSwatches : WrapPanel
{
    /// <summary>The keyboard's own colour menu (red … white), plus a few extras.</summary>
    public static readonly Color[] Presets =
    [
        Color.FromRgb(255, 0, 0), Color.FromRgb(255, 128, 0), Color.FromRgb(255, 255, 0), Color.FromRgb(0, 255, 0),
        Color.FromRgb(0, 255, 255), Color.FromRgb(0, 64, 255), Color.FromRgb(160, 0, 255), Color.FromRgb(255, 0, 160),
        Color.FromRgb(255, 255, 255),
    ];

    private readonly List<Border> _swatches = [];
    private readonly Wpf.Ui.Controls.TextBox _hex = new() { Width = 110, MaxLength = 7, VerticalAlignment = VerticalAlignment.Center };
    private Color _color = Presets[0];
    private bool _updating;

    public event Action<Color>? ColorChanged;

    public ColorSwatches()
    {
        Orientation = Orientation.Horizontal;
        foreach (var c in Presets)
        {
            var swatch = new Border
            {
                Width = 28, Height = 28, CornerRadius = new CornerRadius(14), Margin = new Thickness(0, 0, 8, 8),
                Background = new SolidColorBrush(c), BorderThickness = new Thickness(3), Cursor = System.Windows.Input.Cursors.Hand,
                Tag = c, ToolTip = Hex(c),
            };
            swatch.MouseLeftButtonUp += (_, _) => SetColor((Color)swatch.Tag, notify: true);
            _swatches.Add(swatch);
            Children.Add(swatch);
        }
        _hex.Margin = new Thickness(4, 0, 0, 8);
        _hex.TextChanged += (_, _) =>
        {
            if (_updating) return;
            if (TryParse(_hex.Text, out var c)) SetColor(c, notify: true);
        };
        Children.Add(_hex);
        SetColor(_color, notify: false);
    }

    public Color Color
    {
        get => _color;
        set => SetColor(value, notify: false);
    }

    private void SetColor(Color c, bool notify)
    {
        _color = c;
        foreach (var s in _swatches)
        {
            if ((Color)s.Tag == c) s.SetResourceReference(Border.BorderBrushProperty, "TextFillColorPrimaryBrush");
            else s.BorderBrush = Brushes.Transparent;
        }
        _updating = true;
        if (!string.Equals(_hex.Text, Hex(c), StringComparison.OrdinalIgnoreCase)) _hex.Text = Hex(c);
        _updating = false;
        if (notify) ColorChanged?.Invoke(c);
    }

    public static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    public static bool TryParse(string? text, out Color color)
    {
        color = default;
        var t = text?.Trim().TrimStart('#') ?? "";
        if (t.Length != 6 || !int.TryParse(t, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v)) return false;
        color = Color.FromRgb((byte)(v >> 16), (byte)(v >> 8), (byte)v);
        return true;
    }
}
