using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Aula.Media;

namespace AulaStudio.Controls;

/// <summary>
/// Shows a source frame with a draggable 16:9 window marking what reaches the screen.
/// The mouse wheel zooms the window; everything outside it is dimmed.
/// </summary>
public sealed class CropEditor : Grid
{
    public const double MaxZoom = 4;

    private readonly Image _image = new() { Stretch = Stretch.Uniform };
    private readonly Canvas _overlay = new() { Background = Brushes.Transparent, ClipToBounds = true };
    private readonly Path _dim = new() { Fill = new SolidColorBrush(Color.FromArgb(0x99, 0, 0, 0)), IsHitTestVisible = false };
    private readonly Rectangle _frame = new() { Stroke = Brushes.White, StrokeThickness = 2, Fill = Brushes.Transparent, Cursor = Cursors.SizeAll };

    private int _sourceWidth = 1, _sourceHeight = 1;
    private double _zoom = 1;
    private Point _centre;
    private Point? _dragStart;
    private Point _centreAtDragStart;

    public event Action? CropChanged;

    public CropEditor()
    {
        Children.Add(_image);
        Children.Add(_overlay);
        _overlay.Children.Add(_dim);
        _overlay.Children.Add(_frame);
        SizeChanged += (_, _) => Redraw();
        _frame.MouseLeftButtonDown += (_, e) =>
        {
            _dragStart = e.GetPosition(_overlay);
            _centreAtDragStart = _centre;
            _frame.CaptureMouse();
        };
        _frame.MouseMove += OnDrag;
        _frame.MouseLeftButtonUp += (_, _) =>
        {
            _dragStart = null;
            _frame.ReleaseMouseCapture();
            CropChanged?.Invoke();
        };
        _overlay.MouseWheel += (_, e) =>
        {
            Zoom = _zoom * (e.Delta > 0 ? 1.1 : 1 / 1.1);
            CropChanged?.Invoke();
        };
    }

    /// <summary>Hides the crop window (used in Fit mode, where the whole picture is shown).</summary>
    public bool ShowsCrop
    {
        get => _overlay.Visibility == Visibility.Visible;
        set => _overlay.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
    }

    public double Zoom
    {
        get => _zoom;
        set
        {
            _zoom = Math.Clamp(value, 1, MaxZoom);
            Clamp();
            Redraw();
        }
    }

    public void SetSource(ImageSource image, int sourceWidth, int sourceHeight, bool resetCrop)
    {
        _image.Source = image;
        _sourceWidth = sourceWidth;
        _sourceHeight = sourceHeight;
        if (resetCrop)
        {
            _zoom = 1;
            _centre = new Point(sourceWidth / 2.0, sourceHeight / 2.0);
        }
        Clamp();
        Redraw();
    }

    public CropRect Crop
    {
        get
        {
            var size = CropRect.Centered(_sourceWidth, _sourceHeight, _zoom);
            var x = (int)Math.Round(_centre.X - size.Width / 2.0);
            var y = (int)Math.Round(_centre.Y - size.Height / 2.0);
            return size with
            {
                X = Math.Clamp(x, 0, _sourceWidth - size.Width),
                Y = Math.Clamp(y, 0, _sourceHeight - size.Height),
            };
        }
        set
        {
            var full = CropRect.Centered(_sourceWidth, _sourceHeight);
            _zoom = Math.Clamp(full.Width / (double)Math.Max(1, value.Width), 1, MaxZoom);
            _centre = new Point(value.X + value.Width / 2.0, value.Y + value.Height / 2.0);
            Clamp();
            Redraw();
        }
    }

    private void OnDrag(object sender, MouseEventArgs e)
    {
        if (_dragStart is not { } start) return;
        var scale = Scale();
        if (scale <= 0) return;
        var delta = e.GetPosition(_overlay) - start;
        _centre = new Point(_centreAtDragStart.X + delta.X / scale, _centreAtDragStart.Y + delta.Y / scale);
        Clamp();
        Redraw();
    }

    private void Clamp()
    {
        var size = CropRect.Centered(_sourceWidth, _sourceHeight, _zoom);
        _centre = new Point(
            Math.Clamp(_centre.X, size.Width / 2.0, _sourceWidth - size.Width / 2.0),
            Math.Clamp(_centre.Y, size.Height / 2.0, _sourceHeight - size.Height / 2.0));
    }

    /// <summary>Screen pixels per source pixel for the uniformly stretched image.</summary>
    private double Scale() => Math.Min(ActualWidth / _sourceWidth, ActualHeight / _sourceHeight);

    private void Redraw()
    {
        var scale = Scale();
        if (scale <= 0 || double.IsInfinity(scale)) return;
        var shown = new Rect((ActualWidth - _sourceWidth * scale) / 2, (ActualHeight - _sourceHeight * scale) / 2,
            _sourceWidth * scale, _sourceHeight * scale);
        var c = Crop;
        var window = new Rect(shown.X + c.X * scale, shown.Y + c.Y * scale, c.Width * scale, c.Height * scale);

        _dim.Data = new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(shown), new RectangleGeometry(window));
        Canvas.SetLeft(_frame, window.X);
        Canvas.SetTop(_frame, window.Y);
        _frame.Width = window.Width;
        _frame.Height = window.Height;
    }
}
