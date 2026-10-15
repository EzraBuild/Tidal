using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Tidal.App.ViewModels;

/// <summary>
/// What the tray popup and the Settings window show: the displays, adaptive brightness and the
/// shortcuts, plus the Settings tabs (one per display, then Adaptive and Shortcuts).
/// </summary>
public sealed partial class AppViewModel : ObservableObject
{
    public AppViewModel(DisplayListViewModel displays, AdaptiveViewModel adaptive, ShortcutsViewModel shortcuts)
    {
        Displays = displays;
        Adaptive = adaptive;
        Shortcuts = shortcuts;
        Displays.Displays.CollectionChanged += (_, _) => RebuildPages();
        RebuildPages();
    }

    public DisplayListViewModel Displays { get; }
    public AdaptiveViewModel Adaptive { get; }
    public ShortcutsViewModel Shortcuts { get; }

    /// <summary>Settings tabs: DisplayViewModel per display, then AdaptiveViewModel and ShortcutsViewModel.</summary>
    public ObservableCollection<object> Pages { get; } = [];

    [ObservableProperty]
    private object? _selectedPage;

    private bool _rebuilding;
    private bool _userChoseTab;

    partial void OnSelectedPageChanged(object? value)
    {
        if (!_rebuilding && value is not null) _userChoseTab = true;
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
            Pages.Add(Shortcuts);

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
