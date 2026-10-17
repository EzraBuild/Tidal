using Tidal.Core;
using Tidal.Core.Displays;

namespace Tidal.Linux.Displays;

/// <summary>A built-in screen whose backlight Tidal sets through the kernel (see <see cref="Backlight"/>).</summary>
public sealed class BacklightDisplay : Display
{
    // Backlight writes are quick; the gap just keeps slider drags from piling up.
    private static readonly TimeSpan CommandGap = TimeSpan.FromMilliseconds(15);

    private readonly Backlight _backlight;
    private readonly DeviceCommandQueue _queue;

    internal BacklightDisplay(DisplayInfo info, Backlight backlight) : base(info)
    {
        _backlight = backlight;
        _queue = new DeviceCommandQueue(info.Name, CommandGap);
    }

    public override ControlMethod Method => SupportsBrightness ? ControlMethod.Backlight : ControlMethod.None;

    protected override async Task InitializeAsync(CancellationToken cancellationToken)
    {
        int? brightness = await _queue.RunAsync(_backlight.ReadPercent).WaitAsync(cancellationToken);
        SupportsBrightness = brightness.HasValue;
        Note = SupportsBrightness ? null : $"Linux didn't report a brightness level for this screen's backlight ({_backlight.Name}).";
        Log.Info($"{Name}: backlight {_backlight.Name} ({_backlight.Type}, max {_backlight.MaxBrightness}) brightness={brightness?.ToString() ?? "unavailable"}");
        OnValuesRead(brightness, null);
    }

    public override async Task<bool> RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (!SupportsBrightness)
        {
            await InitializeAsync(cancellationToken);
            return SupportsBrightness;
        }
        int? brightness = await _queue.RunAsync(_backlight.ReadPercent).WaitAsync(cancellationToken);
        OnValuesRead(brightness, null);
        return brightness.HasValue;
    }

    protected override void WriteBrightness(int percent) => _queue.Post(VcpCode.Brightness, () => _backlight.WritePercent(percent));

    protected override void Dispose(bool disposing)
    {
        if (disposing) _queue.Dispose();
    }
}
