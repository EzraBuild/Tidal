using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tidal.Core.Settings;

/// <summary>Reads and writes <see cref="TidalSettings"/> as JSON.</summary>
public sealed class SettingsStore(string filePath)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        // Keep "Ctrl+Alt+PageUp" and device paths readable instead of + escapes; the file
        // never ends up in HTML, which is what the default escaping guards against.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// %APPDATA%\Tidal\settings.json. Not under %LOCALAPPDATA%\Tidal: the installer owns that folder,
    /// replaces it on updates and deletes it on uninstall.
    /// </summary>
    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Tidal", "settings.json");

    /// <summary>Where settings lived before installer support (v0.1 development builds).</summary>
    public static string LegacyPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tidal", "settings.json");

    public string FilePath { get; } = filePath;

    /// <summary>
    /// Moves a settings file from <paramref name="legacyPath"/> to <paramref name="currentPath"/>
    /// unless one already exists there. Returns true if a file was moved.
    /// </summary>
    public static bool MoveLegacyFile(string legacyPath, string currentPath)
    {
        if (!File.Exists(legacyPath) || File.Exists(currentPath)) return false;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(currentPath)!);
            File.Move(legacyPath, currentPath);
            Log.Info($"Moved settings from {legacyPath} to {currentPath}");
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warning($"Couldn't move old settings file: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Loads settings. A missing file gives defaults. A file that can't be read is copied aside to
    /// <c>settings.json.bad</c> (so the next save doesn't silently destroy it) and defaults are used.
    /// </summary>
    public TidalSettings Load()
    {
        if (!File.Exists(FilePath)) return new TidalSettings();
        try
        {
            using var stream = File.OpenRead(FilePath);
            var settings = JsonSerializer.Deserialize<TidalSettings>(stream, Options) ?? new TidalSettings();
            settings.Normalize();
            return settings;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            Log.Warning($"Couldn't read settings from {FilePath} ({ex.Message}); using defaults.");
            TryCopy(FilePath, FilePath + ".bad");
            return new TidalSettings();
        }
    }

    /// <summary>Saves settings, replacing the file in one step so a crash can't leave it half-written.</summary>
    public void Save(TidalSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        string temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, Options));
        File.Move(temp, FilePath, overwrite: true);
    }

    private static void TryCopy(string from, string to)
    {
        try
        {
            File.Copy(from, to, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warning($"Couldn't keep a copy of the unreadable settings file: {ex.Message}");
        }
    }
}
