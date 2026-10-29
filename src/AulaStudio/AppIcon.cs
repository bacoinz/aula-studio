using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AulaStudio;

/// <summary>The app's own icon, drawn in code: a keyboard outline with a small screen.</summary>
public static class AppIcon
{
    public static ImageSource Create(int size = 64)
    {
        var accent = new SolidColorBrush(Color.FromRgb(0x00, 0x5F, 0xB8));
        var light = new SolidColorBrush(Color.FromRgb(0xE6, 0xF1, 0xFB));
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var s = size / 64.0;
            dc.DrawRoundedRectangle(accent, null, new Rect(2 * s, 14 * s, 60 * s, 38 * s), 8 * s, 8 * s);
            dc.DrawRoundedRectangle(light, null, new Rect(36 * s, 19 * s, 21 * s, 12 * s), 2 * s, 2 * s);
            for (var row = 0; row < 2; row++)
            for (var col = 0; col < 4; col++)
                dc.DrawRoundedRectangle(light, null, new Rect((8 + col * 7) * s, (20 + row * 8) * s, 5 * s, 5 * s), s, s);
            dc.DrawRoundedRectangle(light, null, new Rect(8 * s, 37 * s, 49 * s, 6 * s), s, s);
        }
        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }
}
