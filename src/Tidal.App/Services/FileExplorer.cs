using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Tidal.ViewModels.Platform;

namespace Tidal.App.Services;

/// <summary>Shows files in File Explorer.</summary>
internal sealed class FileExplorer : IFileManager
{
    public void ShowFile(string path)
    {
        string arguments = File.Exists(path) ? $"/select,\"{path}\"" : $"\"{Path.GetDirectoryName(path)}\"";
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", arguments) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException)
        {
            Debug.WriteLine($"[Tidal] Opening File Explorer failed: {ex.Message}");
        }
    }
}
