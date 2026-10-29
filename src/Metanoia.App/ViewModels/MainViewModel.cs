using System.Globalization;
using System.IO;
using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Metanoia.App.Services;
using Metanoia.Core.Contracts;
using Metanoia.Core.Models;

namespace Metanoia.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly IMediaProbeService? _probeService;
    private readonly IFFmpegCommandBuilder? _commandBuilder;
    private readonly IFFmpegRunner? _runner;
    private readonly IProcessingQueue _queue;
    private readonly IOutputPathService _outputPaths;
    private readonly IFileDialogService _fileDialogs;
    private readonly IClipboardService _clipboard;
    private readonly ISystemLauncher _launcher;
    private readonly IAdvancedPresetService _presetService;
    private MediaProbeResult? _probeResult;
    private CancellationTokenSource? _processingCancellation;
    private string? _videoPreviewPath;

    public MainViewModel(
        ISettingsService settings,
        IMediaProbeService? probeService,
        IFFmpegCommandBuilder? commandBuilder,
        IFFmpegRunner? runner,
        IProcessingQueue queue,
        IOutputPathService outputPaths,
        IFileDialogService fileDialogs,
        IClipboardService clipboard,
        ISystemLauncher launcher,
        IAdvancedPresetService presetService)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _probeService = probeService;
        _commandBuilder = commandBuilder;
        _runner = runner;
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        _outputPaths = outputPaths ?? throw new ArgumentNullException(nameof(outputPaths));
        _fileDialogs = fileDialogs ?? throw new ArgumentNullException(nameof(fileDialogs));
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
        _presetService = presetService ?? throw new ArgumentNullException(nameof(presetService));

        ToolStatusText = _settings.AreToolsAvailable
            ? "FFmpeg + FFprobe disponibles"
            : "FFmpeg/FFprobe no encontrados · revisa PATH";
        StatusText = _settings.AreToolsAvailable
            ? "Selecciona o arrastra un archivo multimedia para comenzar."
            : "Instala FFmpeg y agrega sus binarios al PATH antes de procesar.";

        _queue.Changed += OnQueueChanged;
        RefreshQueueItems();
        InitializeAdvancedState();
    }

    public IReadOnlyList<string> Presets { get; } =
        ["ultrafast", "veryfast", "fast", "medium", "slow", "veryslow"];

    public IReadOnlyList<string> Resolutions { get; } = ["Original", "720p", "1080p"];

    public IReadOnlyList<string> VideoOperations { get; } = ["Convertir a MP4", "Extraer audio MP3"];

    public IReadOnlyList<string> ImageFormats { get; } = ["PNG", "JPG", "WebP"];

    public bool AreToolsAvailable => _settings.AreToolsAvailable;

    public ObservableCollection<QueueItemViewModel> QueueItems { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentSectionTitle))]
    [NotifyPropertyChangedFor(nameof(CurrentSectionDescription))]
    [NotifyPropertyChangedFor(nameof(UpcomingTools))]
    private AppSection _currentSection = AppSection.Home;

    public string CurrentSectionTitle => CurrentSection switch
    {
        AppSection.Image => "Espacio de imagen",
        AppSection.Video => "Espacio de video",
        AppSection.Audio => "Espacio de audio",
        AppSection.Advanced => "Avanzado y robustez",
        _ => "Inicio"
    };

    public string CurrentSectionDescription => CurrentSection switch
    {
        AppSection.Image => "Conversión, escala proporcional, recorte y ajustes básicos con vista Antes/Después.",
        AppSection.Video => "Monitor proxy, timeline compacta, recorte temporal y exportación MP4 o MP3.",
        AppSection.Audio => "Waveform, espectrograma, recorte, normalización LUFS y exportación WAV, MP3 o AAC.",
        AppSection.Advanced => "Argumentos FFmpeg validados, presets locales, historial persistente y diagnóstico.",
        _ => "Selecciona un medio para comenzar."
    };

    public string UpcomingTools => CurrentSection switch
    {
        AppSection.Image => "RECORTAR  ·  REDIMENSIONAR  ·  BRILLO  ·  CONTRASTE  ·  SATURACIÓN  ·  EXPORTAR",
        AppSection.Audio => "CORTAR  ·  NORMALIZAR  ·  VOLUMEN  ·  FUNDIDOS  ·  CANALES  ·  SAMPLE RATE",
        AppSection.Advanced => "VALIDAR  ·  PRESETS  ·  HISTORIAL  ·  LOGS  ·  RECUPERACIÓN",
        _ => string.Empty
    };

    [ObservableProperty]
    private string _toolStatusText = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ProcessCommand))]
    [NotifyCanExecuteChangedFor(nameof(GenerateVideoProxyCommand))]
    private bool _hasMedia;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ProcessCommand))]
    [NotifyCanExecuteChangedFor(nameof(ProcessImageCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviewImageCommand))]
    [NotifyCanExecuteChangedFor(nameof(GenerateVideoProxyCommand))]
    [NotifyCanExecuteChangedFor(nameof(ProcessAudioCommand))]
    [NotifyCanExecuteChangedFor(nameof(GenerateAudioPreviewCommand))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private bool _isProcessing;

    [ObservableProperty]
    private string _selectedFileName = "Ningún archivo seleccionado";

    [ObservableProperty]
    private string _selectedFilePath = string.Empty;

    [ObservableProperty]
    private string _mediaSummary = "FFprobe mostrará aquí el formato, duración y tamaño.";

    [ObservableProperty]
    private string _technicalSummary = "Códecs · resolución · FPS · audio";

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ProcessCommand))]
    private string _outputPath = string.Empty;

    [ObservableProperty]
    private int _crf = 23;

    [ObservableProperty]
    private string _selectedPreset = "medium";

    [ObservableProperty]
    private string _selectedResolution = "Original";

    [ObservableProperty]
    private string _selectedVideoOperation = "Convertir a MP4";

    [ObservableProperty]
    private double _videoTrimStartSeconds;

    [ObservableProperty]
    private double _videoTrimDurationSeconds;

    [ObservableProperty]
    private double _videoDurationSeconds = 1;

    [ObservableProperty]
    private Uri? _videoPreviewSource;

    [ObservableProperty]
    private bool _hasVideoPreview;

    [ObservableProperty]
    private string _videoPreviewStatus = "Arrastra un video para comenzar.";

    [ObservableProperty]
    private string _commandDisplay = "El comando aparecerá después de analizar un video.";

    [ObservableProperty]
    private double _progressPercent;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenOutputFolderCommand))]
    private bool _resultAvailable;

    [ObservableProperty]
    private string _resultMessage = "Sin resultados todavía.";

    [ObservableProperty]
    private string _queueSummary = "Cola local · 0 trabajos";

    [ObservableProperty]
    private bool _hasQueueItems;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ProcessImageCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviewImageCommand))]
    private bool _isImageLoaded;

    [ObservableProperty]
    private BitmapImage? _imageBeforePreview;

    [ObservableProperty]
    private BitmapImage? _imageAfterPreview;

    [ObservableProperty]
    private bool _hasImageAfterPreview;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ProcessImageCommand))]
    private string _imageOutputPath = string.Empty;

    [ObservableProperty]
    private string _selectedImageFormat = "PNG";

    [ObservableProperty]
    private int _imageTargetWidth;

    [ObservableProperty]
    private bool _imageCropEnabled;

    [ObservableProperty]
    private int _imageCropX;

    [ObservableProperty]
    private int _imageCropY;

    [ObservableProperty]
    private int _imageCropWidth = 640;

    [ObservableProperty]
    private int _imageCropHeight = 480;

    [ObservableProperty]
    private double _imageBrightness;

    [ObservableProperty]
    private double _imageContrast = 1;

    [ObservableProperty]
    private double _imageSaturation = 1;

    [ObservableProperty]
    private int _imageQuality = 90;

    [ObservableProperty]
    private string _imageCommandDisplay = "Importa una imagen para generar el comando FFmpeg.";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenImageOutputFolderCommand))]
    private bool _imageResultAvailable;

    [ObservableProperty]
    private string _imageResultMessage = "Genera una vista previa o exporta para comparar el resultado.";

    [RelayCommand]
    private void Navigate(AppSection section) => CurrentSection = section;

    [RelayCommand]
    private async Task SelectFileAsync()
    {
        var selectedPath = _fileDialogs.SelectInputMedia();
        if (selectedPath is not null)
        {
            await LoadFileAsync(selectedPath);
        }
    }

    public async Task LoadFileAsync(string filePath)
    {
        if (IsBusy || IsProcessing)
        {
            StatusText = "Cancela el proceso actual antes de abrir otro archivo.";
            return;
        }

        if (!_settings.AreToolsAvailable || _probeService is null)
        {
            StatusText = "FFmpeg y FFprobe no están disponibles en PATH.";
            return;
        }

        _imageRefresh?.Cancel(); _audioRefresh?.Cancel(); _videoRefresh?.Cancel();
        ActiveImageFilter = ActiveVideoFilter = ActiveAudioFilter = "";
        ClearVideoPreview();
        ClearAudioPreview();
        try
        {
            IsBusy = true;
            HasMedia = false;
            ResultAvailable = false;
            ProgressPercent = 0;
            StatusText = "Analizando con FFprobe…";

            _probeResult = await _probeService.ProbeAsync(filePath);
            SelectedFilePath = _probeResult.File.FullPath;
            SelectedFileName = _probeResult.File.Name;
            UpdateProbeSummary(_probeResult);

            HasMedia = _probeResult.MediaType == MediaType.Video;
            IsImageLoaded = _probeResult.MediaType == MediaType.Image;
            IsAudioLoaded = _probeResult.MediaType == MediaType.Audio;
            if (HasMedia)
            {
                VideoDurationSeconds = Math.Max(1, _probeResult.Duration?.TotalSeconds ?? 1);
                VideoTrimStartSeconds = 0;
                VideoTrimDurationSeconds = 0;
                VideoPreviewStatus = "Preparando monitor…";
                OutputPath = _outputPaths.SuggestVideoOutputPath(_probeResult.File.FullPath, GetVideoOutputMode());
            }
            else if (IsImageLoaded)
            {
                ImageOutputPath = _outputPaths.SuggestImageOutputPath(_probeResult.File.FullPath, GetImageFormat());
                ImageCropEnabled = false;
                ImageBrightness = 0; ImageContrast = 1; ImageSaturation = 1; ImageTemperature = 0;
                SelectedImageFilter = "Original";
                ImageSourceWidth = _probeResult.PrimaryVideoStream?.Width ?? 1;
                ImageSourceHeight = _probeResult.PrimaryVideoStream?.Height ?? 1;
                ImageBeforePreview = TryLoadBitmap(_probeResult.File.FullPath);
                ImageAfterPreview = null;
                HasImageAfterPreview = false;
            }
            else if (IsAudioLoaded)
            {
                AudioDurationSeconds = Math.Max(1, _probeResult.Duration?.TotalSeconds ?? 1);
                AudioTrimStartSeconds = 0;
                AudioTrimDurationSeconds = 0;
                AudioOutputPath = _outputPaths.SuggestAudioOutputPath(_probeResult.File.FullPath, GetAudioFormat());
                AudioPreviewStatus = "Audio analizado · genera waveform, espectrograma y proxy reproducible.";
                AudioResultMessage = "Configura la exportación y revisa el comando FFmpeg.";
            }
            CurrentSection = _probeResult.MediaType switch
            {
                MediaType.Image => AppSection.Image,
                MediaType.Audio => AppSection.Audio,
                _ => AppSection.Video
            };
            StatusText = _probeResult.MediaType switch
            {
                MediaType.Video => "Video cargado",
                MediaType.Image => "Imagen cargada",
                MediaType.Audio => "Audio cargado",
                _ => "Archivo analizado, pero su tipo no es compatible todavía."
            };
            RefreshCommandPreview();
            RefreshImageCommandPreview();
            RefreshAudioCommandPreview();
        }
        catch (Exception exception)
        {
            _probeResult = null;
            HasMedia = false;
            IsImageLoaded = false;
            IsAudioLoaded = false;
            SelectedFileName = "No se pudo analizar el archivo";
            MediaSummary = exception.Message;
            TechnicalSummary = "Revisa que el archivo sea legible por FFprobe.";
            CommandDisplay = "Comando no disponible.";
            StatusText = "Error durante el análisis.";
        }
        finally
        {
            IsBusy = false;
        }
        if (_probeResult is not null)
        {

            if (HasMedia) await GenerateVideoProxyAsync();
            else if (IsAudioLoaded) await GenerateAudioPreviewAsync();
            else if (IsImageLoaded)
            {
                await PreviewImageAsync();
                if (ImageBeforePreview is null && ImageAfterPreview is not null) ImageBeforePreview = ImageAfterPreview;
            }
            _ = EnsureCapabilitiesAsync();
            StatusText = "Listo";
        }
    }

    [RelayCommand]
    private void SelectOutput()
    {
        if (string.IsNullOrWhiteSpace(SelectedFilePath))
        {
            StatusText = "Primero selecciona un video.";
            return;
        }

        var suggested = string.IsNullOrWhiteSpace(OutputPath)
            ? _outputPaths.SuggestVideoOutputPath(SelectedFilePath, GetVideoOutputMode())
            : OutputPath;
        var selected = _fileDialogs.SelectOutputVideo(suggested, GetVideoOutputMode());
        if (selected is not null)
        {
            OutputPath = selected;
        }
    }

    [RelayCommand]
    private void SelectImageOutput()
    {
        if (!IsImageLoaded)
        {
            StatusText = "Primero selecciona una imagen.";
            return;
        }

        var suggested = string.IsNullOrWhiteSpace(ImageOutputPath)
            ? _outputPaths.SuggestImageOutputPath(SelectedFilePath, GetImageFormat())
            : ImageOutputPath;
        var selected = _fileDialogs.SelectOutputImage(suggested, SelectedImageFormat);
        if (selected is not null)
        {
            ImageOutputPath = selected;
        }
    }

    [RelayCommand(CanExecute = nameof(CanProcess))]
    private async Task ProcessAsync()
    {
        if (_probeResult is null || _commandBuilder is null || _runner is null)
        {
            StatusText = "El entorno de procesamiento no está disponible.";
            return;
        }

        ProcessingJob? job = null;
        try
        {
            _outputPaths.ValidateVideoOutputPath(SelectedFilePath, OutputPath, GetVideoOutputMode());
            var command = ApplyAdvancedArguments(_commandBuilder.BuildVideoConversion(_probeResult, CreateOptions()));
            CommandDisplay = command.ToDisplayString();

            IsExporting = true;
            job = new ProcessingJob(SelectedFilePath, OutputPath);
            _queue.Enqueue(job);
            job.UpdateStatus(JobStatus.Processing);
            _queue.Update(job);

            _processingCancellation = new CancellationTokenSource();
            IsBusy = true;
            IsProcessing = true;
            ResultAvailable = false;
            ResultMessage = "Procesamiento en curso…";
            ProgressPercent = 0;
            StatusText = "FFmpeg está procesando el video sin bloquear la interfaz.";

            var progress = new Progress<JobProgress>(value =>
            {
                job.UpdateProgress(value);
                ProgressPercent = value.Percent ?? ProgressPercent;
                StatusText = value.Percent is null
                    ? $"Procesando · {value.ProcessedDuration:hh\\:mm\\:ss}"
                    : $"Procesando · {value.Percent.Value:0.0}% · {value.Speed ?? "—"}";
            });

            var expectedDuration = GetExpectedVideoDuration();
            var result = await _runner.RunAsync(
                command,
                expectedDuration,
                progress,
                _processingCancellation.Token);

            if (result.WasCancelled)
            {
                job.UpdateStatus(JobStatus.Cancelled);
                _queue.Update(job);
                DeleteIncompleteOutput(OutputPath);
                StatusText = "Proceso cancelado de forma segura.";
                ResultMessage = "Cancelado · no se conservó una salida incompleta.";
                return;
            }

            if (!result.Succeeded || !File.Exists(OutputPath) || new FileInfo(OutputPath).Length == 0)
            {
                var diagnostic = Truncate(result.StandardError.Trim(), 700);
                job.UpdateStatus(JobStatus.Error, diagnostic);
                _queue.Update(job);
                StatusText = "FFmpeg terminó con error.";
                ResultMessage = string.IsNullOrWhiteSpace(diagnostic)
                    ? "No se produjo un archivo de salida válido."
                    : diagnostic;
                return;
            }

            job.UpdateStatus(JobStatus.Completed);
            _queue.Update(job);
            ProgressPercent = 100;
            ResultAvailable = true;
            StatusText = "Conversión completada y archivo verificado.";
            ResultMessage = $"Salida: {OutputPath}";
        }
        catch (Exception exception)
        {
            job?.UpdateStatus(JobStatus.Error, exception.Message);
            if (job is not null)
            {
                _queue.Update(job);
            }
            StatusText = "No fue posible completar la conversión.";
            ResultMessage = exception.Message;
        }
        finally
        {
            IsExporting = false;
            IsProcessing = false;
            IsBusy = false;
            _processingCancellation?.Dispose();
            _processingCancellation = null;
        }
    }

    [RelayCommand(CanExecute = nameof(CanPreviewImage))]
    private async Task PreviewImageAsync()
    {
        var previewDirectory = Path.Combine(Path.GetTempPath(), "METANOIA", "previews");
        Directory.CreateDirectory(previewDirectory);
        var previewPath = Path.Combine(previewDirectory, "image-preview.png");
        await RunImageAsync(previewPath, ImageOutputFormat.Png, false);
    }

    private bool CanPreviewImage() => AreToolsAvailable && IsImageLoaded && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanProcessImage))]
    private async Task ProcessImageAsync() => await RunImageAsync(ImageOutputPath, GetImageFormat(), true);

    private bool CanProcessImage() =>
        AreToolsAvailable && IsImageLoaded && !IsBusy && !string.IsNullOrWhiteSpace(ImageOutputPath);

    private async Task RunImageAsync(string outputPath, ImageOutputFormat format, bool persistInQueue)
    {
        if (_probeResult is null || _commandBuilder is null || _runner is null)
        {
            StatusText = "El entorno de procesamiento de imagen no está disponible.";
            return;
        }

        ProcessingJob? job = null;
        try
        {
            _outputPaths.ValidateImageOutputPath(SelectedFilePath, outputPath, format);
            var command = _commandBuilder.BuildImageConversion(_probeResult, CreateImageOptions(outputPath, format));
            if (persistInQueue)
            {
                command = ApplyAdvancedArguments(command);
            }
            IsExporting = persistInQueue;
            if (persistInQueue)
            {
                ImageCommandDisplay = command.ToDisplayString();
                job = new ProcessingJob(SelectedFilePath, outputPath);
                _queue.Enqueue(job);
                job.UpdateStatus(JobStatus.Processing);
                _queue.Update(job);
            }

            _processingCancellation = new CancellationTokenSource();
            IsBusy = true;
            IsProcessing = true;
            ImageResultAvailable = false;
            ImageResultMessage = persistInQueue ? "Exportando imagen…" : "Generando proxy de vista previa…";
            StatusText = ImageResultMessage;
            ProgressPercent = 0;

            var progress = new Progress<JobProgress>(value =>
            {
                job?.UpdateProgress(value);
                ProgressPercent = value.Percent ?? ProgressPercent;
            });
            var result = await _runner.RunAsync(command, null, progress, _processingCancellation.Token);
            if (result.WasCancelled)
            {
                job?.UpdateStatus(JobStatus.Cancelled);
                if (job is not null) _queue.Update(job);
                DeleteIncompleteOutput(outputPath);
                StatusText = "Procesamiento de imagen cancelado.";
                ImageResultMessage = "Cancelado sin conservar una salida incompleta.";
                return;
            }

            if (!result.Succeeded || !File.Exists(outputPath) || new FileInfo(outputPath).Length == 0)
            {
                var diagnostic = Truncate(result.StandardError.Trim(), 700);
                job?.UpdateStatus(JobStatus.Error, diagnostic);
                if (job is not null) _queue.Update(job);
                StatusText = "FFmpeg no pudo generar la imagen.";
                ImageResultMessage = diagnostic;
                return;
            }

            job?.UpdateStatus(JobStatus.Completed);
            if (job is not null) _queue.Update(job);
            ImageAfterPreview = TryLoadBitmap(outputPath) ?? ImageAfterPreview;
            HasImageAfterPreview = ImageAfterPreview is not null;
            ImageResultAvailable = persistInQueue;
            ProgressPercent = 100;
            StatusText = persistInQueue ? "Imagen exportada y verificada." : "Vista previa actualizada.";
            ImageResultMessage = persistInQueue ? $"Salida: {outputPath}" : "Proxy generado sin modificar el original.";
        }
        catch (Exception exception)
        {
            job?.UpdateStatus(JobStatus.Error, exception.Message);
            if (job is not null) _queue.Update(job);
            StatusText = "No fue posible procesar la imagen.";
            ImageResultMessage = exception.Message;
        }
        finally
        {
            IsExporting = false;
            IsProcessing = false;
            IsBusy = false;
            _processingCancellation?.Dispose();
            _processingCancellation = null;
        }
    }

    [RelayCommand]
    private void CopyImageCommand()
    {
        if (!string.IsNullOrWhiteSpace(ImageCommandDisplay) && IsImageLoaded)
        {
            _clipboard.SetText(ImageCommandDisplay);
            StatusText = "Comando de imagen copiado.";
        }
    }

    [RelayCommand(CanExecute = nameof(CanOpenImageOutputFolder))]
    private void OpenImageOutputFolder() => _launcher.OpenContainingFolder(ImageOutputPath);

    private bool CanOpenImageOutputFolder() => ImageResultAvailable && File.Exists(ImageOutputPath);

    private bool CanProcess() =>
        AreToolsAvailable && HasMedia && !IsBusy && !string.IsNullOrWhiteSpace(OutputPath);

    [RelayCommand(CanExecute = nameof(CanGenerateVideoProxy))]
    private async Task GenerateVideoProxyAsync()
    {
        if (_probeResult is null || _commandBuilder is null || _runner is null)
        {
            VideoPreviewStatus = "El entorno de proxy no está disponible.";
            return;
        }

        var previewDirectory = Path.Combine(Path.GetTempPath(), "METANOIA", "previews");
        Directory.CreateDirectory(previewDirectory);
        var previewPath = Path.Combine(previewDirectory, $"video-proxy-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.mp4");
        var trimStartSnapshot = VideoTrimStartSeconds;
        var trimDurationSnapshot = VideoTrimDurationSeconds;
        try
        {
            var expectedDuration = GetExpectedVideoDuration(
                    maximumSeconds: null,
                    trimStartSeconds: trimStartSnapshot,
                    trimDurationSeconds: trimDurationSnapshot)
                ?? TimeSpan.FromSeconds(Math.Min(60, VideoDurationSeconds));
            if (expectedDuration <= TimeSpan.Zero)
            {
                throw new InvalidOperationException("El recorte no contiene un intervalo reproducible.");
            }

            _processingCancellation = new CancellationTokenSource();
            IsBusy = true;
            IsProcessing = true;
            ProgressPercent = 0;
            VideoPreviewStatus = "Generando proxy 720p para el monitor…";
            var command = _commandBuilder.BuildVideoConversion(_probeResult, new VideoConversionOptions
            {
                InputPath = SelectedFilePath,
                OutputPath = previewPath,
                OutputMode = VideoOutputMode.Mp4,
                Resolution = VideoResolutionMode.Hd720p,
                ExtraFilter = ActiveVideoFilter,
                Crf = 30,
                Preset = "ultrafast",
                TrimStartSeconds = trimStartSnapshot,
                TrimDurationSeconds = expectedDuration.TotalSeconds
            });
            var progress = new Progress<JobProgress>(value =>
            {
                ProgressPercent = value.Percent ?? ProgressPercent;
                VideoPreviewStatus = value.Percent is null
                    ? $"Generando proxy · {value.ProcessedDuration:mm\\:ss}"
                    : $"Generando proxy · {value.Percent.Value:0.0}% · {value.Speed ?? "—"}";
            });
            var result = await _runner.RunAsync(
                command,
                expectedDuration,
                progress,
                _processingCancellation.Token);
            if (result.WasCancelled)
            {
                DeleteIncompleteOutput(previewPath);
                VideoPreviewStatus = "Generación de proxy cancelada sin conservar una salida incompleta.";
                return;
            }

            if (!result.Succeeded || !File.Exists(previewPath) || new FileInfo(previewPath).Length == 0)
            {
                DeleteIncompleteOutput(previewPath);
                VideoPreviewStatus = "No se pudo generar el proxy · " + Truncate(result.StandardError.Trim(), 300);
                return;
            }

            if (Math.Abs(VideoTrimStartSeconds - trimStartSnapshot) > 0.0001 ||
                Math.Abs(VideoTrimDurationSeconds - trimDurationSnapshot) > 0.0001)
            {
                DeleteIncompleteOutput(previewPath);
                VideoPreviewStatus = "El recorte cambió durante el procesamiento · genera el proxy nuevamente.";
                return;
            }

            var previousPreviewPath = _videoPreviewPath;
            _videoPreviewPath = previewPath;
            VideoPreviewSource = new Uri(previewPath);
            HasVideoPreview = true;
            ProgressPercent = 100;
            VideoPreviewStatus = "Proxy listo · usa reproducción y timeline para inspeccionarlo.";
            TryDeletePreview(previousPreviewPath);
        }
        catch (Exception exception)
        {
            DeleteIncompleteOutput(previewPath);
            VideoPreviewStatus = exception.Message;
        }
        finally
        {
            IsExporting = false;
            IsProcessing = false;
            IsBusy = false;
            _processingCancellation?.Dispose();
            _processingCancellation = null;
        }
    }

    private bool CanGenerateVideoProxy() => AreToolsAvailable && HasMedia && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        StatusText = "Solicitando cancelación…";
        _processingCancellation?.Cancel();
    }

    private bool CanCancel() => IsProcessing;

    [RelayCommand]
    private void CopyCommand()
    {
        if (string.IsNullOrWhiteSpace(CommandDisplay) || _probeResult is null)
        {
            return;
        }

        _clipboard.SetText(CommandDisplay);
        StatusText = "Comando representativo copiado.";
    }

    [RelayCommand(CanExecute = nameof(CanOpenOutputFolder))]
    private void OpenOutputFolder()
    {
        _launcher.OpenContainingFolder(OutputPath);
    }

    private bool CanOpenOutputFolder() => ResultAvailable && File.Exists(OutputPath);

    partial void OnOutputPathChanged(string value) => RefreshCommandPreview();

    partial void OnCrfChanged(int value) => RefreshCommandPreview();

    partial void OnSelectedPresetChanged(string value) => RefreshCommandPreview();

    partial void OnSelectedResolutionChanged(string value) => RefreshCommandPreview();

    partial void OnVideoTrimStartSecondsChanged(double value)
    {
        InvalidateVideoPreviewForTrimChange();
        RefreshCommandPreview();
    }

    partial void OnVideoTrimDurationSecondsChanged(double value)
    {
        InvalidateVideoPreviewForTrimChange();
        RefreshCommandPreview();
    }

    partial void OnSelectedVideoOperationChanged(string value)
    {
        if (HasMedia)
        {
            OutputPath = _outputPaths.SuggestVideoOutputPath(SelectedFilePath, GetVideoOutputMode());
        }
        RefreshCommandPreview();
    }

    partial void OnImageOutputPathChanged(string value) => RefreshImageCommandPreview();

    partial void OnImageTargetWidthChanged(int value) { RefreshImageCommandPreview(); ScheduleImagePreview(); }

    partial void OnImageCropEnabledChanged(bool value) { RefreshImageCommandPreview(); ScheduleImagePreview(); }

    partial void OnImageCropXChanged(int value) => RefreshImageCommandPreview();

    partial void OnImageCropYChanged(int value) => RefreshImageCommandPreview();

    partial void OnImageCropWidthChanged(int value) => RefreshImageCommandPreview();

    partial void OnImageCropHeightChanged(int value) => RefreshImageCommandPreview();

    partial void OnImageBrightnessChanged(double value) { RefreshImageCommandPreview(); ScheduleImagePreview(); }

    partial void OnImageContrastChanged(double value) { RefreshImageCommandPreview(); ScheduleImagePreview(); }

    partial void OnImageSaturationChanged(double value) { RefreshImageCommandPreview(); ScheduleImagePreview(); }

    partial void OnImageQualityChanged(int value) => RefreshImageCommandPreview();

    partial void OnSelectedImageFormatChanged(string value)
    {
        if (IsImageLoaded)
        {
            ImageOutputPath = _outputPaths.SuggestImageOutputPath(SelectedFilePath, GetImageFormat());
        }
        RefreshImageCommandPreview();
    }

    private void RefreshCommandPreview()
    {
        if (_probeResult is null || _commandBuilder is null || !HasMedia)
        {
            return;
        }

        try
        {
            _outputPaths.ValidateVideoOutputPath(SelectedFilePath, OutputPath, GetVideoOutputMode());
            CommandDisplay = ApplyAdvancedArguments(_commandBuilder.BuildVideoConversion(_probeResult, CreateOptions()))
                .ToDisplayString();
        }
        catch (Exception exception)
        {
            CommandDisplay = $"Configura una salida válida · {exception.Message}";
        }
    }

    private void RefreshImageCommandPreview()
    {
        if (_probeResult?.MediaType != MediaType.Image || _commandBuilder is null || !IsImageLoaded)
        {
            return;
        }

        try
        {
            var format = GetImageFormat();
            _outputPaths.ValidateImageOutputPath(SelectedFilePath, ImageOutputPath, format);
            ImageCommandDisplay = ApplyAdvancedArguments(_commandBuilder.BuildImageConversion(
                    _probeResult,
                    CreateImageOptions(ImageOutputPath, format)))
                .ToDisplayString();
        }
        catch (Exception exception)
        {
            ImageCommandDisplay = $"Configura valores válidos · {exception.Message}";
        }
    }

    private VideoConversionOptions CreateOptions() => new()
    {
        InputPath = SelectedFilePath,
        OutputPath = OutputPath,
        ExtraFilter = ActiveVideoFilter,
        Crf = Crf,
        Preset = SelectedPreset,
        OutputMode = GetVideoOutputMode(),
        TrimStartSeconds = VideoTrimStartSeconds,
        TrimDurationSeconds = VideoTrimDurationSeconds,
        Resolution = SelectedResolution switch
        {
            "1080p" => VideoResolutionMode.FullHd1080p,
            "720p" => VideoResolutionMode.Hd720p,
            _ => VideoResolutionMode.Original
        }
    };

    private VideoOutputMode GetVideoOutputMode() =>
        SelectedVideoOperation.Contains("audio", StringComparison.OrdinalIgnoreCase)
            ? VideoOutputMode.AudioMp3
            : VideoOutputMode.Mp4;

    private TimeSpan? GetExpectedVideoDuration(
        double? maximumSeconds = null,
        double? trimStartSeconds = null,
        double? trimDurationSeconds = null)
    {
        var trimStart = trimStartSeconds ?? VideoTrimStartSeconds;
        var trimDuration = trimDurationSeconds ?? VideoTrimDurationSeconds;
        if (_probeResult?.Duration is not { } fullDuration)
        {
            if (trimDuration <= 0)
            {
                return null;
            }

            var requestedSeconds = maximumSeconds is { } limit
                ? Math.Min(trimDuration, limit)
                : trimDuration;
            return TimeSpan.FromSeconds(requestedSeconds);
        }

        var remainingSeconds = Math.Max(0, fullDuration.TotalSeconds - trimStart);
        var effectiveSeconds = trimDuration > 0
            ? Math.Min(trimDuration, remainingSeconds)
            : remainingSeconds;
        if (maximumSeconds is { } maximum)
        {
            effectiveSeconds = Math.Min(effectiveSeconds, maximum);
        }

        return TimeSpan.FromSeconds(effectiveSeconds);
    }

    private void InvalidateVideoPreviewForTrimChange()
    {
        ScheduleVideoPreview();
        if (!HasVideoPreview && VideoPreviewSource is null)
        {
            return;
        }

        ClearVideoPreview();
        VideoPreviewStatus = "El recorte cambió · genera de nuevo el proxy para actualizar el monitor.";
    }

    private void ClearVideoPreview()
    {
        var previousPreviewPath = _videoPreviewPath;
        VideoPreviewSource = null;
        HasVideoPreview = false;
        _videoPreviewPath = null;
        TryDeletePreview(previousPreviewPath);
    }

    private static void TryDeletePreview(string? previewPath)
    {
        if (string.IsNullOrWhiteSpace(previewPath))
        {
            return;
        }

        try
        {
            if (File.Exists(previewPath))
            {
                File.Delete(previewPath);
            }
        }
        catch (IOException)
        {
            // MediaElement puede liberar el archivo de forma asíncrona; se reutiliza el directorio temporal.
        }
        catch (UnauthorizedAccessException)
        {
            // La limpieza del proxy no debe interrumpir el flujo principal.
        }
    }

    private ImageConversionOptions CreateImageOptions(string outputPath, ImageOutputFormat format) => new()
    {
        InputPath = SelectedFilePath,
        OutputPath = outputPath,
        Format = format,
        TargetWidth = ImageTargetWidth,
        CropEnabled = ImageCropEnabled,
        CropX = ImageCropX,
        CropY = ImageCropY,
        CropWidth = ImageCropWidth,
        CropHeight = ImageCropHeight,
        Brightness = ImageBrightness,
        Contrast = ImageContrast,
        Saturation = ImageSaturation,
        Temperature = ImageTemperature,
        Filter = SelectedImageFilter,
        ExtraFilter = ActiveImageFilter,
        Quality = ImageQuality
    };

    private ImageOutputFormat GetImageFormat() => SelectedImageFormat.ToUpperInvariant() switch
    {
        "JPG" or "JPEG" => ImageOutputFormat.Jpeg,
        "WEBP" => ImageOutputFormat.WebP,
        _ => ImageOutputFormat.Png
    };

    private void UpdateProbeSummary(MediaProbeResult result)
    {
        var video = result.PrimaryVideoStream;
        var audio = result.PrimaryAudioStream;
        var duration = result.Duration?.ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture) ?? "—";

        MediaSummary = $"{result.MediaType} · {result.FormatName ?? "formato desconocido"} · " +
                       $"{FormatBytes(result.File.SizeBytes)} · {duration}";
        TechnicalSummary =
            $"Video: {video?.CodecName ?? "—"} · {video?.Width?.ToString() ?? "—"}×{video?.Height?.ToString() ?? "—"} · " +
            $"{video?.FramesPerSecond?.ToString("0.###", CultureInfo.InvariantCulture) ?? "—"} FPS\n" +
            $"Audio: {audio?.CodecName ?? "—"} · {audio?.SampleRate?.ToString() ?? "—"} Hz · " +
            $"{audio?.Channels?.ToString() ?? "—"} canales";
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        var value = (double)bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.##} {units[unit]}";
    }

    private static BitmapImage? TryLoadBitmap(string path)
    {
        try
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (NotSupportedException)
        {
            return null;
        }
        catch (FileFormatException)
        {
            return null;
        }
    }

    private static string Truncate(string value, int maximumLength) =>
        value.Length <= maximumLength ? value : "…" + value[^maximumLength..];

    private static void DeleteIncompleteOutput(string outputPath)
    {
        try
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
        catch (IOException)
        {
            // La cancelación sigue siendo válida aunque Windows tarde en liberar el archivo.
        }
    }

    private void OnQueueChanged(object? sender, EventArgs e) => RefreshQueueItems();

    private void RefreshQueueItems()
    {
        QueueItems.Clear();
        foreach (var item in _queue.Jobs
                     .OrderByDescending(job => job.CreatedAtUtc)
                     .Take(8)
                     .Select(QueueItemViewModel.FromJob))
        {
            QueueItems.Add(item);
        }

        HasQueueItems = QueueItems.Count > 0;
        var active = _queue.Jobs.Count(job =>
            job.Status is JobStatus.Pending or JobStatus.Analyzing or JobStatus.Processing);
        QueueSummary = active > 0
            ? $"Cola persistente · {_queue.Jobs.Count} trabajo(s) · {active} activo(s)"
            : $"Cola persistente · {_queue.Jobs.Count} trabajo(s)";
    }
}
