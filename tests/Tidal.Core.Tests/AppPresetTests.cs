using Tidal.Core.Settings;

namespace Tidal.Core.Tests;

public class AppPresetTests
{
    [Theory]
    [InlineData(@"C:\Program Files\VideoLAN\VLC\vlc.exe")]
    [InlineData("VLC.EXE")]
    [InlineData("vlc")]
    [InlineData(" vlc.exe ")]
    public void Apps_match_by_program_file_name(string programPath)
    {
        Assert.True(new AppPreset { App = "vlc.exe" }.Matches(programPath));
    }

    [Theory]
    [InlineData("vlc2.exe")]
    [InlineData(@"C:\Tools\vlc.com")]
    [InlineData("")]
    public void Other_programs_do_not_match(string programPath)
    {
        Assert.False(new AppPreset { App = "vlc.exe" }.Matches(programPath));
    }

    [Fact]
    public void Find_returns_the_preset_for_the_program_in_front()
    {
        var settings = new TidalSettings();
        settings.AppPresets.Add(new AppPreset { App = "vlc.exe", Brightness = 100 });
        settings.AppPresets.Add(new AppPreset { App = "game.exe", Brightness = 40 });

        Assert.Equal(40, settings.FindAppPreset(@"D:\Games\Game.exe")?.Brightness);
        Assert.Null(settings.FindAppPreset(@"C:\Windows\explorer.exe"));
    }

    [Fact]
    public void The_display_name_falls_back_to_the_program_name()
    {
        Assert.Equal("vlc", new AppPreset { App = "vlc.exe" }.DisplayName);
        Assert.Equal("VLC media player", new AppPreset { App = "vlc.exe", Name = "VLC media player" }.DisplayName);
    }
}
