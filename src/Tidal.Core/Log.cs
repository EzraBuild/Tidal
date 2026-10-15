namespace Tidal.Core;

public enum LogLevel { Debug, Info, Warning, Error }

/// <summary>Minimal logging hook. The app or the diagnostics tool decides where lines go.</summary>
public static class Log
{
    /// <summary>Raised for every log line, on whichever thread wrote it.</summary>
    public static event Action<LogLevel, string>? Written;

    internal static void Debug(string message) => Write(LogLevel.Debug, message);
    internal static void Info(string message) => Write(LogLevel.Info, message);
    internal static void Warning(string message) => Write(LogLevel.Warning, message);
    internal static void Error(string message) => Write(LogLevel.Error, message);

    private static void Write(LogLevel level, string message)
    {
        System.Diagnostics.Debug.WriteLine($"[Tidal {level}] {message}");
        Written?.Invoke(level, message);
    }
}
