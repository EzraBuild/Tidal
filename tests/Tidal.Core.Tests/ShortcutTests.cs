using Tidal.Core.Hotkeys;

namespace Tidal.Core.Tests;

public class ShortcutTests
{
    [Theory]
    [InlineData("Ctrl+Alt+PageUp")]
    [InlineData("Ctrl+Shift+Win+F5")]
    [InlineData("Alt+=")]
    [InlineData("Ctrl+-")]
    [InlineData("Win+NumPlus")]
    [InlineData("F13")]
    public void Formatting_round_trips(string text)
    {
        Assert.True(Shortcut.TryParse(text, out var shortcut));
        Assert.Equal(text, shortcut.ToString());
    }

    [Fact]
    public void Parsing_is_forgiving_about_case_spacing_aliases_and_modifier_order()
    {
        Assert.True(Shortcut.TryParse(" alt + control + pageup ", out var shortcut));
        Assert.Equal(new Shortcut(ShortcutModifiers.Control | ShortcutModifiers.Alt, 0x21), shortcut);
        Assert.Equal("Ctrl+Alt+PageUp", shortcut.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Ctrl+Alt+")]      // no key
    [InlineData("Ctrl+Alt")]       // modifiers only
    [InlineData("PageUp")]         // needs a modifier
    [InlineData("Hyper+PageUp")]   // unknown modifier
    [InlineData("Ctrl+Banana")]    // unknown key
    public void Rejects_invalid_text(string? text)
    {
        Assert.False(Shortcut.TryParse(text, out _));
        Assert.Null(Shortcut.ParseOrNull(text));
    }

    [Fact]
    public void Modifier_values_match_RegisterHotKey()
    {
        // MOD_ALT, MOD_CONTROL, MOD_SHIFT, MOD_WIN
        Assert.Equal(0x1, (int)ShortcutModifiers.Alt);
        Assert.Equal(0x2, (int)ShortcutModifiers.Control);
        Assert.Equal(0x4, (int)ShortcutModifiers.Shift);
        Assert.Equal(0x8, (int)ShortcutModifiers.Windows);
    }
}
