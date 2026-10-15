using System.Diagnostics;

namespace Tidal.Core.Displays;

/// <summary>
/// Runs all I/O for one device, one command at a time, on a dedicated background thread.
/// </summary>
/// <remarks>
/// Monitors process DDC/CI commands slowly (tens of milliseconds each) and some lock up when
/// flooded, for example by a slider being dragged. So:
/// <list type="bullet">
/// <item>Writes are coalesced per key: if brightness 40, 41 and 42 are queued while the monitor is
/// busy, only 42 is sent. A replaced write keeps its original place in line.</item>
/// <item>At least <c>minGap</c> passes between the end of one command and the start of the next.</item>
/// <item>Reads run in order with writes, so a read queued after a write runs after it.</item>
/// </list>
/// </remarks>
public sealed class DeviceCommandQueue : IDisposable
{
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(5);

    private readonly object _gate = new();
    private readonly List<Command> _pending = [];
    private readonly TimeSpan _minGap;
    private readonly Thread _thread;
    private long _lastCommandEnd;
    private bool _stopping;

    public DeviceCommandQueue(string name, TimeSpan minGap)
    {
        _minGap = minGap;
        _thread = new Thread(Run) { IsBackground = true, Name = $"Tidal I/O: {name}" };
        _thread.Start();
    }

    /// <summary>Queues a write, replacing any queued (not yet started) write with the same key.</summary>
    public void Post(object key, Action write)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (_gate)
        {
            if (_stopping) return;
            var command = new Command(key, write, Cancel: null);
            int index = _pending.FindIndex(c => key.Equals(c.Key));
            if (index >= 0) _pending[index] = command;
            else _pending.Add(command);
            Monitor.Pulse(_gate);
        }
    }

    /// <summary>Queues an operation that produces a result, such as a read. Never coalesced.</summary>
    public Task<T> RunAsync<T>(Func<T> operation)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            if (_stopping)
            {
                tcs.SetCanceled();
                return tcs.Task;
            }
            _pending.Add(new Command(
                Key: null,
                Execute: () =>
                {
                    try { tcs.TrySetResult(operation()); }
                    catch (Exception ex) { tcs.TrySetException(ex); }
                },
                Cancel: () => tcs.TrySetCanceled()));
            Monitor.Pulse(_gate);
        }
        return tcs.Task;
    }

    /// <summary>
    /// Stops the queue. Pending reads are cancelled; pending writes still go out, so the last value
    /// the user picked sticks. Waits briefly for the command in progress to finish.
    /// </summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_stopping) return;
            _stopping = true;
            foreach (var command in _pending) command.Cancel?.Invoke();
            _pending.RemoveAll(c => c.Key is null);
            Monitor.Pulse(_gate);
        }
        if (Thread.CurrentThread != _thread) _thread.Join(StopTimeout);
    }

    private void Run()
    {
        while (true)
        {
            lock (_gate)
            {
                while (_pending.Count == 0 && !_stopping) Monitor.Wait(_gate);
                if (_pending.Count == 0) return; // stopping, and all writes have drained
            }

            // Wait out the gap before dequeuing, so writes that arrive meanwhile still coalesce.
            var sinceLast = Stopwatch.GetElapsedTime(_lastCommandEnd);
            if (sinceLast < _minGap) Thread.Sleep(_minGap - sinceLast);

            Command command;
            lock (_gate)
            {
                command = _pending[0];
                _pending.RemoveAt(0);
            }

            try
            {
                command.Execute();
            }
            catch (Exception ex)
            {
                Log.Warning($"{_thread.Name}: command failed: {ex.Message}");
            }
            _lastCommandEnd = Stopwatch.GetTimestamp();
        }
    }

    private sealed record Command(object? Key, Action Execute, Action? Cancel);
}
