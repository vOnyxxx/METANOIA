using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Metanoia.App.ViewModels;

public partial class MainViewModel
{
    public bool AutomaticParameterPreviews { get; set; } = true;
    [ObservableProperty] private bool _isExporting;
    public IReadOnlyList<string> ImageFilters { get; } = ["Original", "Monocromo", "Sepia", "Negativo", "Enfoque", "Suave"];
    public IReadOnlyList<string> AudioVisualizations { get; } = ["Forma de onda", "Onda por canal", "Espectrograma", "Espectro de frecuencias"];
    [ObservableProperty] private string _imageTool = "Mover";
    [ObservableProperty] private bool _showImageOriginal;
    [ObservableProperty] private string _selectedImageFilter = "Original";
    [ObservableProperty] private double _imageTemperature;
    [ObservableProperty] private int _imageSourceWidth = 1;
    [ObservableProperty] private int _imageSourceHeight = 1;
    [ObservableProperty] private string _selectedAudioVisualization = "Forma de onda";
    [ObservableProperty] private double _audioLowGain;
    [ObservableProperty] private double _audioMidGain;
    [ObservableProperty] private double _audioHighGain;
    [ObservableProperty] private double _audioGain;
    private CancellationTokenSource? _imageRefresh;
    private CancellationTokenSource? _audioRefresh;
    private CancellationTokenSource? _videoRefresh;
    public BitmapImage? DisplayImage => ShowImageOriginal ? ImageBeforePreview : ImageAfterPreview ?? ImageBeforePreview;
    public BitmapImage? AudioDisplayImage => SelectedAudioVisualization.StartsWith("Espectro", StringComparison.Ordinal) ? AudioSpectrogramImage : AudioWaveformImage;
    public string CropSummary => ImageCropEnabled ? $"{ImageCropWidth} × {ImageCropHeight} px · ({ImageCropX}, {ImageCropY})" : "Sin recorte";

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is nameof(ImageBeforePreview) or nameof(ImageAfterPreview) or nameof(ShowImageOriginal)) OnPropertyChanged(nameof(DisplayImage));
        if (e.PropertyName is nameof(AudioWaveformImage) or nameof(AudioSpectrogramImage) or nameof(SelectedAudioVisualization)) OnPropertyChanged(nameof(AudioDisplayImage));
        if (e.PropertyName is nameof(ImageCropX) or nameof(ImageCropY) or nameof(ImageCropWidth) or nameof(ImageCropHeight) or nameof(ImageCropEnabled)) OnPropertyChanged(nameof(CropSummary));
    }
    partial void OnImageTemperatureChanged(double value) { RefreshImageCommandPreview(); ScheduleImagePreview(); }
    partial void OnSelectedImageFilterChanged(string value) { RefreshImageCommandPreview(); ScheduleImagePreview(); }
    partial void OnSelectedAudioVisualizationChanged(string value) { ScheduleAudioPreview(); }
    partial void OnAudioLowGainChanged(double value) => EqualizerChanged();
    partial void OnAudioMidGainChanged(double value) => EqualizerChanged();
    partial void OnAudioHighGainChanged(double value) => EqualizerChanged();
    partial void OnAudioGainChanged(double value) => EqualizerChanged();
    private void EqualizerChanged() { RefreshAudioCommandPreview(); ScheduleAudioPreview(); }
    [RelayCommand] private void ResetEqualizer() { AudioLowGain = AudioMidGain = AudioHighGain = AudioGain = 0; }
    public void ApplyImageCrop(int x, int y, int width, int height)
    {
        ImageCropX = x; ImageCropY = y; ImageCropWidth = width; ImageCropHeight = height;
        ImageCropEnabled = true; ShowImageOriginal = false; RefreshImageCommandPreview(); ScheduleImagePreview();
    }
    public void ResetImageAdjustments()
    {
        ImageCropEnabled = false; ImageTargetWidth = 0; ImageTemperature = ImageBrightness = 0;
        ImageContrast = ImageSaturation = 1; SelectedImageFilter = "Original"; ShowImageOriginal = false; ScheduleImagePreview();
    }
    private void ScheduleImagePreview() => SchedulePreview(ref _imageRefresh, () => IsImageLoaded, PreviewImageAsync);
    private void ScheduleAudioPreview() => SchedulePreview(ref _audioRefresh, () => IsAudioLoaded, GenerateAudioPreviewAsync);
    private void ScheduleVideoPreview() => SchedulePreview(ref _videoRefresh, () => HasMedia, GenerateVideoProxyAsync);
    /// <summary>Coalesces slider edits and serializes previews with export/import operations.</summary>
    private void SchedulePreview(ref CancellationTokenSource? pending, Func<bool> canPreview, Func<Task> action)
    {
        if (!AutomaticParameterPreviews) return;
        pending?.Cancel(); pending?.Dispose(); var source = new CancellationTokenSource(); pending = source;
        var path = SelectedFilePath;
        _ = RefreshAsync(source.Token);
        async Task RefreshAsync(CancellationToken token)
        {
            try
            {
                await Task.Delay(650, token);
                while (IsBusy) await Task.Delay(120, token);
                token.ThrowIfCancellationRequested();
                if (SelectedFilePath == path && canPreview() && File.Exists(path)) await action();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { StatusText = "Vista previa: " + ex.Message; }
        }
    }
}
