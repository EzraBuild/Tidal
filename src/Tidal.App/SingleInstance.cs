using Tidal.App.Interop;

namespace Tidal.App;

/// <summary>
/// Keeps Tidal to one instance per user session. A second launch signals the first (which shows
/// its popup) and exits; two instances would fight over the same monitors.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\Tidal.SingleInstance";
    private const string ActivateEventName = @"Local\Tidal.Activate";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activate;
    private RegisteredWaitHandle? _registration;

    private SingleInstance(Mutex mutex, EventWaitHandle activate)
    {
        _mutex = mutex;
        _activate = activate;
    }

    /// <summary>Returns null if Tidal is already running, after asking that instance to show itself.</summary>
    public static SingleInstance? TryAcquire()
    {
        var activate = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
        var mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
        if (createdNew) return new SingleInstance(mutex, activate);

        // This launch came from the user, so it may take the foreground; pass that right on so the
        // running instance's popup can activate (and close again when focus moves away).
        NativeMethods.AllowSetForegroundWindow(NativeMethods.ASFW_ANY);
        activate.Set();
        activate.Dispose();
        mutex.Dispose();
        return null;
    }

    /// <summary>Calls <paramref name="onActivate"/> (on a thread-pool thread) whenever another launch is attempted.</summary>
    public void ListenForActivation(Action onActivate)
        => _registration = ThreadPool.RegisterWaitForSingleObject(
            _activate, (_, _) => onActivate(), null, Timeout.Infinite, executeOnlyOnce: false);

    public void Dispose()
    {
        _registration?.Unregister(null);
        _activate.Dispose();
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
