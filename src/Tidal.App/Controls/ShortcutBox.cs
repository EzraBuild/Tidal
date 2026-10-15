using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Tidal.Core.Hotkeys;
using Button = Wpf.Ui.Controls.Button;

namespace Tidal.App.Controls;

/// <summary>
/// Shows a keyboard shortcut; click it and press a new combination to change it. Esc cancels,
/// Backspace or Delete clears the shortcut.
/// </summary>
public sealed class ShortcutBox : Button
{
    public static readonly DependencyProperty ShortcutProperty = DependencyProperty.Register(
        nameof(Shortcut), typeof(Shortcut?), typeof(ShortcutBox),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnStateChanged));

    public static readonly DependencyProperty IsRecordingProperty = DependencyProperty.Register(
        nameof(IsRecording), typeof(bool), typeof(ShortcutBox),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnStateChanged));

    public ShortcutBox()
    {
        // WPF-UI's styles are keyed by exact type, so a subclass would fall back to the classic
        // Windows button look; reuse the Fluent button style explicitly.
        SetResourceReference(StyleProperty, typeof(Button));
        MinWidth = 180;
        HorizontalContentAlignment = HorizontalAlignment.Left;
        UpdateContent();
    }

    public Shortcut? Shortcut
    {
        get => (Shortcut?)GetValue(ShortcutProperty);
        set => SetValue(ShortcutProperty, value);
    }

    public bool IsRecording
    {
        get => (bool)GetValue(IsRecordingProperty);
        set => SetValue(IsRecordingProperty, value);
    }

    protected override void OnClick()
    {
        base.OnClick();
        IsRecording = !IsRecording;
        if (IsRecording) Focus();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (!IsRecording)
        {
            base.OnPreviewKeyDown(e);
            return;
        }
        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key; // Alt combinations arrive as Key.System
        var modifiers = CurrentModifiers();

        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            Content = modifiers == ShortcutModifiers.None ? "Press a shortcut…" : $"{DescribeModifiers(modifiers)} + …";
            return;
        }
        if (modifiers == ShortcutModifiers.None && key == Key.Escape)
        {
            IsRecording = false;
            return;
        }
        if (modifiers == ShortcutModifiers.None && key is Key.Back or Key.Delete)
        {
            Shortcut = null;
            IsRecording = false;
            return;
        }

        var shortcut = new Shortcut(modifiers, KeyInterop.VirtualKeyFromKey(key));
        if (!shortcut.IsValid)
        {
            Content = "Add Ctrl, Alt, Shift or Win";
            return;
        }
        Shortcut = shortcut;
        IsRecording = false;
    }

    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        IsRecording = false;
    }

    private static ShortcutModifiers CurrentModifiers()
    {
        var keys = Keyboard.Modifiers;
        var modifiers = ShortcutModifiers.None;
        if (keys.HasFlag(ModifierKeys.Control)) modifiers |= ShortcutModifiers.Control;
        if (keys.HasFlag(ModifierKeys.Alt)) modifiers |= ShortcutModifiers.Alt;
        if (keys.HasFlag(ModifierKeys.Shift)) modifiers |= ShortcutModifiers.Shift;
        if (keys.HasFlag(ModifierKeys.Windows)) modifiers |= ShortcutModifiers.Windows;
        return modifiers;
    }

    private static string DescribeModifiers(ShortcutModifiers modifiers)
    {
        var names = new List<string>();
        if (modifiers.HasFlag(ShortcutModifiers.Control)) names.Add("Ctrl");
        if (modifiers.HasFlag(ShortcutModifiers.Alt)) names.Add("Alt");
        if (modifiers.HasFlag(ShortcutModifiers.Shift)) names.Add("Shift");
        if (modifiers.HasFlag(ShortcutModifiers.Windows)) names.Add("Win");
        return string.Join(" + ", names);
    }

    private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((ShortcutBox)d).UpdateContent();

    private void UpdateContent()
    {
        if (IsRecording)
        {
            Content = "Press a shortcut…";
            return;
        }
        if (Shortcut is not { } shortcut)
        {
            Content = "Not set";
            return;
        }
        // Each key as a keycap: Ctrl Alt PageUp.
        var keys = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (string key in shortcut.ToString().Split('+')) keys.Children.Add(CreateKeycap(key));
        Content = keys;
    }

    private static Border CreateKeycap(string key)
    {
        var keycap = new Border
        {
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1, 1, 1, 2.5),
            Padding = new Thickness(8, 2, 8, 3),
            Margin = new Thickness(0, 0, 5, 0),
            Child = new TextBlock { Text = key, FontSize = 12, FontWeight = FontWeights.SemiBold },
        };
        keycap.SetResourceReference(Border.BackgroundProperty, "TidalChipBrush");
        keycap.SetResourceReference(Border.BorderBrushProperty, "TidalChipHoverBrush");
        return keycap;
    }
}
