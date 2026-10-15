namespace Tidal.Core.Hotkeys;

/// <summary>Modifier keys. Values match RegisterHotKey's MOD_* flags.</summary>
[Flags]
public enum ShortcutModifiers
{
    None = 0,
    Alt = 0x1,
    Control = 0x2,
    Shift = 0x4,
    Windows = 0x8,
}

/// <summary>
/// A key combination such as <c>Ctrl+Alt+PageUp</c>. <see cref="Key"/> is a Win32 virtual-key code.
/// Settings store shortcuts in their text form.
/// </summary>
public readonly record struct Shortcut(ShortcutModifiers Modifiers, int Key)
{
    private static readonly (ShortcutModifiers Flag, string Name)[] ModifierNames =
    [
        (ShortcutModifiers.Control, "Ctrl"),
        (ShortcutModifiers.Alt, "Alt"),
        (ShortcutModifiers.Shift, "Shift"),
        (ShortcutModifiers.Windows, "Win"),
    ];

    private static readonly Dictionary<string, ShortcutModifiers> ModifierAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Ctrl"] = ShortcutModifiers.Control,
        ["Control"] = ShortcutModifiers.Control,
        ["Alt"] = ShortcutModifiers.Alt,
        ["Shift"] = ShortcutModifiers.Shift,
        ["Win"] = ShortcutModifiers.Windows,
        ["Windows"] = ShortcutModifiers.Windows,
    };

    // Keys a shortcut may use, by virtual-key code. Punctuation names follow the US layout.
    private static readonly Dictionary<int, string> KeyNames = BuildKeyNames();
    private static readonly Dictionary<string, int> KeysByName =
        KeyNames.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// True for a known key with at least one modifier. Function keys may stand alone, since F13–F24
    /// exist precisely for this kind of use.
    /// </summary>
    public bool IsValid => KeyNames.ContainsKey(Key) && (Modifiers != ShortcutModifiers.None || IsFunctionKey(Key));

    public override string ToString()
    {
        var modifiers = Modifiers;
        var parts = ModifierNames.Where(m => modifiers.HasFlag(m.Flag)).Select(m => m.Name).ToList();
        parts.Add(KeyNames.TryGetValue(Key, out var name) ? name : $"0x{Key:X2}");
        return string.Join("+", parts);
    }

    public static bool TryParse(string? text, out Shortcut shortcut)
    {
        shortcut = default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var parts = text.Split('+', StringSplitOptions.TrimEntries);
        var modifiers = ShortcutModifiers.None;
        foreach (var part in parts[..^1])
        {
            if (!ModifierAliases.TryGetValue(part, out var modifier)) return false;
            modifiers |= modifier;
        }
        if (!KeysByName.TryGetValue(parts[^1], out int key)) return false;

        shortcut = new Shortcut(modifiers, key);
        return shortcut.IsValid;
    }

    /// <summary>Parses settings text; null when the text is empty or not a valid shortcut.</summary>
    public static Shortcut? ParseOrNull(string? text) => TryParse(text, out var shortcut) ? shortcut : null;

    private static bool IsFunctionKey(int key) => key is >= 0x70 and <= 0x87;

    private static Dictionary<int, string> BuildKeyNames()
    {
        var names = new Dictionary<int, string>
        {
            [0x08] = "Backspace", [0x09] = "Tab", [0x0D] = "Enter", [0x13] = "Pause", [0x1B] = "Esc",
            [0x20] = "Space", [0x21] = "PageUp", [0x22] = "PageDown", [0x23] = "End", [0x24] = "Home",
            [0x25] = "Left", [0x26] = "Up", [0x27] = "Right", [0x28] = "Down",
            [0x2C] = "PrintScreen", [0x2D] = "Insert", [0x2E] = "Delete",
            [0x6A] = "NumMultiply", [0x6B] = "NumPlus", [0x6D] = "NumMinus", [0x6E] = "NumDecimal", [0x6F] = "NumDivide",
            [0x91] = "ScrollLock",
            [0xAD] = "VolumeMute", [0xAE] = "VolumeDown", [0xAF] = "VolumeUp",
            [0xB0] = "MediaNext", [0xB1] = "MediaPrevious", [0xB2] = "MediaStop", [0xB3] = "MediaPlayPause",
            [0xBA] = ";", [0xBB] = "=", [0xBC] = ",", [0xBD] = "-", [0xBE] = ".", [0xBF] = "/", [0xC0] = "`",
            [0xDB] = "[", [0xDC] = "\\", [0xDD] = "]", [0xDE] = "'",
        };
        for (int i = 0; i <= 9; i++) names[0x30 + i] = i.ToString();
        for (int i = 0; i < 26; i++) names[0x41 + i] = ((char)('A' + i)).ToString();
        for (int i = 0; i <= 9; i++) names[0x60 + i] = $"Num{i}";
        for (int i = 1; i <= 24; i++) names[0x6F + i] = $"F{i}";
        return names;
    }
}
