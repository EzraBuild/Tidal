using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using Tidal.App.Interop;
using static Tidal.App.Interop.NativeMethods;

namespace Tidal.App.Tray;

/// <summary>
/// The notification-area icon, driven directly through Shell_NotifyIcon (NOTIFYICON_VERSION_4) so
/// it can report where it is on screen, follow the taskbar theme and survive Explorer restarts.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    private const int CallbackMessage = WM_APP + 1;
    private const uint IconId = 1;
    private const int MaxToolTipLength = 127;

    // A double-click, or Enter on a keyboard-focused icon, reports "select" twice in a row.
    private static readonly TimeSpan SelectDebounce = TimeSpan.FromMilliseconds(300);

    private readonly HwndSource _window;
    private readonly int _taskbarCreatedMessage = (int)RegisterWindowMessage("TaskbarCreated");
    private IntPtr _icon;
    private string _toolTip;
    private long _lastSelect;
    private bool _added;

    public TrayIcon(string toolTip)
    {
        _toolTip = toolTip;

        // A hidden top-level window, not a message-only one: only top-level windows receive the
        // TaskbarCreated broadcast that Explorer sends after it restarts.
        _window = new HwndSource(new HwndSourceParameters("Tidal tray")
        {
            Width = 0,
            Height = 0,
            WindowStyle = WS_POPUP,
            ExtendedWindowStyle = WS_EX_TOOLWINDOW,
        });
        _window.AddHook(WndProc);

        _icon = CreateThemedIcon();
        Add();

        SystemEvents.UserPreferenceChanged += OnSystemChanged;
        SystemEvents.DisplaySettingsChanged += OnSystemChanged;
    }

    /// <summary>The icon was clicked, or chosen with the keyboard.</summary>
    public event EventHandler? Selected;

    /// <summary>The icon was right-clicked, or Shift+F10 / the menu key was pressed on it.</summary>
    public event EventHandler? ContextMenuRequested;

    public string ToolTip
    {
        get => _toolTip;
        set
        {
            value = value.Length > MaxToolTipLength ? value[..(MaxToolTipLength - 1)] + "…" : value;
            if (value == _toolTip) return;
            _toolTip = value;
            Modify(NIF_TIP | NIF_SHOWTIP);
        }
    }

    /// <summary>The icon's bounds in screen pixels, or null if the shell can't report them.</summary>
    public RECT? GetBounds()
    {
        var id = new NOTIFYICONIDENTIFIER
        {
            cbSize = Marshal.SizeOf<NOTIFYICONIDENTIFIER>(),
            hWnd = _window.Handle,
            uID = IconId,
        };
        return Shell_NotifyIconGetRect(ref id, out var bounds) == 0 ? bounds : null;
    }

    /// <summary>
    /// Gives the app's hidden window the foreground. Needed before showing a context menu, or the
    /// menu won't close when the user clicks somewhere else.
    /// </summary>
    public void ActivateHiddenWindow() => SetForegroundWindow(_window.Handle);

    private void Add()
    {
        var data = CreateData(NIF_MESSAGE | NIF_ICON | NIF_TIP | NIF_SHOWTIP);
        _added = Shell_NotifyIcon(NIM_ADD, ref data);
        if (!_added)
        {
            // Normal at sign-in when Explorer isn't ready yet; TaskbarCreated will arrive later.
            Debug.WriteLine("[Tidal] Adding the tray icon failed; waiting for TaskbarCreated.");
            return;
        }
        data.uVersion = NOTIFYICON_VERSION_4;
        Shell_NotifyIcon(NIM_SETVERSION, ref data);
    }

    private void Modify(uint flags)
    {
        if (!_added) return;
        var data = CreateData(flags);
        Shell_NotifyIcon(NIM_MODIFY, ref data);
    }

    private NOTIFYICONDATA CreateData(uint flags) => new()
    {
        cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
        hWnd = _window.Handle,
        uID = IconId,
        uFlags = flags,
        uCallbackMessage = CallbackMessage,
        hIcon = _icon,
        szTip = _toolTip,
        szInfo = "",
        szInfoTitle = "",
    };

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == CallbackMessage)
        {
            // With NOTIFYICON_VERSION_4 the low word of lParam is the notification.
            switch ((int)(lParam.ToInt64() & 0xFFFF))
            {
                case NIN_SELECT or NIN_KEYSELECT:
                    if (Stopwatch.GetElapsedTime(_lastSelect) > SelectDebounce)
                    {
                        _lastSelect = Stopwatch.GetTimestamp();
                        Selected?.Invoke(this, EventArgs.Empty);
                    }
                    break;
                case WM_CONTEXTMENU:
                    ContextMenuRequested?.Invoke(this, EventArgs.Empty);
                    break;
            }
            handled = true;
        }
        else if (msg == _taskbarCreatedMessage)
        {
            Add(); // Explorer restarted and forgot every icon
        }
        return IntPtr.Zero;
    }

    // Taskbar theme or DPI may have changed; redraw the icon to match.
    private void OnSystemChanged(object? sender, EventArgs e) => _window.Dispatcher.InvokeAsync(() =>
    {
        var old = _icon;
        _icon = CreateThemedIcon();
        Modify(NIF_ICON);
        DestroyIcon(old);
    });

    private static IntPtr CreateThemedIcon()
    {
        // The notification area lives on the primary monitor's taskbar.
        var primary = MonitorFromPoint(default, MONITOR_DEFAULTTOPRIMARY);
        int size = GetSystemMetricsForDpi(SM_CXSMICON, GetMonitorDpi(primary));
        var color = IsTaskbarLight() ? Color.FromRgb(0x1C, 0x1C, 0x1C) : Colors.White;
        return Logo.CreateIconHandle(size, color);
    }

    private static bool IsTaskbarLight()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("SystemUsesLightTheme") is int value && value != 0;
    }

    public void Dispose()
    {
        SystemEvents.UserPreferenceChanged -= OnSystemChanged;
        SystemEvents.DisplaySettingsChanged -= OnSystemChanged;
        if (_added)
        {
            var data = CreateData(0);
            Shell_NotifyIcon(NIM_DELETE, ref data);
            _added = false;
        }
        DestroyIcon(_icon);
        _window.Dispose();
    }
}
