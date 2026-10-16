using System.Runtime.InteropServices;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tidal.Core.Adaptive;
using Tidal.Core.Settings;
using Tidal.Windows.Adaptive;
using Windows.Devices.Geolocation;

namespace Tidal.App.ViewModels;

/// <summary>
/// Adaptive brightness: which source adaptive displays follow (the sun, the built-in screen or the
/// light sensor), the location for the sun, and a status line for the Settings window.
/// </summary>
public sealed partial class AdaptiveViewModel : ObservableObject, IDisposable
{
    private readonly AdaptiveSettings _settings;
    private readonly DisplayListViewModel _displays;
    private readonly Action _settingsChanged;
    private BrightnessSource? _source;

    internal AdaptiveViewModel(AdaptiveSettings settings, DisplayListViewModel displays, Action settingsChanged)
    {
        _settings = settings;
        _displays = displays;
        _settingsChanged = settingsChanged;
        IsBuiltInAvailable = WmiBuiltInDisplaySource.IsAvailable;
        IsSensorAvailable = WindowsLightSensorSource.IsAvailable;

        // Re-detected displays and newly switched-on displays pick up the current level at once.
        _displays.Displays.CollectionChanged += (_, _) => ApplyLevel();
        _displays.AdaptiveToggled += (_, _) => ApplyLevel();
        StartSource();
    }

    public bool IsBuiltInAvailable { get; }
    public bool IsSensorAvailable { get; }

    public string BuiltInDescription => IsBuiltInAvailable
        ? "External displays follow the laptop screen, including Windows' own brightness changes."
        : "Not available: this PC has no built-in screen whose brightness Windows reports.";

    public string SensorDescription => IsSensorAvailable
        ? "Brighter in a bright room, dimmer in the dark."
        : "Not available: this PC has no ambient light sensor.";

    public AdaptiveMode Mode
    {
        get => _settings.Mode;
        set
        {
            if (value == _settings.Mode) return;
            _settings.Mode = value;
            _settingsChanged();
            // Offsets were relative to the old source; they'd be meaningless against the new one.
            // Stop first so clearing them doesn't re-apply the old source's level.
            _displays.StopAdaptive();
            _displays.ResetAdaptiveOffsets();
            StartSource();
            OnPropertyChanged(nameof(Mode));
            OnPropertyChanged(nameof(ModeLabel));
            OnPropertyChanged(nameof(IsOff));
            OnPropertyChanged(nameof(IsSun));
            OnPropertyChanged(nameof(IsBuiltIn));
            OnPropertyChanged(nameof(IsSensor));
        }
    }

    /// <summary>Short name for the mode picker pill.</summary>
    public string ModeLabel => Mode switch
    {
        AdaptiveMode.Sun => "Sun",
        AdaptiveMode.BuiltInDisplay => "Match laptop",
        AdaptiveMode.LightSensor => "Light sensor",
        _ => "Manual",
    };

    // One property per radio button.
    public bool IsOff { get => Mode == AdaptiveMode.Off; set { if (value) Mode = AdaptiveMode.Off; } }
    public bool IsSun { get => Mode == AdaptiveMode.Sun; set { if (value) Mode = AdaptiveMode.Sun; } }
    public bool IsBuiltIn { get => Mode == AdaptiveMode.BuiltInDisplay; set { if (value) Mode = AdaptiveMode.BuiltInDisplay; } }
    public bool IsSensor { get => Mode == AdaptiveMode.LightSensor; set { if (value) Mode = AdaptiveMode.LightSensor; } }

    public double? Latitude
    {
        get => _settings.Latitude;
        set => SetLocation(value, _settings.Longitude);
    }

    public double? Longitude
    {
        get => _settings.Longitude;
        set => SetLocation(_settings.Latitude, value);
    }

    [ObservableProperty]
    private string _status = "";

    [ObservableProperty]
    private string? _locationError;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UseMyLocationCommand))]
    private bool _isLocating;

    /// <summary>Asks Windows for the PC's location. Rounded to about a kilometre, which is plenty for sunrise times.</summary>
    [RelayCommand(CanExecute = nameof(CanUseMyLocation))]
    private async Task UseMyLocationAsync()
    {
        LocationError = null;
        IsLocating = true;
        try
        {
            if (await Geolocator.RequestAccessAsync() != GeolocationAccessStatus.Allowed)
            {
                LocationError = "Windows location access is off for desktop apps. Turn it on in Settings › Privacy & security › Location, or type your coordinates.";
                return;
            }
            var locator = new Geolocator { DesiredAccuracy = PositionAccuracy.Default };
            var position = await locator.GetGeopositionAsync(TimeSpan.FromHours(1), TimeSpan.FromSeconds(15));
            var point = position.Coordinate.Point.Position;
            SetLocation(Math.Round(point.Latitude, 2), Math.Round(point.Longitude, 2));
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or TaskCanceledException or COMException)
        {
            LocationError = "Windows couldn't find a location. Type your coordinates instead.";
        }
        finally
        {
            IsLocating = false;
        }
    }

    private bool CanUseMyLocation() => !IsLocating;

    private void SetLocation(double? latitude, double? longitude)
    {
        latitude = latitude is double lat ? Math.Clamp(lat, -90, 90) : null;
        longitude = longitude is double lon ? Math.Clamp(lon, -180, 180) : null;
        if (latitude != _settings.Latitude || longitude != _settings.Longitude)
        {
            (_settings.Latitude, _settings.Longitude) = (latitude, longitude);
            _settingsChanged();
            if (Mode == AdaptiveMode.Sun) StartSource();
        }
        OnPropertyChanged(nameof(Latitude));
        OnPropertyChanged(nameof(Longitude));
    }

    private void StartSource()
    {
        if (_source is not null)
        {
            _source.LevelChanged -= OnLevelChanged;
            _source.Dispose();
            _source = null;
        }

        _source = Mode switch
        {
            AdaptiveMode.Sun when _settings is { Latitude: double lat, Longitude: double lon } => new SunSource(lat, lon),
            AdaptiveMode.BuiltInDisplay when IsBuiltInAvailable => new WmiBuiltInDisplaySource(),
            AdaptiveMode.LightSensor when IsSensorAvailable => new WindowsLightSensorSource(),
            _ => null,
        };

        if (_source is null)
        {
            _displays.StopAdaptive();
            UpdateStatus();
            return;
        }
        _source.LevelChanged += OnLevelChanged;
        _source.Start();
        ApplyLevel();
    }

    // Sources report from background threads.
    private void OnLevelChanged(object? sender, EventArgs e) => Application.Current?.Dispatcher.InvokeAsync(ApplyLevel);

    private void ApplyLevel()
    {
        if (_source?.Level is double level)
            _displays.ApplyAdaptiveLevel(level, skipBuiltIn: _source is BuiltInDisplaySource);
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        double? level = _source?.Level;
        string now = level is double l ? $"Now {l:P0}" : "Waiting for the first reading";
        Status = _source switch
        {
            SunSource sun => $"{now} · {DescribeSunTimes(sun.Today)}",
            BuiltInDisplaySource builtIn => builtIn.Percent is int percent ? $"Built-in screen at {percent}% · external displays follow it" : now,
            LightSensorSource sensor => sensor.Lux is double lux ? $"{now} · ambient light {lux:0} lux" : now,
            _ => Mode switch
            {
                AdaptiveMode.Off => "Off: brightness only changes when you change it.",
                AdaptiveMode.Sun => "Set your location to follow the sun.",
                _ => "This source isn't available on this PC.",
            },
        };
    }

    private static string DescribeSunTimes(SunDay day)
    {
        if (day.Sunrise is null) return day.NoonElevation > 0 ? "the sun doesn't set today" : "the sun doesn't rise today";
        return $"sunrise {day.Sunrise.Value.ToLocalTime():t} · sunset {day.Sunset!.Value.ToLocalTime():t}";
    }

    public void Dispose()
    {
        if (_source is null) return;
        _source.LevelChanged -= OnLevelChanged;
        _source.Dispose();
    }
}
