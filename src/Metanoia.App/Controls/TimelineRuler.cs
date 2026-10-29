using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace Metanoia.App.Controls;

/// <summary>Resolution-independent ruler; labels represent the actual decoded preview duration.</summary>
public sealed class TimelineRuler : FrameworkElement
{
    public static readonly DependencyProperty DurationProperty = DependencyProperty.Register(nameof(Duration), typeof(double), typeof(TimelineRuler), new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsRender));
    public double Duration { get => (double)GetValue(DurationProperty); set => SetValue(DurationProperty, value); }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(90, 164, 171, 179)), 1);
        var count = Math.Max(2, (int)(ActualWidth / 95));
        for (int i = 0; i <= count * 5; i++)
        {
            var x = i * ActualWidth / (count * 5);
            var major = i % 5 == 0;
            dc.DrawLine(pen, new Point(x, ActualHeight), new Point(x, ActualHeight - (major ? 9 : 4)));
            if (major && i < count * 5)
            {
                var seconds = Math.Max(0, Duration) * i / (count * 5);
                var label = Duration < 10 ? seconds.ToString("0.0", CultureInfo.InvariantCulture) + "s" : TimeSpan.FromSeconds(seconds).ToString(@"mm\:ss");
                dc.DrawText(new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Consolas"), 9, Brushes.LightSlateGray, VisualTreeHelper.GetDpi(this).PixelsPerDip), new Point(x + 3, 0));
            }
        }
    }
}
