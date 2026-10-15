using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Tidal.Core.Adaptive;

namespace Tidal.App.Controls;

/// <summary>
/// Today's adaptive brightness under "follow the sun", midnight to midnight: the level curve,
/// sunrise and sunset, and a marker for now. Uses the same maths as the sun source.
/// </summary>
public sealed class DayCurve : FrameworkElement
{
    private const double AxisHeight = 22;
    private const int SamplesPerDay = 24 * 6; // every ten minutes

    public static readonly DependencyProperty LatitudeProperty = DependencyProperty.Register(
        nameof(Latitude), typeof(double?), typeof(DayCurve), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LongitudeProperty = DependencyProperty.Register(
        nameof(Longitude), typeof(double?), typeof(DayCurve), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CurveBrushProperty = DependencyProperty.Register(
        nameof(CurveBrush), typeof(Brush), typeof(DayCurve), new FrameworkPropertyMetadata(Brushes.Orange, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TextBrushProperty = DependencyProperty.Register(
        nameof(TextBrush), typeof(Brush), typeof(DayCurve), new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty GridBrushProperty = DependencyProperty.Register(
        nameof(GridBrush), typeof(Brush), typeof(DayCurve), new FrameworkPropertyMetadata(Brushes.LightGray, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MarkerFillBrushProperty = DependencyProperty.Register(
        nameof(MarkerFillBrush), typeof(Brush), typeof(DayCurve), new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender));

    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromMinutes(1) };

    /// <summary>Inside of the "now" marker (the background colour, so the ring reads on the curve).</summary>
    public Brush MarkerFillBrush { get => (Brush)GetValue(MarkerFillBrushProperty); set => SetValue(MarkerFillBrushProperty, value); }

    public DayCurve()
    {
        _clock.Tick += (_, _) => InvalidateVisual(); // move the "now" marker
        Loaded += (_, _) => _clock.Start();
        Unloaded += (_, _) => _clock.Stop();
    }

    public double? Latitude { get => (double?)GetValue(LatitudeProperty); set => SetValue(LatitudeProperty, value); }
    public double? Longitude { get => (double?)GetValue(LongitudeProperty); set => SetValue(LongitudeProperty, value); }
    public Brush CurveBrush { get => (Brush)GetValue(CurveBrushProperty); set => SetValue(CurveBrushProperty, value); }
    public Brush TextBrush { get => (Brush)GetValue(TextBrushProperty); set => SetValue(TextBrushProperty, value); }
    public Brush GridBrush { get => (Brush)GetValue(GridBrushProperty); set => SetValue(GridBrushProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        double width = ActualWidth, plotHeight = ActualHeight - AxisHeight;
        if (width <= 0 || plotHeight <= 0) return;
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, width, ActualHeight)); // hit-testable for tooltips

        var gridPen = new Pen(GridBrush, 1);
        dc.DrawLine(gridPen, new Point(0, plotHeight), new Point(width, plotHeight));
        for (int hour = 0; hour <= 24; hour += 6)
        {
            double x = width * hour / 24;
            DrawText(dc, hour == 24 ? "24:00" : $"{hour:00}:00", new Point(Math.Clamp(x, 18, width - 18), plotHeight + 4), TextBrush, center: true);
        }

        if (Latitude is not double lat || Longitude is not double lon) return;

        var now = DateTimeOffset.Now;
        var midnight = new DateTimeOffset(now.Date, now.Offset);
        var day = SolarCalculator.Day(DateOnly.FromDateTime(now.Date), lat, lon);
        Point ToPoint(DateTimeOffset time, double level)
            => new(width * (time - midnight).TotalHours / 24, plotHeight - level * (plotHeight - 6));
        double LevelAt(DateTimeOffset time) => AdaptiveCurves.SunLevel(SolarCalculator.Elevation(time, lat, lon), day.NoonElevation);

        // Area under the curve, fading towards the axis.
        var curve = new StreamGeometry();
        using (var context = curve.Open())
        {
            context.BeginFigure(new Point(0, plotHeight), isFilled: true, isClosed: true);
            for (int i = 0; i <= SamplesPerDay; i++)
            {
                var time = midnight.AddMinutes(i * 24.0 * 60 / SamplesPerDay);
                context.LineTo(ToPoint(time, LevelAt(time)), isStroked: true, isSmoothJoin: true);
            }
            context.LineTo(new Point(width, plotHeight), isStroked: false, isSmoothJoin: false);
        }
        curve.Freeze();
        var color = (CurveBrush as SolidColorBrush)?.Color ?? Colors.Orange;
        var fill = new LinearGradientBrush(Color.FromArgb(110, color.R, color.G, color.B), Color.FromArgb(8, color.R, color.G, color.B), 90);
        dc.DrawGeometry(fill, new Pen(CurveBrush, 2.5) { LineJoin = PenLineJoin.Round }, curve);

        // Sunrise and sunset.
        var dashed = new Pen(TextBrush, 1) { DashStyle = new DashStyle([3, 3], 0) };
        foreach (var (time, label) in new[] { (day.Sunrise, "sunrise"), (day.Sunset, "sunset") })
        {
            if (time is not DateTimeOffset t) continue;
            var local = t.ToOffset(now.Offset);
            double x = ToPoint(local, 0).X;
            dc.DrawLine(dashed, new Point(x, 14), new Point(x, plotHeight));
            DrawText(dc, $"{label} {local:t}", new Point(Math.Clamp(x, 40, width - 40), 0), TextBrush, center: true);
        }

        // Now.
        var nowPoint = ToPoint(now, LevelAt(now));
        dc.DrawLine(new Pen(CurveBrush, 1), new Point(nowPoint.X, nowPoint.Y), new Point(nowPoint.X, plotHeight));
        dc.DrawEllipse(MarkerFillBrush, new Pen(CurveBrush, 2), nowPoint, 5, 5);
    }

    // Axis labels in the app's mono font when available.
    private FontFamily TextFont => TryFindResource("TidalMonoFont") as FontFamily ?? new FontFamily("Consolas");

    private void DrawText(DrawingContext dc, string text, Point at, Brush brush, bool center)
    {
        var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(TextFont, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), 10.5, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(formatted, center ? new Point(at.X - formatted.Width / 2, at.Y) : at);
    }
}
