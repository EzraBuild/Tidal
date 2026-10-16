using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Tidal.App.Interop;
using Tidal.ViewModels;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using static Tidal.App.Interop.NativeMethods;

namespace Tidal.App.Tray;

/// <summary>
/// The flyout shown from the tray icon: a brightness slider per display and "link all". Sits in the
/// corner by the taskbar like the Windows 11 flyouts and closes when it loses focus.
/// </summary>
public partial class TrayPopup : FluentWindow
{
    private const double EdgeMargin = 12; // DIPs between the popup and the work area edge, as in Windows 11
    private const double SlideDistance = 16;
    private static readonly TimeSpan OpenAnimationDuration = TimeSpan.FromMilliseconds(200);
    // Clicking the tray icon while the popup is open first closes it (focus loss), then reports the
    // click. Treat a click that soon after closing as "close", not "open again".
    private static readonly TimeSpan ReopenGuard = TimeSpan.FromMilliseconds(400);

    private readonly AppViewModel _viewModel;
    private RECT? _anchor;
    private TaskbarEdge _edge = TaskbarEdge.Bottom;
    private long _hiddenAt;
    private bool _isOpen;
    private bool _allowClose;

    public TrayPopup(AppViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
        SystemThemeWatcher.Watch(this, WindowBackdropType.None, updateAccents: false); // TidalTheme owns the accent

        SourceInitialized += (_, _) => HideFromAltTab();
        Deactivated += (_, _) => HidePopup();
        // Content can grow or shrink while open (e.g. a monitor is plugged in); keep the corner anchored.
        SizeChanged += (_, e) => { if (_isOpen) Reposition(e.NewSize); };
        DpiChanged += (_, _) => { if (_isOpen) Reposition(RenderSize); };
    }

    /// <summary>The user asked for the Settings window.</summary>
    public event EventHandler? SettingsRequested;

    /// <summary>The user asked to quit Tidal.</summary>
    public event EventHandler? QuitRequested;

    public bool IsOpen => _isOpen;

    /// <summary>True if the popup closed a moment ago, typically because of the click now asking to toggle it.</summary>
    public bool WasJustHidden => _hiddenAt != 0 && Stopwatch.GetElapsedTime(_hiddenAt) < ReopenGuard;

    /// <summary>Shows the popup by the taskbar on the monitor containing <paramref name="anchor"/> (the tray icon).</summary>
    internal void ShowNear(RECT? anchor)
    {
        _anchor = anchor;
        _ = _viewModel.Displays.RefreshValuesAsync(); // values may have changed via monitor buttons or Fn keys

        var handle = new WindowInteropHelper(this).EnsureHandle();
        // A hidden window isn't laid out, so measure the content to know the size before showing.
        var content = (FrameworkElement)Content;
        content.Measure(new Size(Width, double.PositiveInfinity));
        Reposition(new Size(Width, content.DesiredSize.Height));

        _isOpen = true;
        Show();
        Activate();
        SetForegroundWindow(handle);
        PlayOpenAnimation();
    }

    public void HidePopup()
    {
        if (!_isOpen) return;
        _isOpen = false;
        _hiddenAt = Stopwatch.GetTimestamp();
        Hide();
    }

    /// <summary>Lets the window really close (on app exit) instead of hiding.</summary>
    public void AllowClose() => _allowClose = true;

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true; // Alt+F4 hides; the popup is reused for the app's lifetime
            HidePopup();
        }
        base.OnClosing(e);
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            HidePopup();
            e.Handled = true;
        }
        base.OnPreviewKeyDown(e);
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke(this, EventArgs.Empty);

    private void OnQuitClick(object sender, RoutedEventArgs e) => QuitRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Moves the popup into place for a window of <paramref name="size"/> (in DIPs).</summary>
    private void Reposition(Size size)
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero) return;

        POINT anchorPoint = _anchor?.Center ?? (GetCursorPos(out var cursor) ? cursor : default);
        var monitor = MonitorFromPoint(anchorPoint, MONITOR_DEFAULTTONEAREST);
        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(monitor, ref info)) return;

        // Convert with the target monitor's DPI: the window may still be on a monitor with another one.
        double scale = GetMonitorDpi(monitor) / 96.0;
        int width = (int)Math.Ceiling(size.Width * scale);
        int height = (int)Math.Ceiling(size.Height * scale);
        int margin = (int)Math.Round(EdgeMargin * scale);

        var area = GetUsableArea(info, out _edge);
        var (x, y) = Place(area, _edge, width, height, margin);
        SetWindowPos(handle, IntPtr.Zero, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
    }

    /// <summary>
    /// Top-left corner for a popup of the given size: in the work area's corner nearest the tray
    /// (right side, against the taskbar), clamped to stay fully on screen.
    /// </summary>
    internal static (int X, int Y) Place(RECT workArea, TaskbarEdge edge, int width, int height, int margin)
    {
        int x = edge == TaskbarEdge.Left ? workArea.Left + margin : workArea.Right - width - margin;
        int y = edge == TaskbarEdge.Top ? workArea.Top + margin : workArea.Bottom - height - margin;
        x = Math.Max(workArea.Left, Math.Min(x, workArea.Right - width));
        y = Math.Max(workArea.Top, Math.Min(y, workArea.Bottom - height));
        return (x, y);
    }

    /// <summary>The part of the monitor the popup may use, and which edge the taskbar is on.</summary>
    private static RECT GetUsableArea(MONITORINFO monitor, out TaskbarEdge edge)
    {
        var work = monitor.rcWork;
        var data = new APPBARDATA { cbSize = Marshal.SizeOf<APPBARDATA>() };
        if (SHAppBarMessage(ABM_GETTASKBARPOS, ref data) != UIntPtr.Zero && data.rc.IntersectsWith(monitor.rcMonitor))
        {
            edge = (TaskbarEdge)data.uEdge;
            // An auto-hide taskbar leaves the work area full size, but slides in over it whenever
            // the tray is used, so keep clear of where it will be.
            if ((SHAppBarMessage(ABM_GETSTATE, ref data).ToUInt64() & ABS_AUTOHIDE) != 0)
            {
                var bar = data.rc;
                switch (edge)
                {
                    case TaskbarEdge.Bottom: work.Bottom = Math.Min(work.Bottom, monitor.rcMonitor.Bottom - (bar.Bottom - bar.Top)); break;
                    case TaskbarEdge.Top: work.Top = Math.Max(work.Top, monitor.rcMonitor.Top + (bar.Bottom - bar.Top)); break;
                    case TaskbarEdge.Left: work.Left = Math.Max(work.Left, monitor.rcMonitor.Left + (bar.Right - bar.Left)); break;
                    case TaskbarEdge.Right: work.Right = Math.Min(work.Right, monitor.rcMonitor.Right - (bar.Right - bar.Left)); break;
                }
            }
            return work;
        }

        // The main taskbar is elsewhere; infer this monitor's taskbar from its work area.
        edge = work.Top > monitor.rcMonitor.Top ? TaskbarEdge.Top
            : work.Left > monitor.rcMonitor.Left ? TaskbarEdge.Left
            : work.Right < monitor.rcMonitor.Right ? TaskbarEdge.Right
            : TaskbarEdge.Bottom;
        return work;
    }

    private void PlayOpenAnimation()
    {
        if (!SystemParameters.ClientAreaAnimation) return; // "Animation effects" is off in Windows settings

        // Slide in from the taskbar side. Only the content animates: fading the window itself would
        // require a layered window, which can't have the Acrylic backdrop.
        var (property, from) = _edge switch
        {
            TaskbarEdge.Top => (TranslateTransform.YProperty, -SlideDistance),
            TaskbarEdge.Left => (TranslateTransform.XProperty, -SlideDistance),
            TaskbarEdge.Right => (TranslateTransform.XProperty, SlideDistance),
            _ => (TranslateTransform.YProperty, SlideDistance),
        };
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        SlideTransform.BeginAnimation(property, new DoubleAnimation(from, 0, OpenAnimationDuration) { EasingFunction = ease });
        Root.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, OpenAnimationDuration));
    }

    // ShowInTaskbar=False still leaves the window in Alt+Tab; a tool window is left out.
    private void HideFromAltTab()
    {
        var handle = new WindowInteropHelper(this).Handle;
        long style = GetWindowLongPtr(handle, GWL_EXSTYLE).ToInt64();
        SetWindowLongPtr(handle, GWL_EXSTYLE, new IntPtr(style | WS_EX_TOOLWINDOW));
    }
}
