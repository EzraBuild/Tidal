using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Tidal.App.Interop;

namespace Tidal.App.Tray;

/// <summary>
/// Tidal's mark — a ring with a rising tide inside — drawn as vector geometry so the tray icon is
/// crisp at every DPI and can be tinted to match the taskbar.
/// </summary>
internal static class Logo
{
    // Designed on a 24×24 grid.
    private static readonly Geometry Ring = Freeze(new EllipseGeometry(new Point(12, 12), 9.25, 9.25));
    private static readonly Geometry Water = Freeze(new CombinedGeometry(
        GeometryCombineMode.Intersect,
        new EllipseGeometry(new Point(12, 12), 6.75, 6.75),
        Geometry.Parse("M 4,12.5 Q 8,8.5 12,12.5 T 20,12.5 V 21 H 4 Z")));

    /// <summary>The outer ring (stroked, 1.5 wide on the 24×24 grid), for drawing the mark in XAML.</summary>
    public static Geometry RingGeometry => Ring;

    /// <summary>The tide inside the ring (filled).</summary>
    public static Geometry WaterGeometry => Water;

    public static BitmapSource Render(int size, Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        var pen = new Pen(brush, 1.5);
        pen.Freeze();

        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.PushTransform(new ScaleTransform(size / 24.0, size / 24.0));
            context.DrawGeometry(null, pen, Ring);
            context.DrawGeometry(brush, null, Water);
        }

        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>Creates an HICON. The caller owns it and must release it with DestroyIcon.</summary>
    public static IntPtr CreateIconHandle(int size, Color color)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(Render(size, color)));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        byte[] png = stream.ToArray();

        // Since Vista, icon resources may be PNG-compressed, so the PNG bytes are a valid icon image.
        const uint IconFormatVersion = 0x00030000;
        return NativeMethods.CreateIconFromResourceEx(png, (uint)png.Length, true, IconFormatVersion, size, size, 0);
    }

    private static Geometry Freeze(Geometry geometry)
    {
        geometry.Freeze();
        return geometry;
    }
}
