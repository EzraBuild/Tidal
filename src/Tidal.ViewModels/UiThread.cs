namespace Tidal.ViewModels;

/// <summary>
/// The UI thread, whichever UI framework runs it. View models change only there, so updates from
/// hardware, sensors and system events are posted to it.
/// </summary>
internal static class UiThread
{
    /// <summary>
    /// The calling thread's synchronization context: the UI thread's, when called from a view model
    /// constructor. Tests have none; their posts go to the thread pool.
    /// </summary>
    public static SynchronizationContext Capture() => SynchronizationContext.Current ?? new SynchronizationContext();
}
