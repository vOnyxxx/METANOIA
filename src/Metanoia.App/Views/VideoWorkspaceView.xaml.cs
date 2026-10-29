using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Metanoia.App.ViewModels;

namespace Metanoia.App.Views;

public partial class VideoWorkspaceView : UserControl
{
    private readonly DispatcherTimer _positionTimer;
    private bool _isSeeking;

    public VideoWorkspaceView()
    {
        InitializeComponent();
        _positionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _positionTimer.Tick += OnPositionTimerTick;
        DataContextChanged += OnDataContextChanged;
        IsVisibleChanged += (_, _) => { if (!IsVisible) { VideoMonitor.Pause(); _positionTimer.Stop(); } };
        Unloaded += (_, _) => _positionTimer.Stop();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is INotifyPropertyChanged oldValue)
        {
            oldValue.PropertyChanged -= OnViewModelPropertyChanged;
        }
        if (e.NewValue is INotifyPropertyChanged newValue)
        {
            newValue.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.VideoPreviewSource) || sender is not MainViewModel viewModel)
        {
            return;
        }

        Dispatcher.Invoke(() =>
        {
            VideoMonitor.Stop();
            VideoMonitor.Source = viewModel.VideoPreviewSource;
            if (viewModel.VideoPreviewSource is not null)
            {
                VideoMonitor.Play();
            }
        });
    }

    private void OnPlayClick(object sender, RoutedEventArgs e)
    {
        VideoMonitor.Play();
        _positionTimer.Start();
    }

    private void OnPauseClick(object sender, RoutedEventArgs e)
    {
        VideoMonitor.Pause();
        _positionTimer.Stop();
    }

    private void OnStopClick(object sender, RoutedEventArgs e)
    {
        VideoMonitor.Stop();
        _positionTimer.Stop();
        TimelineSlider.Value = 0;
    }

    private void OnMediaOpened(object sender, RoutedEventArgs e)
    {
        if (VideoMonitor.NaturalDuration.HasTimeSpan)
        {
            TimelineSlider.Maximum = VideoMonitor.NaturalDuration.TimeSpan.TotalSeconds;
        }
        VideoMonitor.Pause();
        UpdateTimeDisplay();
    }

    private void OnMediaEnded(object sender, RoutedEventArgs e)
    {
        _positionTimer.Stop();
        VideoMonitor.Position = TimeSpan.Zero;
        TimelineSlider.Value = 0;
        UpdateTimeDisplay();
    }

    private void OnPositionTimerTick(object? sender, EventArgs e)
    {
        if (!_isSeeking)
        {
            TimelineSlider.Value = VideoMonitor.Position.TotalSeconds;
        }
        UpdateTimeDisplay();
    }

    private void OnTimelineMouseDown(object sender, MouseButtonEventArgs e) => _isSeeking = true;

    private void OnTimelineMouseUp(object sender, MouseButtonEventArgs e)
    {
        SeekToTimelineValue();
        _isSeeking = false;
        UpdateTimeDisplay();
    }

    private void OnTimelineKeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Left or Key.Right or Key.Home or Key.End or Key.PageUp or Key.PageDown)
        {
            SeekToTimelineValue();
            UpdateTimeDisplay();
        }
    }

    private void OnTimelineValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isSeeking)
        {
            TimeDisplay.Text = $"{FormatTime(e.NewValue)} / {FormatTime(TimelineSlider.Maximum)}";
        }
    }

    private void UpdateTimeDisplay() =>
        TimeDisplay.Text = $"{FormatTime(VideoMonitor.Position.TotalSeconds)} / {FormatTime(TimelineSlider.Maximum)}";

    private void SeekToTimelineValue()
    {
        if (VideoMonitor.Source is not null)
        {
            VideoMonitor.Position = TimeSpan.FromSeconds(TimelineSlider.Value);
        }
    }

    private void OnMediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.StatusText = "No se pudo reproducir el proxy: " + e.ErrorException.Message;
    }
    private void OnMarkIn(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.VideoTrimStartSeconds += TimelineSlider.Value;
    }
    private void OnMarkOut(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.VideoTrimDurationSeconds = Math.Max(.05, TimelineSlider.Value);
    }

    private static string FormatTime(double seconds) =>
        TimeSpan.FromSeconds(Math.Max(0, seconds)).ToString(@"mm\:ss");
}
