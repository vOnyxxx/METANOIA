using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Metanoia.App.Controls;

/// <summary>Short, interruptible transitions. Windows' animation preference is authoritative.</summary>
public static class Motion
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached("Enabled", typeof(bool), typeof(Motion), new PropertyMetadata(false, Changed));
    public static bool GetEnabled(DependencyObject obj) => (bool)obj.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject obj, bool value) => obj.SetValue(EnabledProperty, value);
    private static void Changed(DependencyObject obj, DependencyPropertyChangedEventArgs args)
    {
        if (obj is not FrameworkElement element || args.NewValue is not true) return;
        if (element is Border border)
        {
            var light = new DropShadowEffect { Color = Color.FromRgb(114, 220, 232), ShadowDepth = 0, BlurRadius = 18, Opacity = 0 };
            border.Effect = light;
            border.MouseEnter += (_, _) => Illuminate(.14);
            border.MouseLeave += (_, _) => Illuminate(0);
            void Illuminate(double value)
            {
                if (SystemParameters.ClientAreaAnimation) light.BeginAnimation(DropShadowEffect.OpacityProperty, new DoubleAnimation(value, TimeSpan.FromMilliseconds(180)));
            }
        }
        if (element is Button)
        {
            var scale = new ScaleTransform(1, 1);
            element.RenderTransform = scale;
            element.RenderTransformOrigin = new Point(.5, .5);
            element.MouseEnter += (_, _) => AnimateScale(1.025);
            element.MouseLeave += (_, _) => AnimateScale(1);
            void AnimateScale(double value)
            {
                if (!SystemParameters.ClientAreaAnimation) return;
                var animation = new DoubleAnimation(value, TimeSpan.FromMilliseconds(160)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
            }
        }
        element.IsVisibleChanged += (_, _) =>
        {
            if (element.IsVisible && SystemParameters.ClientAreaAnimation)
                element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        };
    }
}
