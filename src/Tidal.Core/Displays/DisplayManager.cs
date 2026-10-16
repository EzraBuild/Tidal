namespace Tidal.Core.Displays;

/// <summary>Owns the current set of displays and re-detects them on demand.</summary>
public sealed class DisplayManager(IDisplayDetector detector) : IDisposable
{
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private IReadOnlyList<Display> _displays = [];
    private bool _disposed;

    public IReadOnlyList<Display> Displays => _displays;

    /// <summary>
    /// Detects connected displays and probes what each one supports. The previous <see cref="Display"/>
    /// objects are disposed, since their handles go stale when the display setup changes.
    /// </summary>
    public async Task<IReadOnlyList<Display>> RefreshAsync(CancellationToken cancellationToken = default)
    {
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            DisposeAll(_displays);
            _displays = [];

            var displays = await Task.Run(detector.Detect, cancellationToken);
            try
            {
                // Each display has its own I/O thread, so slow DDC probes run side by side.
                await Task.WhenAll(displays.Select(d => InitializeAsync(d, cancellationToken)));
            }
            catch
            {
                DisposeAll(displays);
                throw;
            }
            _displays = displays;
            return displays;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private static async Task InitializeAsync(Display display, CancellationToken cancellationToken)
    {
        try
        {
            await display.InitializeAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Error($"{display.Name}: probing failed: {ex}");
        }
    }

    private static void DisposeAll(IEnumerable<Display> displays)
    {
        foreach (var display in displays) display.Dispose();
    }

    public void Dispose()
    {
        _refreshLock.Wait();
        try
        {
            _disposed = true;
            DisposeAll(_displays);
            _displays = [];
        }
        finally
        {
            _refreshLock.Release();
        }
    }
}
