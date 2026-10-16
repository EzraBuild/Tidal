using Microsoft.Win32;
using Tidal.ViewModels.Platform;
using Tidal.Windows.Startup;

namespace Tidal.App.Services;

/// <summary>
/// Whether Windows starts Tidal when you sign in: a value under the current user's Run key (no
/// admin rights needed), which Windows lists in Settings › Apps › Startup and in Task Manager.
/// </summary>
internal sealed class WindowsStartup : ISignInStartup
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string ValueName = "Tidal";

    /// <summary>This copy of Tidal; an installed copy keeps the same path across updates.</summary>
    private static string ProgramPath => Environment.ProcessPath ?? throw new InvalidOperationException("Tidal's own path is unknown.");

    /// <summary>True when Windows will start this copy of Tidal at sign-in.</summary>
    public bool IsEnabled => IsRegistered && !IsTurnedOffBySystem;

    /// <summary>True when Tidal is registered but turned off in Windows' Startup apps (Settings or Task Manager).</summary>
    public bool IsTurnedOffBySystem
    {
        get
        {
            if (!IsRegistered) return false;
            using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKey);
            return !StartupCommand.IsApproved(approved?.GetValue(ValueName) as byte[]);
        }
    }

    private static bool IsRegistered
    {
        get
        {
            using var run = Registry.CurrentUser.OpenSubKey(RunKey);
            return StartupCommand.Runs(run?.GetValue(ValueName) as string, ProgramPath);
        }
    }

    /// <summary>
    /// Starts this copy of Tidal, in the tray, at sign-in. Turning it on here also clears an "off"
    /// set in Windows' Startup apps, since that's what the user just asked for.
    /// </summary>
    public void Enable()
    {
        using (var run = Registry.CurrentUser.CreateSubKey(RunKey))
        {
            run.SetValue(ValueName, StartupCommand.For(ProgramPath));
        }
        using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKey, writable: true);
        approved?.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    /// <summary>Stops Windows starting Tidal at sign-in.</summary>
    public void Disable()
    {
        using (var run = Registry.CurrentUser.OpenSubKey(RunKey, writable: true))
        {
            run?.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKey, writable: true);
        approved?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
