namespace Tidal.ViewModels.Platform;

/// <summary>The system's file manager.</summary>
public interface IFileManager
{
    /// <summary>Shows a file, selected, in the file manager; or its folder when the file doesn't exist.</summary>
    void ShowFile(string path);
}
