using System.ComponentModel;
using System.Runtime.InteropServices;
using Tidal.Core.Displays;
using static Tidal.App.Interop.NativeMethods;

namespace Tidal.App.Interop;

/// <summary>
/// A black, see-through window over one display, for software dimming. Clicks go straight through
/// it, it never takes focus, it stays out of Alt+Tab, screenshots and screen sharing, and it
/// disappears with the process, so a crash can't leave a screen dark.
/// </summary>
/// <remarks>
/// A plain Win32 window rather than a WPF one: its class brush paints it black and Windows blends it
/// with a constant alpha, so it costs nothing to draw. Must be used on the UI thread.
/// </remarks>
internal sealed class DimOverlay : IDisposable
{
    private const string ClassName = "TidalDimOverlay";
    private static bool s_classRegistered;

    private IntPtr _handle;
    private ScreenRect _bounds;
    private byte _alpha;

    public DimOverlay()
    {
        RegisterClass();
        _handle = CreateWindowEx(
            WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST,
            ClassName, "Tidal dimming", WS_POPUP, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, GetModuleHandle(null), IntPtr.Zero);
        if (_handle == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "Couldn't create the dimming overlay.");

        SetLayeredWindowAttributes(_handle, 0, 0, LWA_ALPHA);
        SetWindowDisplayAffinity(_handle, WDA_EXCLUDEFROMCAPTURE); // best effort: screenshots show the undimmed picture
    }

    public bool IsShown { get; private set; }

    /// <summary>Covers <paramref name="bounds"/> (physical pixels) at <paramref name="opacity"/>; 0 hides it.</summary>
    public void Update(ScreenRect bounds, double opacity)
    {
        byte alpha = SoftwareDimming.ToAlpha(opacity);
        if (alpha == 0)
        {
            Hide();
            return;
        }

        if (alpha != _alpha)
        {
            SetLayeredWindowAttributes(_handle, 0, alpha, LWA_ALPHA);
            _alpha = alpha;
        }
        if (!IsShown || bounds != _bounds)
        {
            SetWindowPos(_handle, HWND_TOPMOST, bounds.Left, bounds.Top, bounds.Width, bounds.Height, SWP_NOACTIVATE | SWP_SHOWWINDOW);
            _bounds = bounds;
            IsShown = true;
        }
    }

    /// <summary>
    /// Puts the overlay back above other always-on-top windows, such as the taskbar after it was
    /// clicked, which Windows raises over it.
    /// </summary>
    public void BringToFront()
    {
        if (IsShown) SetWindowPos(_handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    private void Hide()
    {
        if (!IsShown) return;
        ShowWindow(_handle, SW_HIDE);
        IsShown = false;
    }

    public void Dispose()
    {
        if (_handle == IntPtr.Zero) return;
        DestroyWindow(_handle);
        _handle = IntPtr.Zero;
        IsShown = false;
    }

    private static void RegisterClass()
    {
        if (s_classRegistered) return;
        // No window procedure of our own: Windows' default one paints the black class brush and
        // nothing ever needs handling, so no managed callback has to stay alive.
        var windowClass = new WNDCLASSEX
        {
            cbSize = Marshal.SizeOf<WNDCLASSEX>(),
            lpfnWndProc = GetProcAddress(GetModuleHandle("user32.dll"), "DefWindowProcW"),
            hInstance = GetModuleHandle(null),
            hbrBackground = GetStockObject(BLACK_BRUSH),
            lpszClassName = ClassName,
        };
        if (RegisterClassEx(ref windowClass) == 0 && Marshal.GetLastWin32Error() != ERROR_CLASS_ALREADY_EXISTS)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Couldn't register the dimming overlay.");
        s_classRegistered = true;
    }
}
