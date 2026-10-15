using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tidal.Core.Displays;
using Wpf.Ui.Controls;

namespace Tidal.App.ViewModels;

public sealed partial class DisplayViewModel : ObservableObject
{
    private readonly Display _display;
    private bool _loadingFromDisplay;

    public DisplayViewModel(Display display)
    {
        _display = display;
        _display.ValuesRead += OnValuesRead;
        LoadFromDisplay();
    }

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
        if (!_loadingFromDisplay && SupportsBrightness) _display.SetBrightness((int)Math.Round(value));
    }

    partial void OnContrastChanged(double value)
    {
        if (!_loadingFromDisplay && SupportsContrast) _display.SetContrast((int)Math.Round(value));
    }

    /// <summary>Reads current values back from the display (e.g. after using the monitor's own buttons).</summary>
    [RelayCommand]
    private Task ReadAsync() => _display.RefreshAsync();

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
            Brightness = _display.Brightness ?? 0;
            Contrast = _display.Contrast ?? 0;
            Note = _display.Note;
            OnPropertyChanged(nameof(Details));
        }
        finally
        {
            _loadingFromDisplay = false;
        }
    }
}
