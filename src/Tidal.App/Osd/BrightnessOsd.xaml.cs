using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Tidal.App.Interop;
using Tidal.Core.Displays;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using static Tidal.App.Interop.NativeMethods;

namespace Tidal.App.Osd;

/// <summary>
/// Brief on-screen brightness level after a hotkey press, shown at the bottom centre of the display
/// it belongs to. Never takes focus, so it doesn't interrupt typing.
/// </summary>
public partial class BrightnessOsd : FluentWindow
{
    private const double BottomOffset = 72; // DIPs above the work area's bottom edge
    private static readonly TimeSpan VisibleFor = TimeSpan.FromSeconds(1.5);

    private readonly DispatcherTimer _hideTimer;
    private ScreenRect _displayBounds;

    public BrightnessOsd()
    {
        InitializeComponent();
        SystemThemeWatcher.Watch(this, WindowBackdropType.None, updateAccents: false); // TidalTheme owns the accent

        _hideTimer = new DispatcherTimer { Interval = VisibleFor };
        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer.Stop();
            Hide();
        };
        SourceInitialized += (_, _) => MakeNonActivating();
        DpiChanged += (_, _) => { if (IsVisible) Reposition(RenderSize); };
    }

    /// <summary>Shows (or updates) the level for the display at <paramref name="displayBounds"/>.</summary>
    public void ShowLevel(ScreenRect displayBounds, int percent)
    {
        _displayBounds = displayBounds;
        percent = Math.Clamp(percent, 0, 100);
        FillColumn.Width = new GridLength(percent, GridUnitType.Star);
        RestColumn.Width = new GridLength(100 - percent, GridUnitType.Star);
        LevelText.Text = percent.ToString();

        new WindowInteropHelper(this).EnsureHandle();
        var content = (FrameworkElement)Content;
        content.Measure(new Size(Width, double.PositiveInfinity));
        Reposition(new Size(Width, content.DesiredSize.Height));

        if (!IsVisible) Show();
        _hideTimer.Stop();
        _hideTimer.Start();
    }

    private void Reposition(Size size)
    {
        var handle = new WindowInteropHelper(this).Handle;
        var center = new POINT
        {
            X = (_displayBounds.Left + _displayBounds.Right) / 2,
            Y = (_displayBounds.Top + _displayBounds.Bottom) / 2,
        };
        var monitor = MonitorFromPoint(center, MONITOR_DEFAULTTONEAREST);
        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(monitor, ref info)) return;

        double scale = GetMonitorDpi(monitor) / 96.0;
        int width = (int)Math.Ceiling(size.Width * scale);
        int height = (int)Math.Ceiling(size.Height * scale);
        var work = info.rcWork;
        int x = (work.Left + work.Right - width) / 2;
        int y = work.Bottom - height - (int)Math.Round(BottomOffset * scale);
        SetWindowPos(handle, IntPtr.Zero, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
    }

    // Out of Alt+Tab, and clicking it never steals focus from the app being used.
    private void MakeNonActivating()
    {
        var handle = new WindowInteropHelper(this).Handle;
        long style = GetWindowLongPtr(handle, GWL_EXSTYLE).ToInt64();
        SetWindowLongPtr(handle, GWL_EXSTYLE, new IntPtr(style | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE));
    }
}
