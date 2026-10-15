namespace Tidal.App.ViewModels;

/// <summary>What the Settings window shows: the displays and the brightness shortcuts.</summary>
public sealed class SettingsViewModel(DisplayListViewModel displays, ShortcutsViewModel shortcuts)
{
    public DisplayListViewModel Displays { get; } = displays;
    public ShortcutsViewModel Shortcuts { get; } = shortcuts;
}
