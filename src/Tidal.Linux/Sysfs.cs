using System.Text;

namespace Tidal.Linux;

/// <summary>
/// The parts of sysfs Tidal reads and writes. Detection and backlights go through this, so tests
/// can run them against a made-up machine.
/// </summary>
public interface ISysfs
{
    /// <summary>Names of the entries in a directory, or none if it doesn't exist.</summary>
    IReadOnlyList<string> List(string directory);

    /// <summary>A file's text without the trailing newline, or null if it can't be read.</summary>
    string? ReadText(string path);

    /// <summary>A file's bytes, or null if it can't be read.</summary>
    byte[]? ReadBytes(string path);

    /// <summary>
    /// The last part of where a symbolic link points (<c>i2c-5</c> for <c>../../i2c-5</c>), or null
    /// if the path isn't a link.
    /// </summary>
    string? LinkName(string path);

    /// <summary>The path with symbolic links resolved.</summary>
    string RealPath(string path);

    /// <summary>Writes a file. Returns false if this user may not; throws <see cref="IOException"/> for other failures.</summary>
    bool TryWrite(string path, string text);
}

/// <summary>The real sysfs.</summary>
public sealed class Sysfs : ISysfs
{
    public IReadOnlyList<string> List(string directory)
    {
        try
        {
            return Directory.EnumerateFileSystemEntries(directory).Select(Path.GetFileName).OfType<string>().ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    public string? ReadText(string path) => ReadBytes(path) is { } bytes ? Encoding.UTF8.GetString(bytes).TrimEnd('\n') : null;

    public byte[]? ReadBytes(string path)
    {
        try
        {
            // Read until the end rather than trusting the size: sysfs reports 4096 for every text
            // attribute and 0 for binary ones like edid.
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, bufferSize: 0);
            using var contents = new MemoryStream();
            var buffer = new byte[4096];
            int read;
            while ((read = file.Read(buffer)) > 0) contents.Write(buffer, 0, read);
            return contents.ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public string? LinkName(string path)
        => new FileInfo(path).LinkTarget is { } target ? target.TrimEnd('/').Split('/')[^1] : null;

    public string RealPath(string path)
    {
        try
        {
            return Directory.ResolveLinkTarget(path, returnFinalTarget: true)?.FullName ?? path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return path;
        }
    }

    public bool TryWrite(string path, string text)
    {
        try
        {
            using var handle = File.OpenHandle(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
            RandomAccess.Write(handle, Encoding.ASCII.GetBytes(text), fileOffset: 0);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
