using CommunityToolkit.Mvvm.ComponentModel;
using Tidal.App.Services;
using Tidal.Core.Hotkeys;
using Tidal.Core.Settings;

namespace Tidal.App.ViewModels;

public enum HotkeyAction
{
    BrightnessUp = 1,
    BrightnessDown = 2,
}

/// <summary>The brightness hotkeys: which keys, how far each press goes, and which displays it moves.</summary>
public sealed partial class ShortcutsViewModel : ObservableObject
{
    private const string TakenMessage = "Windows or another app is already using this shortcut.";

    private readonly HotkeySettings _settings;
    private readonly HotkeyManager _hotkeys;
    private readonly Action _settingsChanged;
    private readonly Dictionary<HotkeyAction, string> _conflicts = [];
    private readonly HashSet<HotkeyAction> _taken = [];
    private bool _isRecordingUp;
    private bool _isRecordingDown;

    internal ShortcutsViewModel(HotkeySettings settings, HotkeyManager hotkeys, Action settingsChanged)
    {
        _settings = settings;
        _hotkeys = hotkeys;
        _settingsChanged = settingsChanged;
        RegisterAll();
    }

    public Shortcut? BrightnessUp
    {
        get => Shortcut.ParseOrNull(_settings.BrightnessUp);
        set => Assign(HotkeyAction.BrightnessUp, value);
    }

    public Shortcut? BrightnessDown
    {
        get => Shortcut.ParseOrNull(_settings.BrightnessDown);
        set => Assign(HotkeyAction.BrightnessDown, value);
    }

    [ObservableProperty]
    private string? _brightnessUpError;

    [ObservableProperty]
    private string? _brightnessDownError;

    /// <summary>Percentage points per press. Nullable because NumberBox edits a double?.</summary>
    public double? Step
    {
        get => _settings.Step;
        set
        {
            if (value is double v)
            {
                int step = Math.Clamp((int)Math.Round(v), HotkeySettings.MinStep, HotkeySettings.MaxStep);
                if (step != _settings.Step)
                {
                    _settings.Step = step;
                    _settingsChanged();
                }
            }
            OnPropertyChanged();
        }
    }

    /// <summary>Only change the display under the mouse pointer, instead of all of them.</summary>
    public bool OnlyDisplayUnderPointer
    {
        get => _settings.Target == HotkeyTarget.DisplayUnderPointer;
        set => SetTarget(value ? HotkeyTarget.DisplayUnderPointer : HotkeyTarget.AllDisplays);
    }

    /// <summary>The opposite choice, for a two-option segmented control.</summary>
    public bool AppliesToAllDisplays
    {
        get => _settings.Target == HotkeyTarget.AllDisplays;
        set { if (value) SetTarget(HotkeyTarget.AllDisplays); }
    }

    private void SetTarget(HotkeyTarget target)
    {
        if (target == _settings.Target) return;
        _settings.Target = target;
        _settingsChanged();
        OnPropertyChanged(nameof(OnlyDisplayUnderPointer));
        OnPropertyChanged(nameof(AppliesToAllDisplays));
    }

    // One flag per shortcut box: sharing one would make both boxes show "Press a shortcut…".
    public bool IsRecordingUp
    {
        get => _isRecordingUp;
        set { if (SetProperty(ref _isRecordingUp, value)) UpdateSuspension(); }
    }

    public bool IsRecordingDown
    {
        get => _isRecordingDown;
        set { if (SetProperty(ref _isRecordingDown, value)) UpdateSuspension(); }
    }

    /// <summary>
    /// True while a shortcut box is listening for keys. The hotkeys are released meanwhile, so the
    /// user can press the current combination without triggering it.
    /// </summary>
    private bool IsRecording => _isRecordingUp || _isRecordingDown;

    private void UpdateSuspension()
    {
        if (IsRecording) UnregisterAll();
        else RegisterAll();
    }

    private void Assign(HotkeyAction action, Shortcut? shortcut)
    {
        var other = action == HotkeyAction.BrightnessUp ? BrightnessDown : BrightnessUp;
        if (shortcut is not null && shortcut == other)
        {
            // Kept apart from registration errors so re-registering when recording ends doesn't wipe it.
            _conflicts[action] = $"Already used for brightness {(action == HotkeyAction.BrightnessUp ? "down" : "up")}.";
            UpdateError(action);
            OnPropertyChanged(action.ToString()); // put the box back to the old shortcut
            return;
        }

        _conflicts.Remove(action);
        string? text = shortcut?.ToString();
        if (action == HotkeyAction.BrightnessUp) _settings.BrightnessUp = text;
        else _settings.BrightnessDown = text;
        _settingsChanged();

        if (!IsRecording) Register(action);
        else UpdateError(action);
        OnPropertyChanged(action.ToString());
    }

    private void RegisterAll()
    {
        Register(HotkeyAction.BrightnessUp);
        Register(HotkeyAction.BrightnessDown);
    }

    private void UnregisterAll()
    {
        _hotkeys.Unregister((int)HotkeyAction.BrightnessUp);
        _hotkeys.Unregister((int)HotkeyAction.BrightnessDown);
    }

    private void Register(HotkeyAction action)
    {
        var shortcut = action == HotkeyAction.BrightnessUp ? BrightnessUp : BrightnessDown;
        _taken.Remove(action);
        if (shortcut is null) _hotkeys.Unregister((int)action);
        // Kept in settings even when taken: it may become free once the other app exits.
        else if (!_hotkeys.Register((int)action, shortcut.Value)) _taken.Add(action);
        UpdateError(action);
    }

    private void UpdateError(HotkeyAction action)
    {
        string? error = _conflicts.GetValueOrDefault(action) ?? (_taken.Contains(action) ? TakenMessage : null);
        if (action == HotkeyAction.BrightnessUp) BrightnessUpError = error;
        else BrightnessDownError = error;
    }
}
