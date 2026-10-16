namespace Tidal.Core.Startup;

/// <summary>
/// The command Windows runs at sign-in to start Tidal, and how to read one back. Windows keeps it
/// as a value under the user's Run key; its "Startup apps" list (Settings and Task Manager) can turn
/// it off without deleting it.
/// </summary>
public static class StartupCommand
{
    /// <summary>Starts Tidal in the tray without showing the popup.</summary>
    public const string BackgroundArgument = "--background";

    /// <summary>The command line that starts <paramref name="programPath"/> in the background.</summary>
    public static string For(string programPath) => $"\"{programPath}\" {BackgroundArgument}";

    /// <summary>The program a command line runs: its first part, quoted or not. Null for an empty command.</summary>
    public static string? ProgramOf(string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) return null;
        command = command.Trim();
        if (command[0] == '"')
        {
            int end = command.IndexOf('"', 1);
            return end > 1 ? command[1..end] : null;
        }
        int space = command.IndexOf(' ');
        return space < 0 ? command : command[..space];
    }

    /// <summary>True when <paramref name="command"/> starts the program at <paramref name="programPath"/>.</summary>
    public static bool Runs(string? command, string programPath)
    {
        if (ProgramOf(command) is not string program) return false;
        try
        {
            return string.Equals(Path.GetFullPath(program), Path.GetFullPath(programPath), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false; // a hand-edited value that isn't a path
        }
    }

    /// <summary>
    /// Whether Windows' "Startup apps" list allows a startup entry, from its StartupApproved value:
    /// none means allowed; otherwise the lowest bit of the first byte is set when the user turned it off.
    /// </summary>
    public static bool IsApproved(byte[]? startupApproved) => startupApproved is not { Length: > 0 } || (startupApproved[0] & 1) == 0;
}
