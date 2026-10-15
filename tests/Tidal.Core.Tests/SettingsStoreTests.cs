using Tidal.Core.Settings;

namespace Tidal.Core.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "TidalTests", Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(_directory, "settings.json");

    [Fact]
    public void A_missing_file_gives_defaults()
    {
        var settings = new SettingsStore(FilePath).Load();

        Assert.False(settings.LinkAll);
        Assert.Equal("Ctrl+Alt+PageUp", settings.Hotkeys.BrightnessUp);
        Assert.Equal(5, settings.Hotkeys.Step);
        Assert.Empty(settings.Displays);
    }

    [Fact]
    public void Settings_round_trip_and_the_directory_is_created()
    {
        var store = new SettingsStore(FilePath);
        var saved = new TidalSettings { LinkAll = true };
        saved.Hotkeys.BrightnessDown = null;
        saved.Hotkeys.Target = HotkeyTarget.DisplayUnderPointer;
        var display = saved.GetDisplay(@"\\?\DISPLAY#DELA1C8#x");
        display.MinBrightness = 20;
        display.MaxBrightness = 80;

        store.Save(saved);
        var loaded = store.Load();

        Assert.True(loaded.LinkAll);
        Assert.Null(loaded.Hotkeys.BrightnessDown);
        Assert.Equal(HotkeyTarget.DisplayUnderPointer, loaded.Hotkeys.Target);
        Assert.Equal((20, 80), (loaded.Displays[@"\\?\DISPLAY#DELA1C8#x"].MinBrightness, loaded.Displays[@"\\?\DISPLAY#DELA1C8#x"].MaxBrightness));
        string json = File.ReadAllText(FilePath);
        Assert.Contains("\"DisplayUnderPointer\"", json); // enums are readable text
        Assert.Contains("\"Ctrl+Alt+PageUp\"", json);      // no + escapes
        Assert.False(File.Exists(FilePath + ".tmp"));
    }

    [Fact]
    public void A_corrupt_file_is_kept_aside_and_defaults_are_used()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(FilePath, "{ \"LinkAll\": tru");

        var settings = new SettingsStore(FilePath).Load();

        Assert.False(settings.LinkAll);
        Assert.Equal("{ \"LinkAll\": tru", File.ReadAllText(FilePath + ".bad"));
    }

    [Fact]
    public void Hand_edited_values_are_repaired()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(FilePath, """
            {
              // comments and trailing commas are tolerated
              "Hotkeys": null,
              "Displays": {
                "a": { "MinBrightness": 90, "MaxBrightness": 10, "BelowZeroLevel": 400, "SoftwareBrightness": -3, "AdaptiveOffset": -9 },
                "b": null,
              },
            }
            """);

        var settings = new SettingsStore(FilePath).Load();

        Assert.NotNull(settings.Hotkeys);
        var a = settings.Displays["a"];
        Assert.Equal((10, 90), (a.MinBrightness, a.MaxBrightness));
        Assert.Equal(50, a.BelowZeroLevel);
        Assert.Equal(0, a.SoftwareBrightness);
        Assert.Equal(-2, a.AdaptiveOffset);
        Assert.False(settings.Displays.ContainsKey("b"));
    }

    [Fact]
    public void Step_is_clamped_to_a_sensible_range()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(FilePath, """{ "Hotkeys": { "Step": 500 } }""");

        Assert.Equal(HotkeySettings.MaxStep, new SettingsStore(FilePath).Load().Hotkeys.Step);
    }

    [Fact]
    public void A_legacy_settings_file_is_moved_once()
    {
        string legacy = Path.Combine(_directory, "old", "settings.json");
        string current = Path.Combine(_directory, "new", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(legacy)!);
        File.WriteAllText(legacy, """{ "LinkAll": true }""");

        Assert.True(SettingsStore.MoveLegacyFile(legacy, current));
        Assert.False(File.Exists(legacy));
        Assert.True(new SettingsStore(current).Load().LinkAll);

        // A later run (or an existing new file) leaves things alone.
        File.WriteAllText(legacy, "{}");
        Assert.False(SettingsStore.MoveLegacyFile(legacy, current));
        Assert.True(new SettingsStore(current).Load().LinkAll);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
