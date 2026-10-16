using System.ComponentModel;
using System.Diagnostics;
using Tidal.App.Interop;
using Tidal.Core.Settings;
using Tidal.ViewModels.Platform;

namespace Tidal.App.Services;

/// <summary>Open apps, program names and icons, read from Windows.</summary>
internal sealed class WindowsAppCatalog : IAppCatalog
{
    // Processes with windows that aren't apps a preset would make sense for.
    private static readonly HashSet<string> NotApps = new(StringComparer.OrdinalIgnoreCase)
    {
        "ApplicationFrameHost.exe", "TextInputHost.exe", "SystemSettings.exe", "ShellExperienceHost.exe",
        "StartMenuExperienceHost.exe", "SearchHost.exe", "LockApp.exe",
    };

    public IEnumerable<string> FindRunningPrograms()
    {
        foreach (var process in Process.GetProcesses())
        {
            string? path = null;
            using (process)
            {
                try
                {
                    if (process.Id != Environment.ProcessId && process.MainWindowHandle != IntPtr.Zero
                        && !string.IsNullOrWhiteSpace(process.MainWindowTitle))
                        path = ProgramInfo.GetProgramPath((uint)process.Id);
                }
                catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
                {
                    // The process exited while we looked at it.
                }
            }
            if (path is not null && !NotApps.Contains(AppPreset.ProgramName(path))) yield return path;
        }
    }

    public string GetDisplayName(string programPath) => ProgramInfo.GetDisplayName(programPath);

    public object? GetIcon(string programPath) => ProgramInfo.GetIcon(programPath);
}
