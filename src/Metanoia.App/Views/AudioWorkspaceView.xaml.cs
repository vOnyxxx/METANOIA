using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Metanoia.App.ViewModels;

namespace Metanoia.App.Views;

public partial class AudioWorkspaceView : UserControl
{
    private readonly DispatcherTimer _positionTimer;
    private bool _isSeeking;

    public AudioWorkspaceView()
    {
        InitializeComponent();
        _positionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _positionTimer.Tick += OnPositionTimerTick;
        DataContextChanged += OnDataContextChanged;
        IsVisibleChanged += (_, _) => { if (!IsVisible) { AudioMonitor.Pause(); _positionTimer.Stop(); } };
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
        if (e.PropertyName != nameof(MainViewModel.AudioPreviewSource) || sender is not MainViewModel viewModel)
        {
            return;
        }
        Dispatcher.Invoke(() =>
        {
            AudioMonitor.Stop();
            AudioMonitor.Source = viewModel.AudioPreviewSource;
            if (viewModel.AudioPreviewSource is not null)
            {
                AudioMonitor.Play();
            }
        });
    }

    private void OnPlayClick(object sender, RoutedEventArgs e)
    {
        AudioMonitor.Play();
        _positionTimer.Start();
    }

    private void OnPauseClick(object sender, RoutedEventArgs e)
    {
        AudioMonitor.Pause();
        _positionTimer.Stop();
    }

    private void OnStopClick(object sender, RoutedEventArgs e)
    {
        AudioMonitor.Stop();
        _positionTimer.Stop();
        AudioTimelineSlider.Value = 0;
    }

    private void OnMediaOpened(object sender, RoutedEventArgs e)
    {
        if (AudioMonitor.NaturalDuration.HasTimeSpan)
        {
            AudioTimelineSlider.Maximum = AudioMonitor.NaturalDuration.TimeSpan.TotalSeconds;
        }
        AudioMonitor.Pause();
        UpdateTimeDisplay();
    }

    private void OnMediaEnded(object sender, RoutedEventArgs e)
    {
        _positionTimer.Stop();
        AudioMonitor.Position = TimeSpan.Zero;
        AudioTimelineSlider.Value = 0;
        UpdateTimeDisplay();
    }

    private void OnPositionTimerTick(object? sender, EventArgs e)
    {
        if (!_isSeeking)
        {
            AudioTimelineSlider.Value = AudioMonitor.Position.TotalSeconds;
        }
        AudioPlayhead.Margin = new Thickness(12 + Math.Max(0, AudioVisualArea.ActualWidth - 24) * AudioTimelineSlider.Value / Math.Max(1, AudioTimelineSlider.Maximum), 12, 0, 12);
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
            AudioTimeDisplay.Text = $"{FormatTime(e.NewValue)} / {FormatTime(AudioTimelineSlider.Maximum)}";
        }
    }

    private void UpdateTimeDisplay() =>
        AudioTimeDisplay.Text = $"{FormatTime(AudioMonitor.Position.TotalSeconds)} / {FormatTime(AudioTimelineSlider.Maximum)}";

    private void SeekToTimelineValue()
    {
        if (AudioMonitor.Source is not null)
        {
            AudioMonitor.Position = TimeSpan.FromSeconds(AudioTimelineSlider.Value);
        }
    }

    private void OnMediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.StatusText = "No se pudo reproducir el proxy: " + e.ErrorException.Message;
    }

    private static string FormatTime(double seconds) =>
        TimeSpan.FromSeconds(Math.Max(0, seconds)).ToString(@"mm\:ss");
}

