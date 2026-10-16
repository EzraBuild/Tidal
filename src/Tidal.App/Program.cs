using System.Diagnostics;
using Tidal.App.Services;
using Velopack;

namespace Tidal.App;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        // Must run before anything else: the installer starts the app with special arguments on
        // install, update and uninstall, and this handles them (exiting when it does).
        VelopackApp.Build()
            .OnAfterInstallFastCallback(_ => ChangeStartup(startup => startup.Enable()))  // a fresh install starts with Windows
            .OnBeforeUninstallFastCallback(_ => ChangeStartup(startup => startup.Disable())) // and leaves nothing behind
            .Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }

    // Installing and uninstalling must never fail over the startup entry.
    private static void ChangeStartup(Action<WindowsStartup> change)
    {
        try
        {
            change(new WindowsStartup());
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Tidal] Changing start with Windows failed: {ex.Message}");
        }
    }
}
