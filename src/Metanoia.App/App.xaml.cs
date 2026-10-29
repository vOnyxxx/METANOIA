using System.IO;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Metanoia.App.Services;
using Metanoia.App.ViewModels;
using Metanoia.Core.Contracts;
using Metanoia.Core.Services;
using Metanoia.Infrastructure.FFmpeg;
using Metanoia.Infrastructure.Files;
using Metanoia.Infrastructure.Queue;
using Metanoia.Infrastructure.Settings;

namespace Metanoia.App;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var settings = new FFToolsSettingsService();
        var outputPaths = new OutputPathService();
        var queue = new ProcessingQueue();
        var probeParser = new FfprobeJsonParser();
        var progressParser = new FFmpegProgressParser();

        IMediaProbeService? probe = settings.FFprobePath is null
            ? null
            : new MediaProbeService(settings.FFprobePath, probeParser);
        IFFmpegCommandBuilder? commandBuilder = settings.FFmpegPath is null
            ? null
            : new FFmpegCommandBuilder(settings.FFmpegPath);
        IFFmpegRunner? runner = settings.FFmpegPath is null
            ? null
            : new FFmpegRunner(progressParser);

        var viewModel = new MainViewModel(
            settings,
            probe,
            commandBuilder,
            runner,
            queue,
            outputPaths,
            new FileDialogService(),
            new ClipboardService(),
            new SystemLauncher(),
            new AdvancedPresetService());

        viewModel.CurrentSection = GetInitialSection(e.Args);
        viewModel.AutomaticParameterPreviews = GetCapturePath(e.Args) is null && !HasSwitch(e.Args, "--exit-after-automation");

        var window = new ShellWindow(viewModel);
        MainWindow = window;
        window.Show();

        var loadPath = GetArgumentValue(e.Args, "--load-file");
        if (loadPath is not null)
        {
            await viewModel.LoadFileAsync(Path.GetFullPath(loadPath));
        }
        if (GetArgumentValue(e.Args, "--engine-filter") is { } engineFilter)
        {
            await viewModel.LoadFormatCatalogAsync();
            viewModel.SelectedEngineFilter = engineFilter;
            await viewModel.InspectFilterCommand.ExecuteAsync(null);
            viewModel.CustomFilterParameters = GetArgumentValue(e.Args, "--filter-parameters") ?? "";
            viewModel.ApplyEngineFilterCommand.Execute(null);
        }

        if (e.Args.Any(argument => string.Equals(argument, "--extract-audio", StringComparison.OrdinalIgnoreCase)))
        {
            viewModel.SelectedVideoOperation = "Extraer audio MP3";
        }

        ApplyVideoAutomationOptions(viewModel, e.Args);

        var videoOutputPath = GetArgumentValue(e.Args, "--video-output");
        if (videoOutputPath is not null)
        {
            viewModel.OutputPath = Path.GetFullPath(videoOutputPath);
        }

        var automationSucceeded = true;
        var advancedArguments = GetArgumentValue(e.Args, "--advanced-arguments");
        if (advancedArguments is not null)
        {
            viewModel.AdvancedArgumentsText = advancedArguments;
            viewModel.ValidateAdvancedArgumentsCommand.Execute(null);
            automationSucceeded &= viewModel.AreAdvancedArgumentsValid;
        }

        var previewVideoRequested = HasSwitch(e.Args, "--preview-video");
        if (previewVideoRequested && viewModel.GenerateVideoProxyCommand.CanExecute(null))
        {
            await viewModel.GenerateVideoProxyCommand.ExecuteAsync(null);
            automationSucceeded &= viewModel.HasVideoPreview;
        }
        else if (previewVideoRequested)
        {
            automationSucceeded = false;
        }

        var exportVideoRequested = HasSwitch(e.Args, "--export-video");
        if (exportVideoRequested && viewModel.ProcessCommand.CanExecute(null))
        {
            await viewModel.ProcessCommand.ExecuteAsync(null);
            automationSucceeded &= viewModel.ResultAvailable;
        }
        else if (exportVideoRequested)
        {
            automationSucceeded = false;
        }

        var imageOutputPath = GetArgumentValue(e.Args, "--image-output");
        if (imageOutputPath is not null)
        {
            viewModel.ImageOutputPath = Path.GetFullPath(imageOutputPath);
        }

        var previewImageRequested = HasSwitch(e.Args, "--preview-image");
        if (previewImageRequested && viewModel.PreviewImageCommand.CanExecute(null))
        {
            await viewModel.PreviewImageCommand.ExecuteAsync(null);
            automationSucceeded &= viewModel.HasImageAfterPreview;
        }
        else if (previewImageRequested)
        {
            automationSucceeded = false;
        }

        var exportImageRequested = HasSwitch(e.Args, "--export-image");
        if (exportImageRequested && viewModel.ProcessImageCommand.CanExecute(null))
        {
            await viewModel.ProcessImageCommand.ExecuteAsync(null);
            automationSucceeded &= viewModel.ImageResultAvailable;
        }
        else if (exportImageRequested)
        {
            automationSucceeded = false;
        }

        ApplyAudioAutomationOptions(viewModel, e.Args);
        if (TryGetDoubleArgument(e.Args, "--eq-low", out var lowGain)) viewModel.AudioLowGain = lowGain;
        if (TryGetDoubleArgument(e.Args, "--eq-mid", out var midGain)) viewModel.AudioMidGain = midGain;
        if (TryGetDoubleArgument(e.Args, "--eq-high", out var highGain)) viewModel.AudioHighGain = highGain;
        if (GetArgumentValue(e.Args, "--visualization") is { } visualization) viewModel.SelectedAudioVisualization = visualization;
        var audioOutputPath = GetArgumentValue(e.Args, "--audio-output");
        if (audioOutputPath is not null)
        {
            viewModel.AudioOutputPath = Path.GetFullPath(audioOutputPath);
        }

        var previewAudioRequested = HasSwitch(e.Args, "--preview-audio");
        if (previewAudioRequested && viewModel.GenerateAudioPreviewCommand.CanExecute(null))
        {
            await viewModel.GenerateAudioPreviewCommand.ExecuteAsync(null);
            automationSucceeded &= viewModel.HasAudioPreview;
        }
        else if (previewAudioRequested)
        {
            automationSucceeded = false;
        }

        var exportAudioRequested = HasSwitch(e.Args, "--export-audio");
        if (exportAudioRequested && viewModel.ProcessAudioCommand.CanExecute(null))
        {
            await viewModel.ProcessAudioCommand.ExecuteAsync(null);
            automationSucceeded &= viewModel.AudioResultAvailable;
        }
        else if (exportAudioRequested)
        {
            automationSucceeded = false;
        }

        var catalogFormat = GetArgumentValue(e.Args, "--catalog-format");
        if (catalogFormat is not null)
        {
            await viewModel.LoadFormatCatalogAsync();
            viewModel.SelectedContainer = viewModel.CompatibleContainers.FirstOrDefault(c => c.Name == catalogFormat);
            if (GetArgumentValue(e.Args, "--catalog-output") is { } catalogOutput) viewModel.CatalogOutputPath = Path.GetFullPath(catalogOutput);
            if (viewModel.SelectedContainer is null) automationSucceeded = false;
            else if (HasSwitch(e.Args, "--export-catalog"))
            {
                await viewModel.ExportCatalogCommand.ExecuteAsync(null);
                automationSucceeded &= viewModel.CatalogResultAvailable;
            }
        }
        var capturePath = GetCapturePath(e.Args);
        if (capturePath is not null)
        {
            await CaptureWindowAsync(window, capturePath);
        }
        if (GetArgumentValue(e.Args, "--report") is { } report)
        {
            var reportPath = Path.GetFullPath(report);
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
            await File.WriteAllTextAsync(reportPath, System.Text.Json.JsonSerializer.Serialize(new
            {
                automationSucceeded, viewModel.SelectedFileName, viewModel.HasVideoPreview,
                viewModel.HasImageAfterPreview, viewModel.HasAudioPreview, viewModel.CatalogResultAvailable,
                viewModel.CapabilitySummary, filterCount = viewModel.AvailableFilters.Count,
                parameterCount = viewModel.FilterParameters.Count, viewModel.FilterStatus,
                viewModel.StatusText, viewModel.AudioCommandDisplay, viewModel.CatalogCommandDisplay
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }

        if (capturePath is not null || HasSwitch(e.Args, "--exit-after-automation"))
        {
            Shutdown(automationSucceeded ? 0 : 1);
        }
    }

    private static string? GetCapturePath(IReadOnlyList<string> arguments)
    {
        for (var index = 0; index < arguments.Count - 1; index++)
        {
            if (string.Equals(arguments[index], "--capture-ui", StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFullPath(arguments[index + 1]);
            }
        }

        return null;
    }

    private static AppSection GetInitialSection(IReadOnlyList<string> arguments)
    {
        for (var index = 0; index < arguments.Count - 1; index++)
        {
            if (!string.Equals(arguments[index], "--section", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return arguments[index + 1].ToLowerInvariant() switch
            {
                "image" or "imagen" => AppSection.Image,
                "video" => AppSection.Video,
                "audio" => AppSection.Audio,
                "advanced" or "avanzado" => AppSection.Advanced,
                _ => AppSection.Home
            };
        }

        return AppSection.Home;
    }

    private static string? GetArgumentValue(IReadOnlyList<string> arguments, string name)
    {
        for (var index = 0; index < arguments.Count - 1; index++)
        {
            if (string.Equals(arguments[index], name, StringComparison.OrdinalIgnoreCase))
            {
                return arguments[index + 1];
            }
        }

        return null;
    }

    private static bool HasSwitch(IReadOnlyList<string> arguments, string name) =>
        arguments.Any(argument => string.Equals(argument, name, StringComparison.OrdinalIgnoreCase));

    private static void ApplyVideoAutomationOptions(MainViewModel viewModel, IReadOnlyList<string> arguments)
    {
        if (TryGetDoubleArgument(arguments, "--trim-start", out var trimStart))
        {
            viewModel.VideoTrimStartSeconds = trimStart;
        }

        if (TryGetDoubleArgument(arguments, "--trim-duration", out var trimDuration))
        {
            viewModel.VideoTrimDurationSeconds = trimDuration;
        }

        if (int.TryParse(
                GetArgumentValue(arguments, "--crf"),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var crf))
        {
            viewModel.Crf = crf;
        }

        var preset = GetArgumentValue(arguments, "--preset");
        if (!string.IsNullOrWhiteSpace(preset))
        {
            viewModel.SelectedPreset = preset.ToLowerInvariant();
        }

        var resolution = GetArgumentValue(arguments, "--resolution")?.ToLowerInvariant();
        viewModel.SelectedResolution = resolution switch
        {
            "720" or "720p" => "720p",
            "1080" or "1080p" => "1080p",
            "original" => "Original",
            _ => viewModel.SelectedResolution
        };
    }

    private static void ApplyAudioAutomationOptions(MainViewModel viewModel, IReadOnlyList<string> arguments)
    {
        if (TryGetDoubleArgument(arguments, "--audio-trim-start", out var trimStart))
        {
            viewModel.AudioTrimStartSeconds = trimStart;
        }
        if (TryGetDoubleArgument(arguments, "--audio-trim-duration", out var trimDuration))
        {
            viewModel.AudioTrimDurationSeconds = trimDuration;
        }
        if (TryGetDoubleArgument(arguments, "--audio-target-lufs", out var targetLufs))
        {
            viewModel.AudioTargetLufs = targetLufs;
        }
        if (TryGetDoubleArgument(arguments, "--audio-true-peak", out var truePeak))
        {
            viewModel.AudioTruePeakDb = truePeak;
        }
        if (HasSwitch(arguments, "--no-normalize"))
        {
            viewModel.NormalizeAudio = false;
        }

        viewModel.SelectedAudioFormat = GetArgumentValue(arguments, "--audio-format")?.ToLowerInvariant() switch
        {
            "mp3" => "MP3 192 kbps",
            "aac" or "m4a" => "AAC 256 kbps",
            "wav" => "WAV 24-bit",
            _ => viewModel.SelectedAudioFormat
        };
        viewModel.SelectedAudioSampleRate = GetArgumentValue(arguments, "--audio-sample-rate")?.ToLowerInvariant() switch
        {
            "44100" or "44.1" or "44.1khz" => "44.1 kHz",
            "48000" or "48" or "48khz" => "48 kHz",
            "96000" or "96" or "96khz" => "96 kHz",
            "original" => "Original",
            _ => viewModel.SelectedAudioSampleRate
        };
        viewModel.SelectedAudioChannels = GetArgumentValue(arguments, "--audio-channels")?.ToLowerInvariant() switch
        {
            "1" or "mono" => "Mono",
            "2" or "stereo" or "estereo" => "Estéreo",
            "original" => "Original",
            _ => viewModel.SelectedAudioChannels
        };
    }

    private static bool TryGetDoubleArgument(
        IReadOnlyList<string> arguments,
        string name,
        out double value) =>
        double.TryParse(
            GetArgumentValue(arguments, name),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value);

    private static async Task CaptureWindowAsync(Window window, string outputPath)
    {
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        await Task.Delay(1500);

        var content = (FrameworkElement)window.Content;
        var width = Math.Max(1, (int)Math.Ceiling(content.ActualWidth));
        var height = Math.Max(1, (int)Math.Ceiling(content.ActualHeight));
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(content);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)
            ?? throw new InvalidOperationException("La ruta de captura no tiene carpeta."));

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        await using var stream = File.Create(outputPath);
        encoder.Save(stream);
    }
}
