using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tidal.Core;
using Tidal.Core.Settings;
using Velopack;
using Velopack.Sources;

namespace Tidal.ViewModels;

/// <summary>
/// Keeps an installed Tidal up to date from its GitHub releases: checks a minute after starting and
/// every few hours, downloads new versions in the background, and installs them when you restart
/// Tidal from the popup or Settings, or otherwise when Tidal quits.
/// </summary>
public sealed partial class UpdatesViewModel : ObservableObject, IDisposable
{
    private const string ReleasesUrl = "https://github.com/EzraBuild/Tidal";
    // For trying out a release before publishing it: a folder or URL with Velopack's release files.
    private const string SourceOverrideVariable = "TIDAL_UPDATE_URL";
    private static readonly TimeSpan FirstCheckDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);

    private readonly TidalSettings _settings;
    private readonly Action _settingsChanged;
    private readonly Action _quit;
    private readonly UpdateManager? _manager;
    private readonly SynchronizationContext _ui = UiThread.Capture();
    private readonly UiTimer _timer;
    private VelopackAsset? _ready;

    /// <param name="quit">Quits Tidal, for restarting into a new version.</param>
    public UpdatesViewModel(TidalSettings settings, Action settingsChanged, Action quit)
    {
        _settings = settings;
        _settingsChanged = settingsChanged;
        _quit = quit;
        _timer = new UiTimer(() => _ = CheckAsync());

        try
        {
            string? overrideSource = Environment.GetEnvironmentVariable(SourceOverrideVariable);
            var manager = string.IsNullOrWhiteSpace(overrideSource)
                ? new UpdateManager(new GithubSource(ReleasesUrl, accessToken: null, prerelease: false))
                : new UpdateManager(overrideSource);
            if (manager.IsInstalled) _manager = manager;
        }
        catch (Exception ex)
        {
            Log.Warning($"Updates unavailable: {ex.Message}");
        }

        if (_manager is null)
        {
            Status = "Updates work in installed copies of Tidal; this one isn't installed.";
            return;
        }

        // Downloaded last time but not installed yet (e.g. Tidal was ended rather than quit).
        if (_manager.UpdatePendingRestart is { } pending) MarkReady(pending);
        else Status = $"Tidal {_manager.CurrentVersion}. Not checked for updates yet.";
        if (AutoUpdate) StartTimer();
    }

    /// <summary>False for copies that weren't installed (e.g. a development build).</summary>
    public bool CanUpdate => _manager is not null;

    /// <summary>Check for and download updates on its own.</summary>
    public bool AutoUpdate
    {
        get => _settings.AutoUpdate;
        set
        {
            if (value == _settings.AutoUpdate) return;
            _settings.AutoUpdate = value;
            _settingsChanged();
            OnPropertyChanged();
            if (value && CanUpdate) StartTimer();
            else _timer.Stop();
        }
    }

    [ObservableProperty]
    private string _status = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckNowCommand))]
    private bool _isBusy;

    /// <summary>The downloaded version waiting to be installed, e.g. "0.1.6"; null when there's none.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReady))]
    [NotifyCanExecuteChangedFor(nameof(RestartToUpdateCommand))]
    private string? _readyVersion;

    public bool IsReady => ReadyVersion is not null;

    [RelayCommand(CanExecute = nameof(CanCheckNow))]
    private Task CheckNowAsync() => CheckAsync();

    private bool CanCheckNow() => CanUpdate && !IsBusy;

    /// <summary>Quits Tidal, installs the downloaded version and starts it again.</summary>
    [RelayCommand(CanExecute = nameof(IsReady))]
    private void RestartToUpdate()
    {
        if (_manager is null || _ready is null) return;
        _manager.WaitExitThenApplyUpdates(_ready, silent: true, restart: true);
        _ready = null; // the updater has it now; quitting mustn't start a second one
        _quit();
    }

    /// <summary>Called as Tidal quits: installs a downloaded version, without starting Tidal again.</summary>
    public void ApplyOnExit()
    {
        if (_manager is null || _ready is null) return;
        try
        {
            _manager.WaitExitThenApplyUpdates(_ready, silent: true, restart: false);
        }
        catch (Exception ex)
        {
            Log.Warning($"Installing the update on exit failed: {ex.Message}");
        }
    }

    private async Task CheckAsync()
    {
        if (_manager is null || IsBusy || _ready is not null) return;
        IsBusy = true;
        Status = "Checking for updates…";
        try
        {
            var update = await _manager.CheckForUpdatesAsync();
            if (update is null)
            {
                Status = $"Tidal {_manager.CurrentVersion} is the latest version.";
                return;
            }

            string version = update.TargetFullRelease.Version.ToString();
            Status = $"Downloading {version}…";
            await _manager.DownloadUpdatesAsync(update, percent =>
                _ui.Post(_ => Status = $"Downloading {version}… {percent}%", null));
            MarkReady(update.TargetFullRelease);
        }
        catch (Exception ex)
        {
            Status = $"Couldn't check for updates: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void MarkReady(VelopackAsset asset)
    {
        _ready = asset;
        ReadyVersion = asset.Version.ToString();
        Status = $"Tidal {ReadyVersion} is ready. Restart to update now, or it installs when Tidal quits.";
    }

    private void StartTimer() => _timer.Start(FirstCheckDelay, CheckInterval);

    public void Dispose() => _timer.Dispose();
}
