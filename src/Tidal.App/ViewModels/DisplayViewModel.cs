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
    private bool _applyingAdaptive;
    private long _lastEdit;
    // A monitor that is switching inputs can report a wrong input for a few seconds.
    private static readonly TimeSpan InputSwitchHold = TimeSpan.FromSeconds(5);
    private long _inputSwitchedAt;
    // The adaptive level this display is following, or null when adaptive brightness isn't driving it.
    private double? _adaptiveLevel;

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
    public bool IsInternal => _display.Info.IsInternal;
    public ScreenRect Bounds => _display.Info.Bounds;
    public SymbolRegular Icon => _display.Info.IsInternal ? SymbolRegular.Laptop24 : SymbolRegular.Desktop24;

    /// <summary>How Tidal reaches the hardware, for the badge on the monitor illustration.</summary>
    public string MethodLabel => _display.Method switch
    {
        ControlMethod.DdcCi => "DDC/CI",
        ControlMethod.Wmi => "Windows brightness",
        _ => "No hardware control",
    };

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

    /// <summary>Inputs the monitor can show (empty until its capabilities have been read).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasInputs))]
    private IReadOnlyList<InputSource> _inputs = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentInputName))]
    private InputSource? _currentInput;

    /// <summary>True when there's more than one input to choose from.</summary>
    public bool HasInputs => Inputs.Count > 1;

    public string CurrentInputName => CurrentInput?.Name ?? "Input";

    [ObservableProperty]
    private bool _supportsVolume;

    [ObservableProperty]
    private double _volume;

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

    /// <summary>Follow adaptive brightness. By default external displays do and the built-in screen doesn't.</summary>
    public bool IsAdaptive
    {
        get => _settings.Adaptive ?? !IsInternal;
        set
        {
            if (value == IsAdaptive) return;
            _settings.Adaptive = value;
            _settings.AdaptiveOffset = 0;
            if (!value) _adaptiveLevel = null;
            _settingsChanged();
            OnPropertyChanged();
            OnAdaptiveOffsetChanged();
            AdaptiveToggled?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Raised when <see cref="IsAdaptive"/> changes, so the current level can be applied straight away.</summary>
    public event EventHandler? AdaptiveToggled;

    /// <summary>"+12% from adaptive" while the user's adjustment is in effect, otherwise null.</summary>
    public string? AdaptiveOffsetText => HasAdaptiveOffset ? $"{_settings.AdaptiveOffset * 100:+0;-0}% from adaptive" : null;

    public bool HasAdaptiveOffset => _adaptiveLevel is not null && Math.Abs(_settings.AdaptiveOffset) >= 0.005;

    /// <summary>True while adaptive brightness is driving this display.</summary>
    public bool IsFollowingAdaptive => _adaptiveLevel is not null;

    /// <summary>
    /// Sets brightness to <paramref name="fraction"/> of the way between this display's limits, as
    /// a user change (so under adaptive brightness it becomes the offset).
    /// </summary>
    public void SetLevel(double fraction)
    {
        if (SupportsBrightness) SetBrightnessQuietly(Range.FromFraction(fraction));
    }

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

        // Any change that didn't come from adaptive brightness (slider, hotkey, Link all, a new
        // limit) becomes the user's offset, so the next adaptive update keeps it.
        if (!_applyingAdaptive && _adaptiveLevel is double adaptive)
        {
            _settings.AdaptiveOffset = Math.Clamp(Range.ToFraction(percent) - adaptive, -1, 1);
            _settingsChanged();
            OnAdaptiveOffsetChanged();
        }

        if (!_suppressEditEvent) BrightnessEdited?.Invoke(this, EventArgs.Empty);
    }

    partial void OnContrastChanged(double value)
    {
        if (_loadingFromDisplay || !SupportsContrast) return;
        _lastEdit = Stopwatch.GetTimestamp();
        _display.SetContrast((int)Math.Round(value));
    }

    partial void OnVolumeChanged(double value)
    {
        if (_loadingFromDisplay || !SupportsVolume) return;
        _lastEdit = Stopwatch.GetTimestamp();
        _display.SetVolume((int)Math.Round(value));
    }

    /// <summary>
    /// Switches the monitor to another input. Choosing the input it's already on does nothing: some
    /// monitors blank briefly when asked to re-select their current input.
    /// </summary>
    public void SwitchInput(InputSource input)
    {
        if (input == CurrentInput || !Inputs.Contains(input)) return;
        _display.SetInput(input);
        _inputSwitchedAt = Stopwatch.GetTimestamp();
        CurrentInput = input;
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

    /// <summary>
    /// Follows adaptive brightness: <paramref name="level"/> (0–1) plus the user's offset, scaled
    /// into this display's limits.
    /// </summary>
    public void ApplyAdaptiveLevel(double level)
    {
        bool wasFollowing = _adaptiveLevel is not null;
        _adaptiveLevel = level;
        if (!wasFollowing) OnAdaptiveOffsetChanged();
        if (!SupportsBrightness) return;

        _applyingAdaptive = true;
        try
        {
            SetBrightnessQuietly(Range.FromFraction(level + _settings.AdaptiveOffset));
        }
        finally
        {
            _applyingAdaptive = false;
        }
    }

    /// <summary>Adaptive brightness stopped driving this display; manual changes no longer count as offsets.</summary>
    public void StopAdaptive()
    {
        _adaptiveLevel = null;
        OnAdaptiveOffsetChanged();
    }

    /// <summary>Drops the user's adjustment and goes back to the plain adaptive level.</summary>
    [RelayCommand]
    public void ResetAdaptiveOffset()
    {
        if (_settings.AdaptiveOffset == 0) return;
        _settings.AdaptiveOffset = 0;
        _settingsChanged();
        OnAdaptiveOffsetChanged();
        if (_adaptiveLevel is double level) ApplyAdaptiveLevel(level);
    }

    private void OnAdaptiveOffsetChanged()
    {
        OnPropertyChanged(nameof(AdaptiveOffsetText));
        OnPropertyChanged(nameof(HasAdaptiveOffset));
        OnPropertyChanged(nameof(IsFollowingAdaptive));
    }

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
                Volume = _display.Volume ?? 0;
            }
            SupportsVolume = _display.SupportsVolume;
            Inputs = _display.InputSources;
            bool recentlySwitched = _inputSwitchedAt != 0 && Stopwatch.GetElapsedTime(_inputSwitchedAt) < InputSwitchHold;
            if (!recentlySwitched) CurrentInput = _display.CurrentInput;
            Note = _display.Note;
            OnPropertyChanged(nameof(Details));
            OnPropertyChanged(nameof(MethodLabel));
        }
        finally
        {
            _loadingFromDisplay = false;
        }
    }
}
