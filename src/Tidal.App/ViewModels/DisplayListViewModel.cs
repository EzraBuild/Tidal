using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Tidal.App.Services;
using Tidal.Core.Displays;

namespace Tidal.App.ViewModels;

/// <summary>
/// The connected displays, shared by the tray popup and the Displays window. Re-detects them when
/// the display setup changes and implements "link all".
/// </summary>
public sealed partial class DisplayListViewModel : ObservableObject, IDisposable
{
    // Windows fires several display-change events per plug/unplug, and monitors need a moment
    // before they answer DDC/CI, so detection waits for things to settle.
    private static readonly TimeSpan DisplayChangeSettleTime = TimeSpan.FromSeconds(1.5);
    private static readonly TimeSpan ResumeSettleTime = TimeSpan.FromSeconds(4);

    private readonly DisplayManager _manager = new();
    private readonly SettingsService _settings;
    private readonly DispatcherTimer _redetectTimer;
    private bool _refreshRequestedWhileBusy;

    public DisplayListViewModel(SettingsService settings)
    {
        _settings = settings;
        LinkAll = settings.Current.LinkAll;

        _redetectTimer = new DispatcherTimer { Interval = DisplayChangeSettleTime };
        _redetectTimer.Tick += async (_, _) =>
        {
            _redetectTimer.Stop();
            await RefreshAsync();
        };
        Displays.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(EmptyMessage));
            OnPropertyChanged(nameof(Summary));
        };

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    public ObservableCollection<DisplayViewModel> Displays { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    [NotifyPropertyChangedFor(nameof(EmptyMessage))]
    private bool _isBusy;

    [ObservableProperty]
    private string _status = "Detecting displays…";

    /// <summary>
    /// When on, moving one display's brightness moves the others to the same relative level within
    /// their own limits (identical values when no limits are set).
    /// </summary>
    [ObservableProperty]
    private bool _linkAll;

    partial void OnLinkAllChanged(bool value)
    {
        if (_settings.Current.LinkAll == value) return;
        _settings.Current.LinkAll = value;
        _settings.ScheduleSave();
    }

    /// <summary>Placeholder text when there are no displays to show, otherwise null.</summary>
    public string? EmptyMessage => Displays.Count > 0 ? null : IsBusy ? "Detecting displays…" : "No displays found";

    /// <summary>One line per display, for the tray icon tooltip.</summary>
    public string Summary => Displays.Count == 0
        ? "Tidal"
        : "Tidal\n" + string.Join("\n", Displays.Select(d => $"{d.Name}: {d.Brightness:0}%"));

    /// <summary>Re-detects displays. Requests made while a detection is running are merged into one follow-up run.</summary>
    [RelayCommand(CanExecute = nameof(CanRefresh))]
    public async Task RefreshAsync()
    {
        if (IsBusy)
        {
            _refreshRequestedWhileBusy = true;
            return;
        }

        IsBusy = true;
        try
        {
            do
            {
                _refreshRequestedWhileBusy = false;
                await DetectAsync();
            }
            while (_refreshRequestedWhileBusy);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanRefresh() => !IsBusy;

    /// <summary>Re-reads current values from every display, e.g. when the popup opens.</summary>
    public Task RefreshValuesAsync() => Task.WhenAll(Displays.Select(d => d.RefreshAsync()));

    /// <summary>
    /// Applies one brightness hotkey press to every display, or only to <paramref name="only"/>.
    /// With "link all" on, one display leads and the rest follow it. Returns the displays whose
    /// level should be shown on screen.
    /// </summary>
    public IReadOnlyList<DisplayViewModel> StepBrightness(int delta, DisplayViewModel? only)
    {
        if (Displays.Count == 0) return [];

        if (LinkAll)
        {
            var lead = only ?? Displays.FirstOrDefault(d => d.IsPrimary) ?? Displays[0];
            lead.StepBrightness(delta); // raises BrightnessEdited, which moves the others
            return Displays.ToList();
        }

        List<DisplayViewModel> targets = only is null ? [.. Displays] : [only];
        foreach (var display in targets) display.StepBrightness(delta);
        return targets;
    }

    /// <summary>
    /// Sets every display to a preset level (e.g. "0.5"): that far between each display's own
    /// limits, so 100% means each display's maximum.
    /// </summary>
    [RelayCommand]
    private void ApplyPreset(string fraction)
    {
        double level = double.Parse(fraction, System.Globalization.CultureInfo.InvariantCulture);
        foreach (var display in Displays) display.SetLevel(level);
    }

    /// <summary>The display containing the given screen point (physical pixels), if any.</summary>
    public DisplayViewModel? FindDisplayAt(int x, int y) => Displays.FirstOrDefault(d => d.Contains(x, y));

    /// <summary>A display's "Adaptive" switch was turned on or off.</summary>
    public event EventHandler? AdaptiveToggled;

    /// <summary>
    /// Moves every adaptive display to <paramref name="level"/> (0–1) within its limits. The built-in
    /// screen is skipped when it is the source being followed.
    /// </summary>
    public void ApplyAdaptiveLevel(double level, bool skipBuiltIn)
    {
        foreach (var display in Displays)
        {
            if (!display.IsAdaptive || (skipBuiltIn && display.IsInternal)) display.StopAdaptive();
            else display.ApplyAdaptiveLevel(level);
        }
    }

    /// <summary>Adaptive brightness is off: displays only change when the user changes them.</summary>
    public void StopAdaptive()
    {
        foreach (var display in Displays) display.StopAdaptive();
    }

    /// <summary>Clears every display's adjustment, e.g. when switching to a different adaptive mode.</summary>
    public void ResetAdaptiveOffsets()
    {
        foreach (var display in Displays) display.ResetAdaptiveOffset();
    }

    private async Task DetectAsync()
    {
        Status = "Detecting displays…";
        foreach (var display in Displays) Untrack(display);
        Displays.Clear();

        try
        {
            var stopwatch = Stopwatch.StartNew();
            var displays = await _manager.RefreshAsync();
            foreach (var display in displays)
            {
                var viewModel = new DisplayViewModel(display, _settings.Current.GetDisplay(display.Id), _settings.ScheduleSave);
                Track(viewModel);
                Displays.Add(viewModel);
            }

            string count = displays.Count == 1 ? "1 display" : $"{displays.Count} displays";
            Status = $"{count} · detected in {stopwatch.Elapsed.TotalSeconds:0.0} s";
        }
        catch (Exception ex)
        {
            Status = $"Detection failed: {ex.Message}";
        }
    }

    private void Track(DisplayViewModel display)
    {
        display.BrightnessEdited += OnBrightnessEdited;
        display.PropertyChanged += OnDisplayPropertyChanged;
        display.AdaptiveToggled += OnAdaptiveToggled;
    }

    private void Untrack(DisplayViewModel display)
    {
        display.BrightnessEdited -= OnBrightnessEdited;
        display.PropertyChanged -= OnDisplayPropertyChanged;
        display.AdaptiveToggled -= OnAdaptiveToggled;
        display.Detach();
    }

    private void OnAdaptiveToggled(object? sender, EventArgs e) => AdaptiveToggled?.Invoke(sender, e);

    private void OnBrightnessEdited(object? sender, EventArgs e)
    {
        if (!LinkAll || sender is not DisplayViewModel source) return;
        double level = source.Range.ToFraction((int)Math.Round(source.Brightness));
        foreach (var other in Displays)
        {
            if (other != source) other.ApplyLinkedLevel(level);
        }
    }

    private void OnDisplayPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DisplayViewModel.Brightness)) OnPropertyChanged(nameof(Summary));
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) => ScheduleRedetect(DisplayChangeSettleTime);

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume) ScheduleRedetect(ResumeSettleTime);
    }

    private void ScheduleRedetect(TimeSpan delay) => _redetectTimer.Dispatcher.InvokeAsync(() =>
    {
        _redetectTimer.Stop();
        _redetectTimer.Interval = delay;
        _redetectTimer.Start();
    });

    public void Dispose()
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        _redetectTimer.Stop();
        foreach (var display in Displays) Untrack(display);
        _manager.Dispose(); // flushes any pending brightness writes
    }
}
