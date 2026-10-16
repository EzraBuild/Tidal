using Tidal.Windows.Startup;

namespace Tidal.Windows.Tests;

public class StartupCommandTests
{
    private const string Program = @"C:\Users\me\AppData\Local\Tidal\current\Tidal.exe";

    [Fact]
    public void The_command_quotes_the_path_and_starts_in_the_background()
    {
        Assert.Equal($"\"{Program}\" --background", StartupCommand.For(Program));
    }

    [Theory]
    [InlineData("\"C:\\Program Files\\Tidal\\Tidal.exe\" --background", @"C:\Program Files\Tidal\Tidal.exe")]
    [InlineData(@"C:\Tools\Tidal.exe --background", @"C:\Tools\Tidal.exe")]
    [InlineData(@"  C:\Tools\Tidal.exe  ", @"C:\Tools\Tidal.exe")]
    [InlineData("\"unterminated", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void The_program_is_read_back_from_a_command(string? command, string? expected)
    {
        Assert.Equal(expected, StartupCommand.ProgramOf(command));
    }

    [Fact]
    public void A_command_runs_a_program_regardless_of_case_or_quoting()
    {
        Assert.True(StartupCommand.Runs(StartupCommand.For(Program), Program));
        Assert.True(StartupCommand.Runs(Program.ToUpperInvariant(), Program));
        Assert.False(StartupCommand.Runs(StartupCommand.For(@"D:\Dev\Tidal\bin\Tidal.exe"), Program));
        Assert.False(StartupCommand.Runs(null, Program));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(new byte[0], true)]
    [InlineData(new byte[] { 0x02, 0, 0, 0 }, true)]
    [InlineData(new byte[] { 0x06, 0, 0, 0 }, true)]
    [InlineData(new byte[] { 0x03, 0x1F, 0x2C }, false)]
    [InlineData(new byte[] { 0x07, 0x1F, 0x2C }, false)]
    public void Startup_apps_can_turn_an_entry_off(byte[]? state, bool approved)
    {
        Assert.Equal(approved, StartupCommand.IsApproved(state));
    }
}
