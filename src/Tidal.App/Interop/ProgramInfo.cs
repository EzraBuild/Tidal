using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static Tidal.App.Interop.NativeMethods;

namespace Tidal.App.Interop;

/// <summary>The window in front and the program it belongs to.</summary>
/// <param name="Window">The foreground window.</param>
/// <param name="ProcessId">The process that owns it.</param>
/// <param name="ProgramPath">Full path of its program, e.g. <c>C:\Program Files\VideoLAN\VLC\vlc.exe</c>.</param>
/// <param name="ClassName">The window's class, which identifies shell windows such as the taskbar.</param>
/// <param name="Center">The middle of the window, in physical pixels.</param>
internal sealed record ForegroundApp(IntPtr Window, uint ProcessId, string ProgramPath, string ClassName, POINT Center);

/// <summary>Finds out which program owns a window, and a program's name and icon.</summary>
internal static class ProgramInfo
{
    /// <summary>The window in front and its program, or null when there's none or it can't be read.</summary>
    public static ForegroundApp? GetForeground()
    {
        var window = GetForegroundWindow();
        if (window == IntPtr.Zero || !GetWindowRect(window, out var rect)) return null;

        GetWindowThreadProcessId(window, out uint processId);
        string? path = GetProgramPath(processId);
        if (path is null) return null;

        // Store apps run inside a frame window owned by ApplicationFrameHost; the app itself owns a
        // window inside the frame.
        if (string.Equals(Path.GetFileName(path), "ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase))
            path = FindHostedProgram(window, processId) ?? path;

        return new ForegroundApp(window, processId, path, GetClassName(window), rect.Center);
    }

    /// <summary>The program file of a process, or null (e.g. a process that has already exited).</summary>
    public static string? GetProgramPath(uint processId)
    {
        var process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
        if (process == IntPtr.Zero) return null;
        try
        {
            var buffer = new char[1024];
            int size = buffer.Length;
            return QueryFullProcessImageName(process, 0, buffer, ref size) ? new string(buffer, 0, size) : null;
        }
        finally
        {
            CloseHandle(process);
        }
    }

    /// <summary>A friendly name for a program, from its version information ("VLC media player").</summary>
    public static string GetDisplayName(string programPath)
    {
        try
        {
            string? description = FileVersionInfo.GetVersionInfo(programPath).FileDescription?.Trim();
            if (!string.IsNullOrEmpty(description)) return description;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // Fall back to the file name.
        }
        return Path.GetFileNameWithoutExtension(programPath);
    }

    /// <summary>The program's icon, or null if it has none or can't be read.</summary>
    public static ImageSource? GetIcon(string? programPath)
    {
        if (string.IsNullOrEmpty(programPath) || !File.Exists(programPath)) return null;
        var info = new SHFILEINFO();
        SHGetFileInfo(programPath, 0, ref info, (uint)System.Runtime.InteropServices.Marshal.SizeOf<SHFILEINFO>(), SHGFI_ICON | SHGFI_LARGEICON);
        if (info.hIcon == IntPtr.Zero) return null;
        try
        {
            var image = Imaging.CreateBitmapSourceFromHIcon(info.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            image.Freeze();
            return image;
        }
        finally
        {
            DestroyIcon(info.hIcon);
        }
    }

    private static string? FindHostedProgram(IntPtr frame, uint frameProcessId)
    {
        string? hosted = null;
        EnumChildWindows(frame, (child, _) =>
        {
            GetWindowThreadProcessId(child, out uint childProcessId);
            if (childProcessId == frameProcessId) return true;
            hosted = GetProgramPath(childProcessId);
            return hosted is null;
        }, IntPtr.Zero);
        return hosted;
    }

    private static string GetClassName(IntPtr window)
    {
        var buffer = new char[256];
        int length = NativeMethods.GetClassName(window, buffer, buffer.Length);
        return new string(buffer, 0, Math.Max(0, length));
    }
}
