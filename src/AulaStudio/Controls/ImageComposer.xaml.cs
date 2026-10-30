using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Aula.Protocol;
using Microsoft.Win32;

namespace AulaStudio.Controls;

/// <summary>
/// Builds a still 240×135 image for the keyboard screen: text in any installed font over a colour
/// or a picture. Raises <see cref="Changed"/> (debounced) whenever the result changes.
/// </summary>
public partial class ImageComposer : UserControl
{
    private readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(150) };
    private BitmapSource? _background;
    private bool _ready;

    public event Action? Changed;

    public ImageComposer()
    {
        InitializeComponent();
        FontBox.ItemsSource = Fonts.SystemFontFamilies.OrderBy(f => f.Source).ToList();
        FontBox.SelectedItem = Fonts.SystemFontFamilies.FirstOrDefault(f => f.Source == "Segoe UI Black")
                               ?? Fonts.SystemFontFamilies.FirstOrDefault(f => f.Source == "Segoe UI");
        TextColor.Color = Colors.White;
        BackColor.Color = Color.FromRgb(0x00, 0x40, 0xFF);
        TextColor.ColorChanged += _ => OnChanged(this, new RoutedEventArgs());
        BackColor.ColorChanged += _ => OnChanged(this, new RoutedEventArgs());
        _debounce.Tick += (_, _) =>
        {
            _debounce.Stop();
            Changed?.Invoke();
        };
        _ready = true;
    }

    /// <summary>A short name for the library entry: the first line of text.</summary>
    public string Title => MessageText.Text.Split('\n').FirstOrDefault()?.Trim() is { Length: > 0 } t ? t : "";

    private void OnChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        _debounce.Stop();
        _debounce.Start();
    }

    private void OnPickImage(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = $"{Localization.Loc.T("Composer_ImageFiles")}|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp" };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(dialog.FileName);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();
            _background = image;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or FileFormatException)
        {
            Services.Notify.Error(ex.Message);
            return;
        }
        ClearImage.Visibility = DimPanel.Visibility = Visibility.Visible;
        OnChanged(this, e);
    }

    private void OnClearImage(object sender, RoutedEventArgs e)
    {
        _background = null;
        ClearImage.Visibility = DimPanel.Visibility = Visibility.Collapsed;
        OnChanged(this, e);
    }

    /// <summary>Draws the image at the screen's size and returns it as RGB24 pixels.</summary>
    public byte[] Render()
    {
        const int w = LcdImage.Width, h = LcdImage.Height;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(BackColor.Color), null, new Rect(0, 0, w, h));
            if (_background is { } bg)
            {
                // Fill the screen, cropping the centre, like the Screen page's "Fill" mode.
                var scale = Math.Max(w / (double)bg.PixelWidth, h / (double)bg.PixelHeight);
                double dw = bg.PixelWidth * scale, dh = bg.PixelHeight * scale;
                dc.DrawImage(bg, new Rect((w - dw) / 2, (h - dh) / 2, dw, dh));
                dc.DrawRectangle(new SolidColorBrush(Color.FromArgb((byte)(DimSlider.Value * 255), 0, 0, 0)), null, new Rect(0, 0, w, h));
            }

            var text = MessageText.Text.Replace("\r", "");
            if (text.Length > 0 && FontBox.SelectedItem is FontFamily font)
            {
                var typeface = new Typeface(font, FontStyles.Normal, BoldToggle.IsChecked == true ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal);
                FormattedText Format(Brush brush) => new(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                    typeface, SizeSlider.Value, brush, 1.0)
                {
                    MaxTextWidth = w - 8,
                    TextAlignment = AlignLeft.IsChecked == true ? TextAlignment.Left : AlignRight.IsChecked == true ? TextAlignment.Right : TextAlignment.Center,
                };
                var main = Format(new SolidColorBrush(TextColor.Color));
                var origin = new Point(4, (h - main.Height) * PositionSlider.Value);
                if (ShadowToggle.IsChecked == true)
                    dc.DrawText(Format(new SolidColorBrush(Color.FromArgb(160, 0, 0, 0))), origin + new Vector(2, 2));
                dc.DrawText(main, origin);
            }
        }

        var target = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);
        var bgra = new byte[w * h * 4];
        target.CopyPixels(bgra, w * 4, 0);
        var rgb = new byte[w * h * 3];
        for (int i = 0, j = 0; i < bgra.Length; i += 4, j += 3)
        {
            rgb[j] = bgra[i + 2];
            rgb[j + 1] = bgra[i + 1];
            rgb[j + 2] = bgra[i];
        }
        return rgb;
    }
}
