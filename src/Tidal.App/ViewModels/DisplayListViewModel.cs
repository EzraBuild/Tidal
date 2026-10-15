using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
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
    private readonly DispatcherTimer _redetectTimer;
    private bool _refreshRequestedWhileBusy;

    public DisplayListViewModel()
    {
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

    /// <summary>When on, moving one display's brightness moves every display to the same level.</summary>
    [ObservableProperty]
    private bool _linkAll;

    /// <summary>Placeholder text when there are no displays to show, otherwise null.</summary>
    public string? EmptyMessage => Displays.Count > 0 ? null : IsBusy ? "Detecting displays…" : "No displays found";

    /// <summary>One line per display, for the tray icon tooltip.</summary>
    public string Summary => Displays.Count == 0
        ? "Tidal"
        : "Tidal\n" + string.Join("\n", Displays.Select(d => d.SupportsBrightness ? $"{d.Name}: {d.Brightness:0}%" : $"{d.Name}: no control"));

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
                var viewModel = new DisplayViewModel(display);
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
    }

    private void Untrack(DisplayViewModel display)
    {
        display.BrightnessEdited -= OnBrightnessEdited;
        display.PropertyChanged -= OnDisplayPropertyChanged;
        display.Detach();
    }

    private void OnBrightnessEdited(object? sender, EventArgs e)
    {
        if (!LinkAll || sender is not DisplayViewModel source) return;
        foreach (var other in Displays)
        {
            if (other != source && other.SupportsBrightness) other.ApplyLinkedBrightness(source.Brightness);
        }
    }

    private void OnDisplayPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DisplayViewModel.Brightness) or nameof(DisplayViewModel.SupportsBrightness))
            OnPropertyChanged(nameof(Summary));
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
