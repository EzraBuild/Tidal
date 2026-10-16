using System.Reflection;
using System.Security;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tidal.Core.Settings;
using Tidal.ViewModels.Platform;

namespace Tidal.ViewModels;

/// <summary>The General tab: starting at sign-in, updates, the version, and where settings are kept.</summary>
public sealed partial class GeneralViewModel(UpdatesViewModel updates, ISignInStartup startup, IFileManager files) : ObservableObject
{
    public UpdatesViewModel Updates { get; } = updates;

    /// <summary>
    /// Start Tidal, in the tray, when you sign in. Read from the system each time, since it can also
    /// be changed in the system's own startup settings (on Windows, Settings › Apps › Startup or Task Manager).
    /// </summary>
    public bool StartAtSignIn
    {
        get => startup.IsEnabled;
        set
        {
            try
            {
                if (value) startup.Enable();
                else startup.Disable();
                StartupError = null;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException or IOException)
            {
                StartupError = $"The system didn't allow the change: {ex.Message}";
            }
            Refresh();
        }
    }

    /// <summary>Shown when Tidal is set to start but the system's startup settings turned it off.</summary>
    public bool IsTurnedOffBySystem => startup.IsTurnedOffBySystem;

    [ObservableProperty]
    private string? _startupError;

    /// <summary>e.g. "0.1.4".</summary>
    public string Version { get; } = ReadVersion();

    public string SettingsPath => SettingsStore.DefaultPath;

    /// <summary>Reads the startup state again, e.g. when the tab is shown.</summary>
    public void Refresh()
    {
        OnPropertyChanged(nameof(StartAtSignIn));
        OnPropertyChanged(nameof(IsTurnedOffBySystem));
    }

    /// <summary>Shows the settings file in the file manager.</summary>
    [RelayCommand]
    private void ShowSettingsFile() => files.ShowFile(SettingsPath);

    // The informational version carries a "+commit" suffix from the build; the version is the part before it.
    private static string ReadVersion()
    {
        string? version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return version?.Split('+')[0] ?? "unknown";
    }
}
