using Tidal.Core.Settings;
using Tidal.ViewModels.Platform;

namespace Tidal.ViewModels.Tests;

public class GeneralViewModelTests
{
    private sealed class FakeStartup : ISignInStartup
    {
        public bool IsEnabled { get; set; }
        public bool IsTurnedOffBySystem => false;
        public Exception? Refusal { get; set; }

        public void Enable()
        {
            if (Refusal is not null) throw Refusal;
            IsEnabled = true;
        }

        public void Disable() => IsEnabled = false;
    }

    private sealed class FakeFiles : IFileManager
    {
        public string? Shown { get; private set; }
        public void ShowFile(string path) => Shown = path;
    }

    private static GeneralViewModel Create(ISignInStartup startup, IFileManager? files = null)
    {
        var settings = new TidalSettings { AutoUpdate = false };
        var updates = new UpdatesViewModel(settings, () => { }, () => { });
        return new GeneralViewModel(updates, startup, files ?? new FakeFiles());
    }

    [Fact]
    public void Starting_at_sign_in_is_read_from_and_written_to_the_system()
    {
        var startup = new FakeStartup();
        var general = Create(startup);

        general.StartAtSignIn = true;

        Assert.True(startup.IsEnabled);
        Assert.True(general.StartAtSignIn);
        Assert.Null(general.StartupError);
    }

    [Fact]
    public void A_refused_change_is_explained()
    {
        var startup = new FakeStartup { Refusal = new UnauthorizedAccessException("Access is denied.") };
        var general = Create(startup);

        general.StartAtSignIn = true;

        Assert.False(general.StartAtSignIn);
        Assert.Equal("The system didn't allow the change: Access is denied.", general.StartupError);
    }

    [Fact]
    public void Show_settings_file_opens_the_settings_path()
    {
        var files = new FakeFiles();
        var general = Create(new FakeStartup(), files);

        general.ShowSettingsFileCommand.Execute(null);

        Assert.Equal(general.SettingsPath, files.Shown);
    }

    [Fact]
    public void A_copy_that_was_not_installed_cannot_update()
    {
        var general = Create(new FakeStartup());

        Assert.False(general.Updates.CanUpdate);
        Assert.False(general.Updates.CheckNowCommand.CanExecute(null));
    }
}
