namespace Tidal.ViewModels;

/// <summary>
/// A timer that ticks on the UI thread, like a dispatcher timer but tied to no UI framework: it
/// posts to the synchronization context of the thread that created it. Start and stop it from that
/// thread too.
/// </summary>
internal sealed class UiTimer(Action tick) : IDisposable
{
    private readonly SynchronizationContext _context = UiThread.Capture();
    private Timer? _timer;
    // Bumped by every start and stop, so a tick already on its way to the UI thread is dropped.
    private int _generation;

    /// <summary>(Re)starts the timer: it ticks after <paramref name="due"/>, then every <paramref name="period"/> if one is given.</summary>
    public void Start(TimeSpan due, TimeSpan? period = null)
    {
        Stop();
        int generation = _generation;
        _timer = new Timer(_ => _context.Post(_ =>
        {
            if (generation == Volatile.Read(ref _generation)) tick();
        }, null), null, due, period ?? Timeout.InfiniteTimeSpan);
    }

    public void Stop()
    {
        Interlocked.Increment(ref _generation);
        _timer?.Dispose();
        _timer = null;
    }

    public void Dispose() => Stop();
}
