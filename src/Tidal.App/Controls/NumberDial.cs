using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Tidal.App.Controls;

/// <summary>
/// A big number you can change in place: scroll the mouse wheel (Shift for steps of 10), use the
/// arrow keys, or click and type. Commits on Enter or when focus leaves; Esc reverts.
/// </summary>
public sealed class NumberDial : TextBox
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double?), typeof(NumberDial),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((NumberDial)d).ShowValue()));

    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum), typeof(double), typeof(NumberDial), new PropertyMetadata(0.0));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(NumberDial), new PropertyMetadata(100.0));

    public NumberDial()
    {
        TextAlignment = TextAlignment.Center;
        ShowValue();
    }

    public double? Value
    {
        get => (double?)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
    {
        base.OnPreviewMouseWheel(e);
        if (!IsEnabled) return;
        Nudge(Math.Sign(e.Delta) * (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 1));
        e.Handled = true;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        int step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 1;
        switch (e.Key)
        {
            case Key.Up: Nudge(step); e.Handled = true; break;
            case Key.Down: Nudge(-step); e.Handled = true; break;
            case Key.Enter: Commit(); SelectAll(); e.Handled = true; break;
            case Key.Escape: ShowValue(); SelectAll(); e.Handled = true; break;
        }
    }

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        SelectAll();
    }

    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        Commit();
    }

    private void Nudge(int delta)
    {
        Commit(); // keep anything typed so far
        Value = Math.Clamp(Math.Round(Value ?? Minimum) + delta, Minimum, Maximum);
        ShowValue(); // the bound value may have been adjusted (e.g. by limits)
    }

    private void Commit()
    {
        if (double.TryParse(Text.Trim().TrimEnd('%'), NumberStyles.Float, CultureInfo.CurrentCulture, out double typed))
        {
            double clamped = Math.Clamp(Math.Round(typed), Minimum, Maximum);
            if (clamped != Value) Value = clamped;
        }
        ShowValue();
    }

    private void ShowValue() => Text = Value is double value ? value.ToString("0", CultureInfo.CurrentCulture) : "–";
}
