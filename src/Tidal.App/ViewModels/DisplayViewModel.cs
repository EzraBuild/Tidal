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
    private bool _loaded;

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

    /// <summary>How Tidal changes the brightness, for the badge on the monitor illustration.</summary>
    public string MethodLabel => !HasHardwareBrightness ? "Software dimming" : _display.Method switch
    {
        ControlMethod.DdcCi => "DDC/CI",
        ControlMethod.Wmi => "Windows brightness",
        _ => "Software dimming",
    };

    /// <summary>
    /// True when Tidal sets the display's own brightness. Otherwise brightness is software dimming:
    /// an overlay that darkens the picture (see <see cref="SoftwareDimming"/>).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UsesSoftwareBrightness), nameof(MethodLabel), nameof(Range), nameof(SliderMinimum))]
    private bool _hasHardwareBrightness;

    public bool UsesSoftwareBrightness => !HasHardwareBrightness;

    [ObservableProperty]
    private bool _supportsContrast;

    /// <summary>
    /// Brightness in percent. Below zero (only with <see cref="DimBelowZero"/>) the hardware stays
    /// at its lowest and the picture is dimmed in software. For a display with no hardware control
    /// it's all software dimming.
    /// </summary>
    [ObservableProperty]
    private double _brightness;

    /// <summary>How dark this display's dimming overlay is: 0 (none) to <see cref="SoftwareDimming.MaxOpacity"/>.</summary>
    [ObservableProperty]
    private double _dimOpacity;

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

    /// <summary>The brightness band Tidal keeps this display in, continuing below zero when it dims below zero.</summary>
    public BrightnessRange Range => new(_settings.MinBrightness, _settings.MaxBrightness, belowZero: HasHardwareBrightness && _settings.DimBelowZero);

    /// <summary>Where the brightness slider starts: below zero when dimming below zero, otherwise 0.</summary>
    public double SliderMinimum => Math.Min(Range.Bottom, 0);

    /// <summary>
    /// Keep dimming in software after the display reaches its lowest brightness. Only for displays
    /// with hardware brightness; the others are dimmed in software all the way.
    /// </summary>
    public bool DimBelowZero
    {
        get => _settings.DimBelowZero;
        set
        {
            if (value == _settings.DimBelowZero) return;
            _settings.DimBelowZero = value;
            _settingsChanged();
            // Out of the dimmed zone first, so the slider's minimum never rises above its value.
            if (!value && Brightness < 0) SetBrightnessQuietly(Range.Min);
            OnPropertyChanged();
            OnPropertyChanged(nameof(Range));
            OnPropertyChanged(nameof(SliderMinimum));
        }
    }

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
    public bool IsFollowingAdaptive => _adaptiveLevel is not null && AppPresetName is null;

    /// <summary>The app whose preset sets this display's brightness right now, or null.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAppPreset), nameof(IsFollowingAdaptive), nameof(AppPresetToolTip))]
    private string? _appPresetName;

    public bool HasAppPreset => AppPresetName is not null;

    public string? AppPresetToolTip => AppPresetName is null ? null : $"Brightness set for {AppPresetName}; it goes back when you switch apps";

    private double _appPresetLevel;

    /// <summary>
    /// Holds the display at an app's preset (<paramref name="fraction"/> of its range) while that app
    /// is in front. Adaptive brightness waits, and changes made meanwhile don't become adaptive offsets.
    /// </summary>
    public void HoldForAppPreset(string appName, double fraction)
    {
        if (AppPresetName == appName && _appPresetLevel == fraction) return; // already there; keep any nudge the user made
        AppPresetName = appName;
        _appPresetLevel = fraction;
        SetBrightnessQuietly(Range.FromFraction(fraction));
    }

    /// <summary>
    /// The preset's app is no longer in front: back to adaptive brightness if it drives this display,
    /// otherwise to <paramref name="previous"/>, the brightness from before the preset.
    /// </summary>
    public void ReleaseAppPreset(int previous)
    {
        if (AppPresetName is null) return;
        AppPresetName = null;
        if (_adaptiveLevel is double level) ApplyAdaptiveLevel(level);
        else SetBrightnessQuietly(Range.Clamp(previous));
    }

    /// <summary>
    /// Sets brightness to <paramref name="fraction"/> of the way between this display's limits, as
    /// a user change (so under adaptive brightness it becomes the offset).
    /// </summary>
    public void SetLevel(double fraction) => SetBrightnessQuietly(Range.FromFraction(fraction));

    partial void OnBrightnessChanged(double value)
    {
        if (_loadingFromDisplay) return;

        int percent = (int)Math.Round(value);
        int allowed = Range.Clamp(percent);
        if (allowed != percent)
        {
            Brightness = allowed; // the nested change does the write
            return;
        }

        _lastEdit = Stopwatch.GetTimestamp();
        ApplyBrightness(percent);

        // Any change that didn't come from adaptive brightness (slider, hotkey, Link all, a new
        // limit) becomes the user's offset, so the next adaptive update keeps it.
        if (!_applyingAdaptive && AppPresetName is null && _adaptiveLevel is double adaptive)
        {
            _settings.AdaptiveOffset = Math.Clamp(Range.ToFraction(percent) - adaptive, -DisplaySettings.MaxAdaptiveOffset, DisplaySettings.MaxAdaptiveOffset);
            _settingsChanged();
            OnAdaptiveOffsetChanged();
        }

        if (!_suppressEditEvent) BrightnessEdited?.Invoke(this, EventArgs.Empty);
    }

    // Sends a brightness to the hardware, the dimming overlay, or both.
    private void ApplyBrightness(int percent)
    {
        if (UsesSoftwareBrightness)
        {
            DimOpacity = SoftwareDimming.ForSoftwareBrightness(percent);
            Remember(belowZero: 0, software: percent);
            return;
        }

        if (percent >= 0) _display.SetBrightness(percent);
        else if (_display.Brightness is not int hardware || hardware > Range.Min) _display.SetBrightness(Range.Min); // dimming below zero starts from the lowest
        DimOpacity = SoftwareDimming.ForBelowZero(percent);
        Remember(belowZero: Math.Max(0, -percent), software: _settings.SoftwareBrightness);
    }

    // Software dimming has no hardware to read it back from, so it's kept in the settings.
    private void Remember(int belowZero, int? software)
    {
        if (_settings.BelowZeroLevel == belowZero && _settings.SoftwareBrightness == software) return;
        _settings.BelowZeroLevel = belowZero;
        _settings.SoftwareBrightness = software;
        _settingsChanged();
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
    public void StepBrightness(int delta) => Brightness = Range.Step((int)Math.Round(Brightness), delta);

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
        if (AppPresetName is not null) return; // an app preset holds the display; the level applies when it lets go

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
        var limits = new BrightnessRange(min, max);
        if (limits.Min != _settings.MinBrightness || limits.Max != _settings.MaxBrightness)
        {
            (_settings.MinBrightness, _settings.MaxBrightness) = (limits.Min, limits.Max);
            _settingsChanged();
            OnPropertyChanged(nameof(Range));
            OnPropertyChanged(nameof(LimitsDescription));

            // Respect the new limits right away rather than at the next change.
            int current = (int)Math.Round(Brightness);
            if (Range.Clamp(current) != current) SetBrightnessQuietly(Range.Clamp(current));
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
            bool hardware = _display.SupportsBrightness;
            bool controlChanged = !_loaded || hardware != HasHardwareBrightness; // e.g. a monitor that answered only after waking
            HasHardwareBrightness = hardware;
            SupportsContrast = _display.SupportsContrast;
            bool recentlyEdited = _lastEdit != 0 && Stopwatch.GetElapsedTime(_lastEdit) < EditHold;

            if (!hardware)
            {
                if (controlChanged) ShowLoadedBrightness(_settings.SoftwareBrightness ?? 100, SoftwareDimming.ForSoftwareBrightness);
            }
            else if (!recentlyEdited || controlChanged)
            {
                // Shown as read, even outside the limits: Tidal only enforces them when it changes the value.
                int read = _display.Brightness ?? 0;
                // Dimming below zero carries on while the hardware is still at its lowest; a brighter
                // reading (the monitor's own buttons) ends it.
                bool stillBelowZero = Range.BelowZero && _settings.BelowZeroLevel > 0 && read <= Range.Min;
                if (!stillBelowZero && _settings.BelowZeroLevel > 0)
                {
                    _settings.BelowZeroLevel = 0;
                    _settingsChanged();
                }
                ShowLoadedBrightness(stillBelowZero ? -_settings.BelowZeroLevel : read, SoftwareDimming.ForBelowZero);
            }

            if (!recentlyEdited)
            {
                Contrast = _display.Contrast ?? 0;
                Volume = _display.Volume ?? 0;
            }
            SupportsVolume = _display.SupportsVolume;
            Inputs = _display.InputSources;
            bool recentlySwitched = _inputSwitchedAt != 0 && Stopwatch.GetElapsedTime(_inputSwitchedAt) < InputSwitchHold;
            if (!recentlySwitched) CurrentInput = _display.CurrentInput;
            Note = _display.Note;
            OnPropertyChanged(nameof(MethodLabel));
            _loaded = true;
        }
        finally
        {
            _loadingFromDisplay = false;
        }
    }

    // While loading, brightness changes don't reach the hardware, so the overlay is set here.
    private void ShowLoadedBrightness(int percent, Func<int, double> dimming)
    {
        Brightness = percent;
        DimOpacity = dimming(percent);
    }
}
