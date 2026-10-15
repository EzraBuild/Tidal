using Velopack;

namespace Tidal.App;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        // Must run before anything else: the installer starts the app with special arguments on
        // install, update and uninstall, and this handles them (exiting when it does).
        VelopackApp.Build().Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
