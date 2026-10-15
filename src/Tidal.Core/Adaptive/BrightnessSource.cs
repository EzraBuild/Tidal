namespace Tidal.Core.Adaptive;

/// <summary>
/// Something adaptive displays follow: produces a level from 0 (dimmest) to 1 (brightest) and
/// says when it changes.
/// </summary>
public abstract class BrightnessSource : IDisposable
{
    // Smaller changes aren't worth a (slow) DDC/CI write.
    private const double MinChange = 0.005;

    private readonly object _gate = new();
    private double? _level;

    /// <summary>The latest level, or null until the first reading.</summary>
    public double? Level
    {
        get { lock (_gate) return _level; }
    }

    /// <summary>Raised, on a background thread, when <see cref="Level"/> changes noticeably.</summary>
    public event EventHandler? LevelChanged;

    /// <summary>Begins producing levels. Subscribe to <see cref="LevelChanged"/> first.</summary>
    public abstract void Start();

    protected void Report(double level)
    {
        level = Math.Clamp(level, 0, 1);
        lock (_gate)
        {
            if (_level is double previous && Math.Abs(previous - level) < MinChange) return;
            _level = level;
        }
        LevelChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
    }
}
