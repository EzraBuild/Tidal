using System.Diagnostics;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tidal.Core.Displays;
using Tidal.Core.Settings;
using Wpf.Ui.Controls;

namespace Tidal.App.ViewModels;

public sealed partial class DisplayViewModel : ObservableObject
{
    // A hardware read can finish just after the user moves a slider, carrying the old value.
    // Ignore read values for a moment after an edit so the slider doesn't jump back.
    private static readonly TimeSpan EditHold = TimeSpan.FromSeconds(1.5);

    private readonly Display _display;
    private readonly DisplaySettings _settings;
    private readonly Action _settingsChanged;
    private bool _loadingFromDisplay;
    private bool _suppressEditEvent;
    private long _lastEdit;

    public DisplayViewModel(Display display, DisplaySettings settings, Action settingsChanged)
    {
        _display = display;
        _settings = settings;
        _settingsChanged = settingsChanged;
        _display.ValuesRead += OnValuesRead;
        LoadFromDisplay();
    }

    /// <summary>Raised when the user (not a hardware read, a limit change or "link all") changes the brightness.</summary>
    public event EventHandler? BrightnessEdited;

    public string Id => _display.Id;
    public string Name => _display.Name;
    public bool IsPrimary => _display.Info.IsPrimary;
    public ScreenRect Bounds => _display.Info.Bounds;
    public SymbolRegular Icon => _display.Info.IsInternal ? SymbolRegular.Laptop24 : SymbolRegular.Desktop24;

    public string Details => _display.Method switch
    {
        ControlMethod.DdcCi => $"{_display.Info.Connection} · DDC/CI",
        ControlMethod.Wmi => $"{_display.Info.Connection} · Windows brightness",
        _ => $"{_display.Info.Connection} · No hardware control",
    };

    [ObservableProperty]
    private bool _supportsBrightness;

    [ObservableProperty]
    private bool _supportsContrast;

    [ObservableProperty]
    private double _brightness;

    [ObservableProperty]
    private double _contrast;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNote))]
    private string? _note;

    public bool HasNote => Note is not null;

    /// <summary>The brightness band Tidal keeps this display in.</summary>
    public BrightnessRange Range => _settings.BrightnessRange;

    /// <summary>Lowest brightness Tidal will set. Nullable because NumberBox edits a double?.</summary>
    public double? MinBrightness
    {
        get => _settings.MinBrightness;
        set
        {
            if (value is not double v) return;
            int min = (int)Math.Round(v);
            SetLimits(min, Math.Max(min, _settings.MaxBrightness)); // raising the floor past the ceiling lifts the ceiling too
        }
    }

    /// <summary>Highest brightness Tidal will set.</summary>
    public double? MaxBrightness
    {
        get => _settings.MaxBrightness;
        set
        {
            if (value is not double v) return;
            int max = (int)Math.Round(v);
            SetLimits(Math.Min(_settings.MinBrightness, max), max);
        }
    }

    /// <summary>Tooltip text when the limits narrow the slider, otherwise null.</summary>
    public string? LimitsDescription => Range.IsFull ? null : $"Limited to {Range}";

    partial void OnBrightnessChanged(double value)
    {
        if (_loadingFromDisplay || !SupportsBrightness) return;

        int percent = (int)Math.Round(value);
        int allowed = Range.Clamp(percent);
        if (allowed != percent)
        {
            Brightness = allowed; // the nested change does the write
            return;
        }

        _lastEdit = Stopwatch.GetTimestamp();
        _display.SetBrightness(percent);
        if (!_suppressEditEvent) BrightnessEdited?.Invoke(this, EventArgs.Empty);
    }

    partial void OnContrastChanged(double value)
    {
        if (_loadingFromDisplay || !SupportsContrast) return;
        _lastEdit = Stopwatch.GetTimestamp();
        _display.SetContrast((int)Math.Round(value));
    }

    /// <summary>One hotkey press: brighter or dimmer by <paramref name="delta"/>, within the limits.</summary>
    public void StepBrightness(int delta)
    {
        if (!SupportsBrightness) return;
        Brightness = Range.Step((int)Math.Round(Brightness), delta);
    }

    /// <summary>
    /// Follows another display under "link all": takes the same relative position within this
    /// display's own limits, without echoing the edit.
    /// </summary>
    public void ApplyLinkedLevel(double fraction) => SetBrightnessQuietly(Range.FromFraction(fraction));

    public bool Contains(int x, int y) => x >= Bounds.Left && x < Bounds.Right && y >= Bounds.Top && y < Bounds.Bottom;

    /// <summary>Reads current values back from the display (e.g. after using the monitor's own buttons).</summary>
    [RelayCommand]
    public async Task RefreshAsync()
    {
        try
        {
            await _display.RefreshAsync();
        }
        catch (OperationCanceledException)
        {
            // The display was replaced by a re-detection while the read was queued.
        }
    }

    /// <summary>Stops listening to the display; call before it is disposed.</summary>
    public void Detach() => _display.ValuesRead -= OnValuesRead;

    private void SetLimits(int min, int max)
    {
        var range = new BrightnessRange(min, max);
        if (range != Range)
        {
            (_settings.MinBrightness, _settings.MaxBrightness) = (range.Min, range.Max);
            _settingsChanged();
            OnPropertyChanged(nameof(Range));
            OnPropertyChanged(nameof(LimitsDescription));

            // Respect the new limits right away rather than at the next change.
            int current = (int)Math.Round(Brightness);
            if (SupportsBrightness && range.Clamp(current) != current) SetBrightnessQuietly(range.Clamp(current));
        }
        // Always re-raise: the box may show a value that was just adjusted (e.g. min typed above max).
        OnPropertyChanged(nameof(MinBrightness));
        OnPropertyChanged(nameof(MaxBrightness));
    }

    private void SetBrightnessQuietly(int value)
    {
        _suppressEditEvent = true;
        try
        {
            Brightness = value;
        }
        finally
        {
            _suppressEditEvent = false;
        }
    }

    private void OnValuesRead(object? sender, EventArgs e)
        => Application.Current?.Dispatcher.InvokeAsync(LoadFromDisplay);

    private void LoadFromDisplay()
    {
        _loadingFromDisplay = true;
        try
        {
            SupportsBrightness = _display.SupportsBrightness;
            SupportsContrast = _display.SupportsContrast;
            bool recentlyEdited = _lastEdit != 0 && Stopwatch.GetElapsedTime(_lastEdit) < EditHold;
            if (!recentlyEdited)
            {
                // Shown as read, even outside the limits: Tidal only enforces them when it changes the value.
                Brightness = _display.Brightness ?? 0;
                Contrast = _display.Contrast ?? 0;
            }
            Note = _display.Note;
            OnPropertyChanged(nameof(Details));
        }
        finally
        {
            _loadingFromDisplay = false;
        }
    }
}
