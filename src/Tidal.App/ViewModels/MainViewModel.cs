using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tidal.Core.Displays;

namespace Tidal.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly DisplayManager _manager = new();
    private bool _refreshRequestedWhileBusy;

    public ObservableCollection<DisplayViewModel> Displays { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _status = "Detecting displays…";

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

    private async Task DetectAsync()
    {
        Status = "Detecting displays…";
        foreach (var display in Displays) display.Detach();
        Displays.Clear();

        try
        {
            var stopwatch = Stopwatch.StartNew();
            var displays = await _manager.RefreshAsync();
            foreach (var display in displays) Displays.Add(new DisplayViewModel(display));

            string count = displays.Count == 1 ? "1 display" : $"{displays.Count} displays";
            Status = $"{count} · detected in {stopwatch.Elapsed.TotalSeconds:0.0} s";
        }
        catch (Exception ex)
        {
            Status = $"Detection failed: {ex.Message}";
        }
    }

    public void Dispose()
    {
        foreach (var display in Displays) display.Detach();
        _manager.Dispose();
    }
}
