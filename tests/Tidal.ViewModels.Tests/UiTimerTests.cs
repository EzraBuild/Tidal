namespace Tidal.ViewModels.Tests;

public class UiTimerTests
{
    [Fact]
    public async Task It_ticks_once_after_the_delay()
    {
        int ticks = 0;
        using var timer = new UiTimer(() => Interlocked.Increment(ref ticks));

        timer.Start(TimeSpan.FromMilliseconds(20));
        await Task.Delay(300);

        Assert.Equal(1, ticks);
    }

    [Fact]
    public async Task Starting_again_restarts_the_countdown()
    {
        int ticks = 0;
        using var timer = new UiTimer(() => Interlocked.Increment(ref ticks));

        timer.Start(TimeSpan.FromMilliseconds(300));
        await Task.Delay(150);
        timer.Start(TimeSpan.FromMilliseconds(600));
        await Task.Delay(400); // past the first deadline, before the second

        Assert.Equal(0, ticks);
        await Task.Delay(600);
        Assert.Equal(1, ticks);
    }

    [Fact]
    public async Task A_stopped_timer_does_not_tick()
    {
        int ticks = 0;
        using var timer = new UiTimer(() => Interlocked.Increment(ref ticks));

        timer.Start(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(50));
        timer.Stop();
        await Task.Delay(250);

        Assert.Equal(0, ticks);
    }

    [Fact]
    public async Task With_a_period_it_keeps_ticking()
    {
        int ticks = 0;
        using var timer = new UiTimer(() => Interlocked.Increment(ref ticks));

        timer.Start(TimeSpan.Zero, TimeSpan.FromMilliseconds(30));
        await Task.Delay(400);

        Assert.True(ticks >= 3, $"ticked {ticks} times");
    }
}
