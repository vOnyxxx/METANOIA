using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Metanoia.Core.Models;
using Metanoia.Core.Services;

namespace Metanoia.App.ViewModels;

public partial class MainViewModel
{
    private string? _audioPreviewPath;
    private string? _audioWaveformPath;
    private string? _audioSpectrogramPath;

    public IReadOnlyList<string> AudioFormats { get; } = ["WAV 24-bit", "MP3 192 kbps", "AAC 256 kbps"];

    public IReadOnlyList<string> AudioSampleRates { get; } = ["Original", "44.1 kHz", "48 kHz", "96 kHz"];

    public IReadOnlyList<string> AudioChannelModes { get; } = ["Original", "Mono", "Estéreo"];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ProcessAudioCommand))]
    [NotifyCanExecuteChangedFor(nameof(GenerateAudioPreviewCommand))]
    private bool _isAudioLoaded;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ProcessAudioCommand))]
    private string _audioOutputPath = string.Empty;

    [ObservableProperty]
    private string _selectedAudioFormat = "WAV 24-bit";

    [ObservableProperty]
    private string _selectedAudioSampleRate = "48 kHz";

    [ObservableProperty]
    private string _selectedAudioChannels = "Estéreo";

    [ObservableProperty]
    private double _audioTrimStartSeconds;

    [ObservableProperty]
    private double _audioTrimDurationSeconds;

    [ObservableProperty]
    private double _audioDurationSeconds = 1;

    [ObservableProperty]
    private bool _normalizeAudio = true;

    [ObservableProperty]
    private double _audioTargetLufs = -14;

    [ObservableProperty]
    private double _audioTruePeakDb = -1;

    [ObservableProperty]
    private Uri? _audioPreviewSource;

    [ObservableProperty]
    private BitmapImage? _audioWaveformImage;

    [ObservableProperty]
    private BitmapImage? _audioSpectrogramImage;

    [ObservableProperty]
    private bool _hasAudioPreview;

    [ObservableProperty]
    private string _audioPreviewStatus = "Carga un audio para generar su análisis visual.";

    [ObservableProperty]
    private string _audioCommandDisplay = "Importa un audio para generar el flujo FFmpeg.";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenAudioOutputFolderCommand))]
    private bool _audioResultAvailable;

    [ObservableProperty]
    private string _audioResultMessage = "Sin resultados todavía.";

    [RelayCommand]
    private void SelectAudioOutput()
    {
        if (!IsAudioLoaded)
        {
            StatusText = "Primero selecciona un archivo de audio.";
            return;
        }

        var format = GetAudioFormat();
        var suggested = string.IsNullOrWhiteSpace(AudioOutputPath)
            ? _outputPaths.SuggestAudioOutputPath(SelectedFilePath, format)
            : AudioOutputPath;
        var selected = _fileDialogs.SelectOutputAudio(suggested, format);
        if (selected is not null)
        {
            AudioOutputPath = selected;
        }
    }

    [RelayCommand(CanExecute = nameof(CanGenerateAudioPreview))]
    private async Task GenerateAudioPreviewAsync()
    {
        if (_probeResult is null || _commandBuilder is null || _runner is null)
        {
            AudioPreviewStatus = "El entorno de análisis de audio no está disponible.";
            return;
        }

        var previewDirectory = Path.Combine(Path.GetTempPath(), "METANOIA", "audio-previews");
        Directory.CreateDirectory(previewDirectory);
        var token = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var previewPath = Path.Combine(previewDirectory, $"audio-proxy-{token}.mp3");
        var waveformPath = Path.Combine(previewDirectory, $"waveform-{token}.png");
        var spectrogramPath = Path.Combine(previewDirectory, $"spectrogram-{token}.png");
        var trimStartSnapshot = AudioTrimStartSeconds;
        var trimDurationSnapshot = AudioTrimDurationSeconds;
        var expectedDuration = GetExpectedAudioDuration(null, trimStartSnapshot, trimDurationSnapshot);

        try
        {
            if (expectedDuration <= TimeSpan.Zero)
            {
                throw new InvalidOperationException("El recorte no contiene un intervalo reproducible.");
            }

            _processingCancellation = new CancellationTokenSource();
            IsBusy = true;
            IsProcessing = true;
            HasAudioPreview = false;
            ProgressPercent = 0;
            AudioPreviewStatus = "Generando proxy MP3 para reproducción…";

            var previewOptions = new AudioConversionOptions
            {
                InputPath = SelectedFilePath,
                OutputPath = previewPath,
                Format = AudioOutputFormat.Mp3,
                TrimStartSeconds = trimStartSnapshot,
                TrimDurationSeconds = expectedDuration.TotalSeconds,
                SampleRate = 48000,
                Channels = 2,
                NormalizeLoudness = false,
                ExtraFilter = ActiveAudioFilter,
        LowGainDb = AudioLowGain, MidGainDb = AudioMidGain, HighGainDb = AudioHighGain, GainDb = AudioGain
            };
            var previewCommand = _commandBuilder.BuildAudioConversion(_probeResult, previewOptions);
            var previewProgress = new Progress<JobProgress>(value =>
            {
                ProgressPercent = (value.Percent ?? 0) * 0.6;
                AudioPreviewStatus = $"Generando proxy · {ProgressPercent:0.0}%";
            });
            var previewResult = await _runner.RunAsync(
                previewCommand,
                expectedDuration,
                previewProgress,
                _processingCancellation.Token);
            if (previewResult.WasCancelled)
            {
                CleanupAudioPreviewFiles(previewPath, waveformPath, spectrogramPath);
                AudioPreviewStatus = "Análisis cancelado sin conservar archivos parciales.";
                return;
            }
            EnsureSuccessfulAudioArtifact(previewResult, previewPath, "proxy de audio");

            var visualSource = _probeResult with { File = _probeResult.File with { FullPath = previewPath }, Duration = expectedDuration };
            AudioPreviewStatus = "Generando waveform…";
            ProgressPercent = 65;
            var waveformCommand = _commandBuilder.BuildAudioVisualization(
                visualSource,
                waveformPath,
                SelectedAudioVisualization == "Onda por canal" ? AudioVisualizationKind.StereoWaveform : AudioVisualizationKind.Waveform,
                0,
                expectedDuration.TotalSeconds);
            var waveformResult = await _runner.RunAsync(
                waveformCommand,
                expectedDuration,
                cancellationToken: _processingCancellation.Token);
            if (waveformResult.WasCancelled)
            {
                CleanupAudioPreviewFiles(previewPath, waveformPath, spectrogramPath);
                AudioPreviewStatus = "Análisis cancelado sin conservar archivos parciales.";
                return;
            }
            EnsureSuccessfulAudioArtifact(waveformResult, waveformPath, "waveform");

            AudioPreviewStatus = "Generando espectrograma…";
            ProgressPercent = 82;
            var spectrogramCommand = _commandBuilder.BuildAudioVisualization(
                visualSource,
                spectrogramPath,
                SelectedAudioVisualization == "Espectro de frecuencias" ? AudioVisualizationKind.Spectrum : AudioVisualizationKind.Spectrogram,
                0,
                expectedDuration.TotalSeconds);
            var spectrogramResult = await _runner.RunAsync(
                spectrogramCommand,
                expectedDuration,
                cancellationToken: _processingCancellation.Token);
            if (spectrogramResult.WasCancelled)
            {
                CleanupAudioPreviewFiles(previewPath, waveformPath, spectrogramPath);
                AudioPreviewStatus = "Análisis cancelado sin conservar archivos parciales.";
                return;
            }
            EnsureSuccessfulAudioArtifact(spectrogramResult, spectrogramPath, "espectrograma");

            if (Math.Abs(AudioTrimStartSeconds - trimStartSnapshot) > 0.0001 ||
                Math.Abs(AudioTrimDurationSeconds - trimDurationSnapshot) > 0.0001)
            {
                CleanupAudioPreviewFiles(previewPath, waveformPath, spectrogramPath);
                AudioPreviewStatus = "El recorte cambió durante el análisis · genera la vista nuevamente.";
                return;
            }

            var oldPreview = _audioPreviewPath;
            var oldWaveform = _audioWaveformPath;
            var oldSpectrogram = _audioSpectrogramPath;
            _audioPreviewPath = previewPath;
            _audioWaveformPath = waveformPath;
            _audioSpectrogramPath = spectrogramPath;
            AudioPreviewSource = new Uri(previewPath);
            AudioWaveformImage = TryLoadBitmap(waveformPath);
            AudioSpectrogramImage = TryLoadBitmap(spectrogramPath);
            HasAudioPreview = AudioWaveformImage is not null && AudioSpectrogramImage is not null;
            ProgressPercent = 100;
            AudioPreviewStatus = HasAudioPreview
                ? "Vista de escucha lista"
                : "FFmpeg generó el análisis, pero la interfaz no pudo cargar una imagen.";
            CleanupAudioPreviewFiles(oldPreview, oldWaveform, oldSpectrogram);
        }
        catch (Exception exception)
        {
            CleanupAudioPreviewFiles(previewPath, waveformPath, spectrogramPath);
            AudioPreviewStatus = exception.Message;
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

    private bool CanGenerateAudioPreview() => AreToolsAvailable && IsAudioLoaded && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanProcessAudio))]
    private async Task ProcessAudioAsync()
    {
        if (_probeResult is null || _commandBuilder is null || _runner is null)
        {
            StatusText = "El entorno de procesamiento de audio no está disponible.";
            return;
        }

        ProcessingJob? job = null;
        try
        {
            IsExporting = true;
            var baseOptions = CreateAudioOptions(measurement: null);
            _outputPaths.ValidateAudioOutputPath(SelectedFilePath, AudioOutputPath, baseOptions.Format);
            job = new ProcessingJob(SelectedFilePath, AudioOutputPath);
            _queue.Enqueue(job);
            _processingCancellation = new CancellationTokenSource();
            IsBusy = true;
            IsProcessing = true;
            AudioResultAvailable = false;
            AudioResultMessage = "Procesamiento de audio en curso…";
            ProgressPercent = 0;
            var expectedDuration = GetExpectedAudioDuration();

            AudioLoudnessMeasurement? measurement = null;
            FFmpegCommand? analysisCommand = null;
            if (NormalizeAudio)
            {
                job.UpdateStatus(JobStatus.Analyzing);
                _queue.Update(job);
                StatusText = "Paso 1/2 · midiendo sonoridad integrada y true peak…";
                analysisCommand = _commandBuilder.BuildAudioLoudnessAnalysis(_probeResult, baseOptions);
                AudioCommandDisplay = "PASO 1 · MEDICIÓN LUFS\n" + analysisCommand.ToDisplayString();
                var analysisProgress = new Progress<JobProgress>(value =>
                {
                    ProgressPercent = (value.Percent ?? 0) * 0.35;
                    StatusText = $"Paso 1/2 · medición LUFS · {ProgressPercent:0.0}%";
                });
                var analysisResult = await _runner.RunAsync(
                    analysisCommand,
                    expectedDuration,
                    analysisProgress,
                    _processingCancellation.Token);
                if (analysisResult.WasCancelled)
                {
                    job.UpdateStatus(JobStatus.Cancelled);
                    _queue.Update(job);
                    StatusText = "Normalización cancelada de forma segura.";
                    AudioResultMessage = "Cancelado antes de exportar.";
                    return;
                }
                if (!analysisResult.Succeeded)
                {
                    throw new InvalidOperationException("No fue posible medir LUFS · " + Truncate(analysisResult.StandardError.Trim(), 500));
                }
                measurement = new LoudnessAnalysisParser().Parse(analysisResult.StandardError);
            }

            var exportOptions = CreateAudioOptions(measurement);
            var exportCommand = ApplyAdvancedArguments(
                _commandBuilder.BuildAudioConversion(_probeResult, exportOptions));
            AudioCommandDisplay = analysisCommand is null
                ? exportCommand.ToDisplayString()
                : "PASO 1 · MEDICIÓN LUFS\n" + analysisCommand.ToDisplayString() +
                  "\n\nPASO 2 · NORMALIZACIÓN Y EXPORTACIÓN\n" + exportCommand.ToDisplayString();
            job.UpdateStatus(JobStatus.Processing);
            _queue.Update(job);
            StatusText = NormalizeAudio
                ? "Paso 2/2 · normalizando y exportando audio…"
                : "Exportando audio sin normalización LUFS…";
            var exportProgress = new Progress<JobProgress>(value =>
            {
                var basePercent = NormalizeAudio ? 35 : 0;
                var scale = NormalizeAudio ? 0.65 : 1;
                ProgressPercent = basePercent + (value.Percent ?? 0) * scale;
                job.UpdateProgress(value);
                StatusText = $"Exportando · {ProgressPercent:0.0}% · {value.Speed ?? "—"}";
            });
            var exportResult = await _runner.RunAsync(
                exportCommand,
                expectedDuration,
                exportProgress,
                _processingCancellation.Token);
            if (exportResult.WasCancelled)
            {
                job.UpdateStatus(JobStatus.Cancelled);
                _queue.Update(job);
                DeleteIncompleteOutput(AudioOutputPath);
                StatusText = "Exportación cancelada de forma segura.";
                AudioResultMessage = "Cancelado · no se conservó una salida incompleta.";
                return;
            }
            EnsureSuccessfulAudioArtifact(exportResult, AudioOutputPath, "archivo de audio");

            job.UpdateStatus(JobStatus.Completed);
            _queue.Update(job);
            ProgressPercent = 100;
            AudioResultAvailable = true;
            StatusText = NormalizeAudio
                ? "Audio normalizado en dos pasadas y exportado."
                : "Audio exportado y verificado.";
            AudioResultMessage = $"Salida: {AudioOutputPath}";
        }
        catch (Exception exception)
        {
            job?.UpdateStatus(JobStatus.Error, exception.Message);
            if (job is not null)
            {
                _queue.Update(job);
            }
            DeleteIncompleteOutput(AudioOutputPath);
            StatusText = "No fue posible completar el procesamiento de audio.";
            AudioResultMessage = exception.Message;
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

    private bool CanProcessAudio() =>
        AreToolsAvailable && IsAudioLoaded && !IsBusy && !string.IsNullOrWhiteSpace(AudioOutputPath);

    [RelayCommand]
    private void CopyAudioCommand()
    {
        if (!string.IsNullOrWhiteSpace(AudioCommandDisplay) && IsAudioLoaded)
        {
            _clipboard.SetText(AudioCommandDisplay);
            StatusText = "Flujo FFmpeg de audio copiado.";
        }
    }

    [RelayCommand(CanExecute = nameof(CanOpenAudioOutputFolder))]
    private void OpenAudioOutputFolder() => _launcher.OpenContainingFolder(AudioOutputPath);

    private bool CanOpenAudioOutputFolder() => AudioResultAvailable && File.Exists(AudioOutputPath);

    partial void OnAudioOutputPathChanged(string value) => RefreshAudioCommandPreview();

    partial void OnSelectedAudioSampleRateChanged(string value) => RefreshAudioCommandPreview();

    partial void OnSelectedAudioChannelsChanged(string value) => RefreshAudioCommandPreview();

    partial void OnNormalizeAudioChanged(bool value) => RefreshAudioCommandPreview();

    partial void OnAudioTargetLufsChanged(double value) => RefreshAudioCommandPreview();

    partial void OnAudioTruePeakDbChanged(double value) => RefreshAudioCommandPreview();

    partial void OnAudioTrimStartSecondsChanged(double value)
    {
        InvalidateAudioPreviewForTrimChange();
        RefreshAudioCommandPreview();
    }

    partial void OnAudioTrimDurationSecondsChanged(double value)
    {
        InvalidateAudioPreviewForTrimChange();
        RefreshAudioCommandPreview();
    }

    partial void OnSelectedAudioFormatChanged(string value)
    {
        if (IsAudioLoaded)
        {
            AudioOutputPath = _outputPaths.SuggestAudioOutputPath(SelectedFilePath, GetAudioFormat());
        }
        RefreshAudioCommandPreview();
    }

    private void RefreshAudioCommandPreview()
    {
        if (_probeResult?.MediaType != MediaType.Audio || _commandBuilder is null || !IsAudioLoaded)
        {
            return;
        }

        try
        {
            var options = CreateAudioOptions(measurement: null);
            _outputPaths.ValidateAudioOutputPath(SelectedFilePath, AudioOutputPath, options.Format);
            if (NormalizeAudio)
            {
                var analysis = _commandBuilder.BuildAudioLoudnessAnalysis(_probeResult, options);
                AudioCommandDisplay = "PASO 1 · MEDICIÓN LUFS\n" + analysis.ToDisplayString() +
                    "\n\nPASO 2 · se completa con las mediciones reales y exporta el formato seleccionado." +
                    (UseAdvancedArguments ? "\nOpciones v0.6 validadas: " + AdvancedArgumentsText : string.Empty);
            }
            else
            {
                AudioCommandDisplay = ApplyAdvancedArguments(
                    _commandBuilder.BuildAudioConversion(_probeResult, options)).ToDisplayString();
            }
        }
        catch (Exception exception)
        {
            AudioCommandDisplay = $"Configura valores válidos · {exception.Message}";
        }
    }

    private AudioConversionOptions CreateAudioOptions(AudioLoudnessMeasurement? measurement) => new()
    {
        InputPath = SelectedFilePath,
        OutputPath = AudioOutputPath,
        Format = GetAudioFormat(),
        TrimStartSeconds = AudioTrimStartSeconds,
        TrimDurationSeconds = AudioTrimDurationSeconds,
        SampleRate = GetAudioSampleRate(),
        Channels = GetAudioChannels(),
        NormalizeLoudness = NormalizeAudio,
        TargetLufs = AudioTargetLufs,
        TruePeakDb = AudioTruePeakDb,
        LoudnessRangeLu = 11,
        ExtraFilter = ActiveAudioFilter,
        LowGainDb = AudioLowGain, MidGainDb = AudioMidGain, HighGainDb = AudioHighGain, GainDb = AudioGain,
        LoudnessMeasurement = measurement
    };

    private AudioOutputFormat GetAudioFormat() => SelectedAudioFormat.ToUpperInvariant() switch
    {
        var value when value.StartsWith("MP3", StringComparison.Ordinal) => AudioOutputFormat.Mp3,
        var value when value.StartsWith("AAC", StringComparison.Ordinal) => AudioOutputFormat.Aac,
        _ => AudioOutputFormat.Wav
    };

    private int GetAudioSampleRate() => SelectedAudioSampleRate switch
    {
        "44.1 kHz" => 44100,
        "48 kHz" => 48000,
        "96 kHz" => 96000,
        _ => 0
    };

    private int GetAudioChannels() => SelectedAudioChannels switch
    {
        "Mono" => 1,
        "Estéreo" => 2,
        _ => 0
    };

    private TimeSpan GetExpectedAudioDuration(
        double? maximumSeconds = null,
        double? trimStartSeconds = null,
        double? trimDurationSeconds = null)
    {
        var start = trimStartSeconds ?? AudioTrimStartSeconds;
        var requested = trimDurationSeconds ?? AudioTrimDurationSeconds;
        var sourceSeconds = _probeResult?.Duration?.TotalSeconds ?? AudioDurationSeconds;
        var remaining = Math.Max(0, sourceSeconds - start);
        var effective = requested > 0 ? Math.Min(requested, remaining) : remaining;
        if (maximumSeconds is { } maximum)
        {
            effective = Math.Min(effective, maximum);
        }
        return TimeSpan.FromSeconds(effective);
    }

    private void InvalidateAudioPreviewForTrimChange()
    {
        ScheduleAudioPreview();
        if (!HasAudioPreview && AudioPreviewSource is null)
        {
            return;
        }
        ClearAudioPreview();
        AudioPreviewStatus = "El recorte cambió · genera nuevamente waveform y espectrograma.";
    }

    private void ClearAudioPreview()
    {
        var preview = _audioPreviewPath;
        var waveform = _audioWaveformPath;
        var spectrogram = _audioSpectrogramPath;
        AudioPreviewSource = null;
        AudioWaveformImage = null;
        AudioSpectrogramImage = null;
        HasAudioPreview = false;
        _audioPreviewPath = null;
        _audioWaveformPath = null;
        _audioSpectrogramPath = null;
        CleanupAudioPreviewFiles(preview, waveform, spectrogram);
    }

    private static void CleanupAudioPreviewFiles(params string?[] paths)
    {
        foreach (var path in paths)
        {
            TryDeletePreview(path);
        }
    }

    private static void EnsureSuccessfulAudioArtifact(FFmpegRunResult result, string path, string description)
    {
        if (!result.Succeeded || !File.Exists(path) || new FileInfo(path).Length == 0)
        {
            var diagnostic = Truncate(result.StandardError.Trim(), 500);
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(diagnostic)
                    ? $"FFmpeg no produjo un {description} válido."
                    : $"FFmpeg no produjo un {description} válido · {diagnostic}");
        }
    }
}
