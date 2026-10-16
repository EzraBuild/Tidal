using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tidal.App.Interop;
using Tidal.Core.Settings;

namespace Tidal.App.ViewModels;

/// <summary>A running app that could get a preset, for the "Add app" menu.</summary>
public sealed record RunningApp(string Path, string Name, ImageSource? Icon);

/// <summary>
/// The Apps tab: a brightness for particular apps, used while that app is in front (see
/// <see cref="Services.AppPresetService"/>).
/// </summary>
public sealed partial class AppPresetsViewModel : ObservableObject
{
    // Processes with windows that aren't apps a preset would make sense for.
    private static readonly HashSet<string> NotApps = new(StringComparer.OrdinalIgnoreCase)
    {
        "ApplicationFrameHost.exe", "TextInputHost.exe", "SystemSettings.exe", "ShellExperienceHost.exe",
        "StartMenuExperienceHost.exe", "SearchHost.exe", "LockApp.exe",
    };

    private readonly TidalSettings _settings;
    private readonly Action _changed;

    public AppPresetsViewModel(TidalSettings settings, Action changed)
    {
        _settings = settings;
        _changed = changed;
        foreach (var preset in settings.AppPresets) Presets.Add(new AppPresetViewModel(preset, changed));
        Presets.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmpty));
    }

    public ObservableCollection<AppPresetViewModel> Presets { get; } = [];

    public bool IsEmpty => Presets.Count == 0;

    /// <summary>Adds a preset for the program at <paramref name="programPath"/> at full brightness, unless it has one already.</summary>
    public void Add(string programPath)
    {
        if (_settings.FindAppPreset(programPath) is not null) return;
        var preset = new AppPreset
        {
            App = AppPreset.ProgramName(programPath),
            Name = ProgramInfo.GetDisplayName(programPath),
            Path = programPath,
        };
        _settings.AppPresets.Add(preset);
        Presets.Add(new AppPresetViewModel(preset, _changed));
        _changed();
    }

    [RelayCommand]
    private void Remove(AppPresetViewModel preset)
    {
        _settings.AppPresets.Remove(preset.Preset);
        Presets.Remove(preset);
        _changed();
    }

    /// <summary>
    /// Apps with a window open that don't have a preset yet, by name. Reads every process, so call it
    /// off the UI thread.
    /// </summary>
    public IReadOnlyList<RunningApp> FindRunningApps()
    {
        var apps = new Dictionary<string, RunningApp>(StringComparer.OrdinalIgnoreCase);
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (process.Id == Environment.ProcessId || process.MainWindowHandle == IntPtr.Zero
                        || string.IsNullOrWhiteSpace(process.MainWindowTitle)) continue;
                    string? path = ProgramInfo.GetProgramPath((uint)process.Id);
                    if (path is null) continue;
                    string program = AppPreset.ProgramName(path);
                    if (NotApps.Contains(program) || apps.ContainsKey(program) || _settings.FindAppPreset(path) is not null) continue;
                    apps[program] = new RunningApp(path, ProgramInfo.GetDisplayName(path), ProgramInfo.GetIcon(path));
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                    // The process exited while we looked at it.
                }
            }
        }
        return apps.Values.OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
}

/// <summary>One app's preset in Settings.</summary>
public sealed partial class AppPresetViewModel : ObservableObject
{
    private readonly Action _changed;

    public AppPresetViewModel(AppPreset preset, Action changed)
    {
        Preset = preset;
        _changed = changed;
        Icon = ProgramInfo.GetIcon(preset.Path);
    }

    public AppPreset Preset { get; }
    public string Name => Preset.DisplayName;
    public string App => Preset.App;
    public ImageSource? Icon { get; }
    public bool HasIcon => Icon is not null;

    /// <summary>Brightness while the app is in front, in percent of each display's range.</summary>
    public double Brightness
    {
        get => Preset.Brightness;
        set
        {
            int percent = Math.Clamp((int)Math.Round(value), 0, 100);
            if (percent == Preset.Brightness) return;
            Preset.Brightness = percent;
            OnPropertyChanged();
            _changed();
        }
    }
}
