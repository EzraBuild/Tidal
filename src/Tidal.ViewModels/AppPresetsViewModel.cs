using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tidal.Core.Settings;
using Tidal.ViewModels.Platform;

namespace Tidal.ViewModels;

/// <summary>A running app that could get a preset, for the "Add app" menu.</summary>
/// <param name="Icon">An image the UI framework can show (see <see cref="IAppCatalog.GetIcon"/>), or null.</param>
public sealed record RunningApp(string Path, string Name, object? Icon);

/// <summary>
/// The Apps tab: a brightness for particular apps, used while that app is in front. Watching which
/// app is in front is up to each platform's app (on Windows, AppPresetService).
/// </summary>
public sealed partial class AppPresetsViewModel : ObservableObject
{
    private readonly TidalSettings _settings;
    private readonly IAppCatalog _catalog;
    private readonly Action _changed;

    public AppPresetsViewModel(TidalSettings settings, IAppCatalog catalog, Action changed)
    {
        _settings = settings;
        _catalog = catalog;
        _changed = changed;
        foreach (var preset in settings.AppPresets) Presets.Add(CreateViewModel(preset));
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
            Name = _catalog.GetDisplayName(programPath),
            Path = programPath,
        };
        _settings.AppPresets.Add(preset);
        Presets.Add(CreateViewModel(preset));
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
        foreach (string path in _catalog.FindRunningPrograms())
        {
            string program = AppPreset.ProgramName(path);
            if (apps.ContainsKey(program) || _settings.FindAppPreset(path) is not null) continue;
            apps[program] = new RunningApp(path, _catalog.GetDisplayName(path), _catalog.GetIcon(path));
        }
        return apps.Values.OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    private AppPresetViewModel CreateViewModel(AppPreset preset)
        => new(preset, preset.Path is { } path ? _catalog.GetIcon(path) : null, _changed);
}

/// <summary>One app's preset in Settings.</summary>
public sealed partial class AppPresetViewModel : ObservableObject
{
    private readonly Action _changed;

    public AppPresetViewModel(AppPreset preset, object? icon, Action changed)
    {
        Preset = preset;
        Icon = icon;
        _changed = changed;
    }

    public AppPreset Preset { get; }
    public string Name => Preset.DisplayName;
    public string App => Preset.App;
    public object? Icon { get; }
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
