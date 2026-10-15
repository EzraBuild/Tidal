using System.Collections.Concurrent;
using System.Diagnostics;
using Tidal.Core.Displays;

namespace Tidal.Core.Tests;

public class DeviceCommandQueueTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Writes_to_the_same_key_are_coalesced_to_the_latest_value()
    {
        using var queue = new DeviceCommandQueue("test", TimeSpan.Zero);
        var executed = new ConcurrentQueue<int>();
        using var gate = new ManualResetEventSlim();

        var blocker = queue.RunAsync(() => gate.Wait(Timeout)); // keeps the "monitor" busy
        for (int value = 1; value <= 10; value++)
        {
            int v = value;
            queue.Post("brightness", () => executed.Enqueue(v));
        }
        gate.Set();
        await queue.RunAsync(() => 0).WaitAsync(Timeout);

        Assert.True(await blocker);
        Assert.Equal(new[] { 10 }, executed);
    }

    [Fact]
    public async Task A_replaced_write_keeps_its_place_in_line()
    {
        using var queue = new DeviceCommandQueue("test", TimeSpan.Zero);
        var executed = new ConcurrentQueue<string>();
        using var gate = new ManualResetEventSlim();

        _ = queue.RunAsync(() => gate.Wait(Timeout));
        queue.Post("a", () => executed.Enqueue("a1"));
        queue.Post("b", () => executed.Enqueue("b1"));
        queue.Post("a", () => executed.Enqueue("a2"));
        gate.Set();
        await queue.RunAsync(() => 0).WaitAsync(Timeout);

        Assert.Equal(new[] { "a2", "b1" }, executed);
    }

    [Fact]
    public async Task Reads_run_after_writes_queued_before_them()
    {
        using var queue = new DeviceCommandQueue("test", TimeSpan.Zero);
        int value = 0;
        using var gate = new ManualResetEventSlim();

        _ = queue.RunAsync(() => gate.Wait(Timeout));
        queue.Post("x", () => value = 42);
        var read = queue.RunAsync(() => value);
        gate.Set();

        Assert.Equal(42, await read.WaitAsync(Timeout));
    }

    [Fact]
    public async Task Commands_are_spaced_by_the_minimum_gap()
    {
        var gap = TimeSpan.FromMilliseconds(80);
        using var queue = new DeviceCommandQueue("test", gap);
        var times = new ConcurrentQueue<long>();

        for (int i = 0; i < 4; i++) queue.Post(i, () => times.Enqueue(Stopwatch.GetTimestamp()));
        await queue.RunAsync(() => 0).WaitAsync(Timeout);

        var stamps = times.ToArray();
        Assert.Equal(4, stamps.Length);
        for (int i = 1; i < stamps.Length; i++)
        {
            var elapsed = Stopwatch.GetElapsedTime(stamps[i - 1], stamps[i]);
            Assert.True(elapsed >= gap - TimeSpan.FromMilliseconds(10), $"gap {i} was only {elapsed.TotalMilliseconds} ms");
        }
    }

    [Fact]
    public async Task A_failing_command_does_not_stop_the_queue()
    {
        using var queue = new DeviceCommandQueue("test", TimeSpan.Zero);
        queue.Post("x", () => throw new InvalidOperationException("boom"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => queue.RunAsync<int>(() => throw new InvalidOperationException()));
        Assert.Equal(7, await queue.RunAsync(() => 7).WaitAsync(Timeout));
    }

    [Fact]
    public async Task Dispose_flushes_pending_writes_and_cancels_pending_reads()
    {
        var queue = new DeviceCommandQueue("test", TimeSpan.Zero);
        using var gate = new ManualResetEventSlim();
        bool written = false;

        _ = queue.RunAsync(() => gate.Wait(Timeout));
        queue.Post("x", () => written = true);
        var read = queue.RunAsync(() => 1);

        var dispose = Task.Run(queue.Dispose);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => read.WaitAsync(Timeout));
        gate.Set();
        await dispose.WaitAsync(Timeout);

        Assert.True(written);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queue.RunAsync(() => 1));
    }
}
