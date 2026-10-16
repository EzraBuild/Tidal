using Tidal.Core.Displays;
using Tidal.Core.Settings;
using Tidal.ViewModels;
using Tidal.ViewModels.Platform;

namespace Tidal.ViewModels.Tests;

/// <summary>A display that answers at once and records what Tidal writes to it.</summary>
internal sealed class FakeDisplay(string id, int? brightness) : Display(new DisplayInfo
{
    Id = id,
    Name = id,
    DeviceName = id,
    Bounds = new ScreenRect(0, 0, 1920, 1080),
})
{
    /// <summary>Brightness writes, in order.</summary>
    public List<int> Writes { get; } = [];

    public override ControlMethod Method => SupportsBrightness ? ControlMethod.DdcCi : ControlMethod.None;

    protected override Task InitializeAsync(CancellationToken cancellationToken)
    {
        SupportsBrightness = brightness is not null;
        OnValuesRead(brightness, null);
        return Task.CompletedTask;
    }

    public override Task<bool> RefreshAsync(CancellationToken cancellationToken = default) => Task.FromResult(SupportsBrightness);

    protected override void WriteBrightness(int percent) => Writes.Add(percent);
}

internal sealed class FakeDetector(params Display[] displays) : IDisplayDetector
{
    public IReadOnlyList<Display> Detect() => displays;
}

internal sealed class FakeDisplayWatcher : IDisplayWatcher
{
    public event EventHandler? DisplaysChanged { add { } remove { } }
    public event EventHandler? Resumed { add { } remove { } }
}

/// <summary>Settings in a temporary folder, deleted afterwards.</summary>
internal sealed class TemporarySettings : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "TidalTests", Guid.NewGuid().ToString("N"));

    public TemporarySettings() => Service = new SettingsService(new SettingsStore(Path.Combine(_directory, "settings.json")));

    public SettingsService Service { get; }

    public TidalSettings Current => Service.Current;

    public void Dispose()
    {
        Service.SaveNow(); // also cancels a save that's still scheduled
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
