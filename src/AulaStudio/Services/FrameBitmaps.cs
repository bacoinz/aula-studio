using System.Windows.Media;
using System.Windows.Media.Imaging;
using Aula.Protocol;

namespace AulaStudio.Services;

/// <summary>Turns 240×135 screen frames into WPF bitmaps for previews.</summary>
public static class FrameBitmaps
{
    public static BitmapSource FromRgb24(byte[] rgb24)
    {
        var bitmap = BitmapSource.Create(LcdImage.Width, LcdImage.Height, 96, 96, PixelFormats.Rgb24, null,
            rgb24, LcdImage.Width * 3);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>What the screen will actually show: rounded to RGB565 and back.</summary>
    public static BitmapSource AsShownOnScreen(byte[] rgb24) =>
        FromRgb24(Rgb565ToRgb24(Aula.Media.Clip.ToRgb565(rgb24)));

    public static byte[] Rgb565ToRgb24(ReadOnlySpan<byte> rgb565)
    {
        var output = new byte[rgb565.Length / 2 * 3];
        for (int i = 0, o = 0; i + 1 < rgb565.Length; i += 2, o += 3)
        {
            var p = rgb565[i] | (rgb565[i + 1] << 8);
            int r = (p >> 11) & 0x1F, g = (p >> 5) & 0x3F, b = p & 0x1F;
            output[o] = (byte)((r << 3) | (r >> 2));
            output[o + 1] = (byte)((g << 2) | (g >> 4));
            output[o + 2] = (byte)((b << 3) | (b >> 2));
        }
        return output;
    }
}
