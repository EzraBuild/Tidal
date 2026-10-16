using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Tidal.App.ViewModels;

/// <summary>
/// What the tray popup and the Settings window show: the displays, adaptive brightness, app presets,
/// the shortcuts and general options, plus the Settings tabs (one per display, then Adaptive, Apps,
/// Shortcuts and General).
/// </summary>
public sealed partial class AppViewModel : ObservableObject
{
    public AppViewModel(
        DisplayListViewModel displays, AdaptiveViewModel adaptive, AppPresetsViewModel appPresets,
        ShortcutsViewModel shortcuts, GeneralViewModel general)
    {
        Displays = displays;
        Adaptive = adaptive;
        AppPresets = appPresets;
        Shortcuts = shortcuts;
        General = general;
        Displays.Displays.CollectionChanged += (_, _) => RebuildPages();
        RebuildPages();
    }

    public DisplayListViewModel Displays { get; }
    public AdaptiveViewModel Adaptive { get; }
    public AppPresetsViewModel AppPresets { get; }
    public ShortcutsViewModel Shortcuts { get; }
    public GeneralViewModel General { get; }

    /// <summary>Settings tabs: DisplayViewModel per display, then Adaptive, AppPresets, Shortcuts and General.</summary>
    public ObservableCollection<object> Pages { get; } = [];

    [ObservableProperty]
    private object? _selectedPage;

    private bool _rebuilding;
    private bool _userChoseTab;

    partial void OnSelectedPageChanged(object? value)
    {
        if (!_rebuilding && value is not null) _userChoseTab = true;
        if (value is GeneralViewModel general) general.Refresh(); // start with Windows can change outside Tidal
    }

    private void RebuildPages()
    {
        // Keep the user on the tab they picked across re-detection (displays are matched by ID);
        // otherwise start on the first display.
        string? selectedId = (SelectedPage as DisplayViewModel)?.Id;
        object? selectedOther = SelectedPage is DisplayViewModel ? null : SelectedPage;

        _rebuilding = true;
        try
        {
            Pages.Clear();
            foreach (var display in Displays.Displays) Pages.Add(display);
            Pages.Add(Adaptive);
            Pages.Add(AppPresets);
            Pages.Add(Shortcuts);
            Pages.Add(General);

            object? kept = _userChoseTab
                ? selectedOther ?? Displays.Displays.FirstOrDefault(d => d.Id == selectedId)
                : null;
            SelectedPage = kept ?? Pages.FirstOrDefault();
        }
        finally
        {
            _rebuilding = false;
        }
    }
}
