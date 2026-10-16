using Tidal.Core.Settings;
using Tidal.ViewModels.Platform;

namespace Tidal.ViewModels.Tests;

public class AppPresetsViewModelTests
{
    private sealed class FakeCatalog(params string[] running) : IAppCatalog
    {
        public IEnumerable<string> FindRunningPrograms() => running;
        public string GetDisplayName(string programPath) => Path.GetFileNameWithoutExtension(programPath).ToUpperInvariant();
        public object? GetIcon(string programPath) => null;
    }

    [Fact]
    public void Open_apps_are_listed_once_by_name_leaving_out_those_with_a_preset()
    {
        var settings = new TidalSettings { AppPresets = [new AppPreset { App = "vlc.exe" }] };
        var catalog = new FakeCatalog("/apps/zed.exe", "/apps/vlc/vlc.exe", "/apps/code.exe", "/other/code.exe");
        var presets = new AppPresetsViewModel(settings, catalog, () => { });

        var apps = presets.FindRunningApps();

        Assert.Equal(["CODE", "ZED"], apps.Select(a => a.Name));
        Assert.Equal("/apps/code.exe", apps[0].Path);
    }

    [Fact]
    public void Adding_an_app_names_it_and_saves_once()
    {
        var settings = new TidalSettings();
        int saves = 0;
        var presets = new AppPresetsViewModel(settings, new FakeCatalog(), () => saves++);

        presets.Add("/apps/vlc/vlc.exe");
        presets.Add("/apps/vlc/vlc.exe"); // already has one

        var preset = Assert.Single(settings.AppPresets);
        Assert.Equal(("vlc.exe", "VLC", 100), (preset.App, preset.Name, preset.Brightness));
        Assert.Single(presets.Presets);
        Assert.Equal(1, saves);
    }
}
