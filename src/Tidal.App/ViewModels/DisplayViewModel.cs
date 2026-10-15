using System.Diagnostics;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tidal.Core.Displays;
using Wpf.Ui.Controls;

namespace Tidal.App.ViewModels;

public sealed partial class DisplayViewModel : ObservableObject
{
    // A hardware read can finish just after the user moves a slider, carrying the old value.
    // Ignore read values for a moment after an edit so the slider doesn't jump back.
    private static readonly TimeSpan EditHold = TimeSpan.FromSeconds(1.5);

    private readonly Display _display;
    private bool _loadingFromDisplay;
    private bool _applyingLinkedValue;
    private long _lastEdit;

    public DisplayViewModel(Display display)
    {
        _display = display;
        _display.ValuesRead += OnValuesRead;
        LoadFromDisplay();
    }

    /// <summary>Raised when the user (not a hardware read or "link all") changes the brightness.</summary>
    public event EventHandler? BrightnessEdited;

    public string Name => _display.Name;
    public bool IsPrimary => _display.Info.IsPrimary;
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

    partial void OnBrightnessChanged(double value)
    {
        if (_loadingFromDisplay || !SupportsBrightness) return;
        _lastEdit = Stopwatch.GetTimestamp();
        _display.SetBrightness((int)Math.Round(value));
        if (!_applyingLinkedValue) BrightnessEdited?.Invoke(this, EventArgs.Empty);
    }

    partial void OnContrastChanged(double value)
    {
        if (_loadingFromDisplay || !SupportsContrast) return;
        _lastEdit = Stopwatch.GetTimestamp();
        _display.SetContrast((int)Math.Round(value));
    }

    /// <summary>Sets brightness on behalf of another display's slider ("link all"), without echoing the edit.</summary>
    public void ApplyLinkedBrightness(double value)
    {
        _applyingLinkedValue = true;
        try
        {
            Brightness = value;
        }
        finally
        {
            _applyingLinkedValue = false;
        }
    }

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
