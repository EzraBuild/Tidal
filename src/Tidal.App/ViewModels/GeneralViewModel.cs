using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tidal.App.Services;
using Tidal.Core.Settings;

namespace Tidal.App.ViewModels;

/// <summary>The General tab: starting with Windows, the version, and where settings are kept.</summary>
public sealed partial class GeneralViewModel : ObservableObject
{
    /// <summary>
    /// Start Tidal, in the tray, when you sign in. Read from Windows each time, since it can also be
    /// changed in Settings › Apps › Startup or Task Manager.
    /// </summary>
    public bool StartWithWindows
    {
        get => WindowsStartup.IsEnabled;
        set
        {
            try
            {
                if (value) WindowsStartup.Enable();
                else WindowsStartup.Disable();
                StartupError = null;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException or IOException)
            {
                StartupError = $"Windows didn't allow the change: {ex.Message}";
            }
            Refresh();
        }
    }

    /// <summary>Shown when Tidal is set to start but Windows' Startup apps list has it turned off.</summary>
    public bool IsTurnedOffByWindows => WindowsStartup.IsTurnedOffByWindows;

    [ObservableProperty]
    private string? _startupError;

    /// <summary>e.g. "0.1.4".</summary>
    public string Version { get; } = ReadVersion();

    public string SettingsPath => SettingsStore.DefaultPath;

    /// <summary>Reads the startup state again, e.g. when the tab is shown.</summary>
    public void Refresh()
    {
        OnPropertyChanged(nameof(StartWithWindows));
        OnPropertyChanged(nameof(IsTurnedOffByWindows));
    }

    /// <summary>Shows the settings file in File Explorer.</summary>
    [RelayCommand]
    private void ShowSettingsFile()
    {
        string arguments = File.Exists(SettingsPath) ? $"/select,\"{SettingsPath}\"" : $"\"{Path.GetDirectoryName(SettingsPath)}\"";
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", arguments) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            Debug.WriteLine($"[Tidal] Opening File Explorer failed: {ex.Message}");
        }
    }

    // The informational version carries a "+commit" suffix from the build; the version is the part before it.
    private static string ReadVersion()
    {
        string? version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return version?.Split('+')[0] ?? "unknown";
    }
}
