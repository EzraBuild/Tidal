using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using Tidal.App.ViewModels;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace Tidal.App;

public partial class MainWindow : FluentWindow
{
    // Windows fires several display-change events per plug/unplug, and monitors need a moment
    // before they answer DDC/CI, so detection waits for things to settle.
    private static readonly TimeSpan DisplayChangeSettleTime = TimeSpan.FromSeconds(1.5);
    private static readonly TimeSpan ResumeSettleTime = TimeSpan.FromSeconds(4);

    private readonly MainViewModel _viewModel = new();
    private readonly DispatcherTimer _redetectTimer;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        SystemThemeWatcher.Watch(this);

        _redetectTimer = new DispatcherTimer { Interval = DisplayChangeSettleTime };
        _redetectTimer.Tick += async (_, _) =>
        {
            _redetectTimer.Stop();
            await _viewModel.RefreshAsync();
        };

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        Loaded += async (_, _) => await _viewModel.RefreshAsync();
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) => ScheduleRedetect(DisplayChangeSettleTime);

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume) ScheduleRedetect(ResumeSettleTime);
    }

    // SystemEvents fire on their own thread; restart the timer on the UI thread.
    private void ScheduleRedetect(TimeSpan delay) => Dispatcher.InvokeAsync(() =>
    {
        _redetectTimer.Stop();
        _redetectTimer.Interval = delay;
        _redetectTimer.Start();
    });

    protected override void OnClosed(EventArgs e)
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        _redetectTimer.Stop();
        _viewModel.Dispose(); // flushes any pending brightness writes
        base.OnClosed(e);
    }
}
