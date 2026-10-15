using System.Windows.Interop;
using Tidal.Core.Hotkeys;
using static Tidal.App.Interop.NativeMethods;

namespace Tidal.App.Services;

/// <summary>
/// System-wide keyboard shortcuts via RegisterHotKey. Holding a shortcut down repeats it, which is
/// what brightness keys should do.
/// </summary>
internal sealed class HotkeyManager : IDisposable
{
    private readonly HwndSource _window;
    private readonly HashSet<int> _registered = [];

    public HotkeyManager()
    {
        _window = new HwndSource(new HwndSourceParameters("Tidal hotkeys")
        {
            ParentWindow = HWND_MESSAGE, // message-only: no UI, just a target for WM_HOTKEY
            Width = 0,
            Height = 0,
            WindowStyle = 0,
        });
        _window.AddHook(WndProc);
    }

    /// <summary>Raised with the id passed to <see cref="Register"/>.</summary>
    public event EventHandler<int>? Pressed;

    /// <summary>
    /// Registers <paramref name="shortcut"/> under <paramref name="id"/>, replacing what that id had.
    /// Returns false if Windows or another app already owns the combination.
    /// </summary>
    public bool Register(int id, Shortcut shortcut)
    {
        Unregister(id);
        if (!RegisterHotKey(_window.Handle, id, (uint)shortcut.Modifiers, (uint)shortcut.Key)) return false;
        _registered.Add(id);
        return true;
    }

    public void Unregister(int id)
    {
        if (_registered.Remove(id)) UnregisterHotKey(_window.Handle, id);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            Pressed?.Invoke(this, wParam.ToInt32());
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        foreach (int id in _registered.ToList()) Unregister(id);
        _window.Dispose();
    }
}
