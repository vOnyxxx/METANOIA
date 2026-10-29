using System.Windows;
using System.Windows.Controls;

namespace Metanoia.App.Controls;

/// <summary>Keyboard-editable numeric parameter with a consistently styled slider.</summary>
public partial class ParameterSlider : UserControl
{
    public ParameterSlider() => InitializeComponent();
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(ParameterSlider), new PropertyMetadata(""));
    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(ParameterSlider), new PropertyMetadata(0d));
    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(ParameterSlider), new PropertyMetadata(100d));
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(double), typeof(ParameterSlider), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, null, CoerceValue));
    private static object CoerceValue(DependencyObject obj, object value)
    {
        var control = (ParameterSlider)obj;
        var number = (double)value;
        return double.IsFinite(number) ? Math.Clamp(number, control.Minimum, Math.Max(control.Minimum, control.Maximum)) : control.Minimum;
    }
    public static readonly DependencyProperty StepProperty = DependencyProperty.Register(nameof(Step), typeof(double), typeof(ParameterSlider), new PropertyMetadata(0.01d));
    public static readonly DependencyProperty SnapProperty = DependencyProperty.Register(nameof(Snap), typeof(bool), typeof(ParameterSlider), new PropertyMetadata(false));
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public double Minimum { get => (double)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double Step { get => (double)GetValue(StepProperty); set => SetValue(StepProperty, value); }
    public bool Snap { get => (bool)GetValue(SnapProperty); set => SetValue(SnapProperty, value); }
}
