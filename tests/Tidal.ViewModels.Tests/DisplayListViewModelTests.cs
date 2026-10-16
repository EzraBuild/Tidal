using Tidal.Core.Displays;

namespace Tidal.ViewModels.Tests;

public sealed class DisplayListViewModelTests : IDisposable
{
    private readonly TemporarySettings _settings = new();

    private async Task<DisplayListViewModel> DetectAsync(params FakeDisplay[] displays)
    {
        var list = new DisplayListViewModel(_settings.Service, new FakeDetector(displays), new FakeDisplayWatcher());
        await list.RefreshAsync();
        return list;
    }

    [Fact]
    public async Task Detected_displays_show_their_brightness()
    {
        using var list = await DetectAsync(new FakeDisplay("a", 70), new FakeDisplay("b", 30));

        Assert.Equal(["a", "b"], list.Displays.Select(d => d.Id));
        Assert.Equal([70.0, 30.0], list.Displays.Select(d => d.Brightness));
        Assert.Equal("Tidal\na: 70%\nb: 30%", list.Summary);
    }

    [Fact]
    public async Task Link_all_moves_the_other_displays_to_the_same_level_within_their_limits()
    {
        _settings.Current.LinkAll = true;
        var limits = _settings.Current.GetDisplay("b");
        (limits.MinBrightness, limits.MaxBrightness) = (20, 60);
        var b = new FakeDisplay("b", 40);
        using var list = await DetectAsync(new FakeDisplay("a", 50), b);

        list.Displays[0].Brightness = 100;

        Assert.Equal(60, list.Displays[1].Brightness);
        Assert.Equal(60, b.Writes[^1]);
    }

    [Fact]
    public async Task Without_link_all_other_displays_stay_put()
    {
        var b = new FakeDisplay("b", 40);
        using var list = await DetectAsync(new FakeDisplay("a", 50), b);

        list.Displays[0].Brightness = 100;

        Assert.Equal(40, list.Displays[1].Brightness);
        Assert.Empty(b.Writes);
    }

    [Fact]
    public async Task A_display_without_hardware_control_is_dimmed_in_software()
    {
        var display = new FakeDisplay("a", brightness: null);
        using var list = await DetectAsync(display);
        var viewModel = list.Displays[0];

        viewModel.Brightness = 50;

        Assert.True(viewModel.UsesSoftwareBrightness);
        Assert.Equal(SoftwareDimming.ForSoftwareBrightness(50), viewModel.DimOpacity);
        Assert.Equal(50, _settings.Current.GetDisplay("a").SoftwareBrightness);
        Assert.Empty(display.Writes);
    }

    [Fact]
    public async Task Below_zero_keeps_the_hardware_at_its_lowest_and_dims_the_rest_in_software()
    {
        _settings.Current.GetDisplay("a").DimBelowZero = true;
        var display = new FakeDisplay("a", 10);
        using var list = await DetectAsync(display);
        var viewModel = list.Displays[0];

        viewModel.Brightness = -25;

        Assert.Equal(0, display.Writes[^1]);
        Assert.Equal(SoftwareDimming.ForBelowZero(-25), viewModel.DimOpacity);
        Assert.Equal(25, _settings.Current.GetDisplay("a").BelowZeroLevel);
    }

    public void Dispose() => _settings.Dispose();
}
