namespace Tidal.App.ViewModels;

/// <summary>What the Settings window shows: the displays, adaptive brightness and the brightness shortcuts.</summary>
public sealed class SettingsViewModel(DisplayListViewModel displays, AdaptiveViewModel adaptive, ShortcutsViewModel shortcuts)
{
    public DisplayListViewModel Displays { get; } = displays;
    public AdaptiveViewModel Adaptive { get; } = adaptive;
    public ShortcutsViewModel Shortcuts { get; } = shortcuts;
}
