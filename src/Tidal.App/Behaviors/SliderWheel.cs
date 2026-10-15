using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Tidal.App.Behaviors;

/// <summary>Lets the mouse wheel move a slider by <c>Step</c> per notch, like the Windows volume flyout.</summary>
public static class SliderWheel
{
    public static readonly DependencyProperty StepProperty = DependencyProperty.RegisterAttached(
        "Step", typeof(double), typeof(SliderWheel), new PropertyMetadata(0.0, OnStepChanged));

    public static double GetStep(Slider slider) => (double)slider.GetValue(StepProperty);

    public static void SetStep(Slider slider, double value) => slider.SetValue(StepProperty, value);

    private static void OnStepChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Slider slider) return;
        slider.PreviewMouseWheel -= OnPreviewMouseWheel;
        if ((double)e.NewValue > 0) slider.PreviewMouseWheel += OnPreviewMouseWheel;
    }

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var slider = (Slider)sender;
        double change = GetStep(slider) * Math.Sign(e.Delta);
        slider.Value = Math.Clamp(slider.Value + change, slider.Minimum, slider.Maximum);
        e.Handled = true;
    }
}
