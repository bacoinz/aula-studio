using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using AulaStudio.Services;

namespace AulaStudio.Controls;

/// <summary>
/// The F108 Pro drawn from its layout. Click a key to toggle it, drag to select an area
/// (hold Ctrl to add to the current selection). Each key shows the colour assigned to it.
/// </summary>
public sealed class KeyboardView : Viewbox
{
    private const double PlateMargin = 12;

    private readonly Canvas _canvas = new() { Background = Brushes.Transparent };
    private readonly Dictionary<int, Border> _keys = [];
    private readonly Dictionary<int, Color> _colors = [];
    private readonly HashSet<int> _selected = [];
    private readonly Rectangle _band = new()
    {
        Stroke = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4)), StrokeThickness = 1,
        Fill = new SolidColorBrush(Color.FromArgb(0x33, 0x00, 0x78, 0xD4)), Visibility = Visibility.Collapsed,
        IsHitTestVisible = false,
    };
    private readonly Dictionary<int, string> _captions = [];
    private Point? _dragStart;

    /// <summary>One key at a time: clicks replace the selection and dragging does nothing.</summary>
    public bool SingleSelect { get; set; }

    public event Action? SelectionChanged;

    public IReadOnlyCollection<int> Selected => _selected;
    public IReadOnlyDictionary<int, Color> Colors => _colors;

    public KeyboardView()
    {
        Stretch = Stretch.Uniform;
        var keys = KeyboardLayout.Keys;
        var minX = keys.Min(k => k.Left) - PlateMargin;
        var minY = keys.Min(k => k.Top) - PlateMargin;
        _canvas.Width = keys.Max(k => k.Left + k.Width) - minX + PlateMargin;
        _canvas.Height = keys.Max(k => k.Top + k.Height) - minY + PlateMargin;

        var plate = new Border { CornerRadius = new CornerRadius(10), Width = _canvas.Width, Height = _canvas.Height };
        plate.SetResourceReference(Border.BackgroundProperty, "CardBackgroundFillColorSecondaryBrush");
        _canvas.Children.Add(plate);

        foreach (var key in keys)
        {
            var label = new TextBlock
            {
                Text = key.Label, FontSize = 9, HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis,
            };
            var border = new Border
            {
                Width = key.Width, Height = key.Height, CornerRadius = new CornerRadius(4),
                BorderThickness = new Thickness(2), Child = label, Tag = key, ToolTip = key.Label,
            };
            Canvas.SetLeft(border, key.Left - minX);
            Canvas.SetTop(border, key.Top - minY);
            _canvas.Children.Add(border);
            _keys[key.LightIndex] = border;
            Paint(key.LightIndex);
        }
        _canvas.Children.Add(_band);
        Child = _canvas;

        _canvas.MouseLeftButtonDown += (_, e) =>
        {
            _dragStart = e.GetPosition(_canvas);
            _canvas.CaptureMouse();
        };
        _canvas.MouseMove += (_, e) =>
        {
            if (_dragStart is not { } start || SingleSelect) return;
            var r = new Rect(start, e.GetPosition(_canvas));
            _band.Visibility = Visibility.Visible;
            Canvas.SetLeft(_band, r.X);
            Canvas.SetTop(_band, r.Y);
            _band.Width = r.Width;
            _band.Height = r.Height;
        };
        _canvas.MouseLeftButtonUp += (_, e) =>
        {
            if (_dragStart is not { } start) return;
            _canvas.ReleaseMouseCapture();
            _band.Visibility = Visibility.Collapsed;
            _dragStart = null;
            var end = e.GetPosition(_canvas);
            var additive = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
            if ((end - start).Length < 4) ToggleAt(end);
            else if (!SingleSelect) SelectArea(new Rect(start, end), additive);
        };
    }

    public void SetColor(int lightIndex, Color? color)
    {
        if (color is { } c) _colors[lightIndex] = c;
        else _colors.Remove(lightIndex);
        Paint(lightIndex);
    }

    /// <summary>Replaces a keycap's label (null restores it); the original stays in the tooltip.</summary>
    public void SetCaption(int lightIndex, string? caption)
    {
        if (caption is null) _captions.Remove(lightIndex);
        else _captions[lightIndex] = caption;
        if (!_keys.TryGetValue(lightIndex, out var border)) return;
        var key = (LayoutKey)border.Tag;
        ((TextBlock)border.Child).Text = caption ?? key.Label;
        ((TextBlock)border.Child).FontWeight = caption is null ? FontWeights.Normal : FontWeights.SemiBold;
    }

    public void ClearCaptions()
    {
        foreach (var i in _captions.Keys.ToList()) SetCaption(i, null);
    }

    public void ClearColors()
    {
        _colors.Clear();
        foreach (var i in _keys.Keys) Paint(i);
    }

    public void SelectAll()
    {
        _selected.UnionWith(_keys.Keys);
        RepaintAll();
        SelectionChanged?.Invoke();
    }

    public void ClearSelection()
    {
        _selected.Clear();
        RepaintAll();
        SelectionChanged?.Invoke();
    }

    private void ToggleAt(Point p)
    {
        var hit = _keys.FirstOrDefault(k => Bounds(k.Value).Contains(p));
        if (hit.Value is null) return;
        if (SingleSelect)
        {
            _selected.Clear();
            _selected.Add(hit.Key);
            RepaintAll();
        }
        else
        {
            if (!_selected.Remove(hit.Key)) _selected.Add(hit.Key);
            Paint(hit.Key);
        }
        SelectionChanged?.Invoke();
    }

    private void SelectArea(Rect area, bool additive)
    {
        if (!additive) _selected.Clear();
        foreach (var (index, border) in _keys)
            if (area.IntersectsWith(Bounds(border))) _selected.Add(index);
        RepaintAll();
        SelectionChanged?.Invoke();
    }

    private static Rect Bounds(Border b) => new(Canvas.GetLeft(b), Canvas.GetTop(b), b.Width, b.Height);

    private void RepaintAll()
    {
        foreach (var i in _keys.Keys) Paint(i);
    }

    private void Paint(int index)
    {
        var border = _keys[index];
        var label = (TextBlock)border.Child;
        if (_colors.TryGetValue(index, out var c))
        {
            border.Background = new SolidColorBrush(c);
            // Dark text on light colours, light text on dark ones.
            var luminance = 0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B;
            label.Foreground = luminance > 140 ? Brushes.Black : Brushes.White;
        }
        else
        {
            border.SetResourceReference(Border.BackgroundProperty, "ControlFillColorDefaultBrush");
            label.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        }
        if (_selected.Contains(index)) border.SetResourceReference(Border.BorderBrushProperty, "AccentFillColorDefaultBrush");
        else border.SetResourceReference(Border.BorderBrushProperty, "ControlStrokeColorDefaultBrush");
    }
}
