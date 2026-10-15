using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using Tidal.Core.Settings;

namespace Tidal.App.Services;

/// <summary>Holds the live settings and writes them to disk shortly after they change.</summary>
public sealed class SettingsService
{
    // Dragging a value changes settings many times a second; write once things settle.
    private static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(750);

    private readonly SettingsStore _store;
    private readonly DispatcherTimer _saveTimer;

    public SettingsService(SettingsStore store)
    {
        _store = store;
        Current = store.Load();
        _saveTimer = new DispatcherTimer { Interval = SaveDelay };
        _saveTimer.Tick += (_, _) => SaveNow();
    }

    public TidalSettings Current { get; }

    public void ScheduleSave()
    {
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    public void SaveNow()
    {
        _saveTimer.Stop();
        try
        {
            _store.Save(Current);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"[Tidal] Saving settings failed: {ex.Message}");
        }
    }
}
