using Metanoia.Core.Contracts;
using Metanoia.Core.Models;
using System.Globalization;

namespace Metanoia.Core.Services;

public sealed class FFmpegCommandBuilder : IFFmpegCommandBuilder
{
    private static readonly HashSet<string> AllowedPresets = new(StringComparer.OrdinalIgnoreCase)
    {
        "ultrafast", "superfast", "veryfast", "faster", "fast",
        "medium", "slow", "slower", "veryslow"
    };

    private readonly string _ffmpegPath;

    public FFmpegCommandBuilder(string ffmpegPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ffmpegPath);
        FFmpegArgumentValidator.ValidateExecutable(ffmpegPath);
        _ffmpegPath = ffmpegPath;
    }

    public FFmpegCommand BuildVideoConversion(MediaProbeResult source, VideoConversionOptions options)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);

        if (source.MediaType != MediaType.Video)
        {
            throw new InvalidOperationException("La operación de video requiere un archivo de video.");
        }

        ValidateOptions(options);
        if (source.Duration is { } sourceDuration &&
            options.TrimStartSeconds >= sourceDuration.TotalSeconds)
        {
            throw new ArgumentOutOfRangeException(nameof(options.TrimStartSeconds), "El inicio del recorte debe estar dentro del video.");
        }

        if (options.OutputMode == VideoOutputMode.AudioMp3 && source.PrimaryAudioStream is null)
        {
            throw new InvalidOperationException("El archivo no contiene una pista de audio para extraer.");
        }

        if (options.OutputMode == VideoOutputMode.Mp4 && source.PrimaryVideoStream is null)
        {
            throw new InvalidOperationException("El archivo no contiene una pista de video para exportar.");
        }

        var arguments = new List<string> { "-y" };
        if (options.TrimStartSeconds > 0)
        {
            arguments.AddRange(["-ss", FormatSeconds(options.TrimStartSeconds)]);
        }
        arguments.AddRange(["-i", options.InputPath]);
        if (options.TrimDurationSeconds > 0)
        {
            arguments.AddRange(["-t", FormatSeconds(options.TrimDurationSeconds)]);
        }

        if (options.OutputMode == VideoOutputMode.AudioMp3)
        {
            arguments.AddRange([
                "-map", "0:a:0", "-vn", "-c:a", "libmp3lame", "-b:a", "192k",
                "-progress", "pipe:1", "-nostats", options.OutputPath
            ]);
            FFmpegArgumentValidator.ValidateArguments(arguments);
            return new FFmpegCommand(_ffmpegPath, arguments);
        }

        arguments.AddRange(["-map", "0:v:0", "-map", "0:a?"]);

        var videoFilters = new List<string>();
        if (options.Resolution == VideoResolutionMode.FullHd1080p) videoFilters.Add("scale=-2:1080");
        else if (options.Resolution == VideoResolutionMode.Hd720p) videoFilters.Add("scale=-2:720");
        if (!string.IsNullOrWhiteSpace(options.ExtraFilter)) videoFilters.Add(options.ExtraFilter);
        if (videoFilters.Count > 0) arguments.AddRange(["-vf", string.Join(',', videoFilters)]);

        arguments.AddRange(
        [
            "-c:v", "libx264",
            "-preset", options.Preset.ToLowerInvariant(),
            "-crf", options.Crf.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "-c:a", "aac",
            "-b:a", "192k",
            "-movflags", "+faststart",
            "-progress", "pipe:1",
            "-nostats",
            options.OutputPath
        ]);

        FFmpegArgumentValidator.ValidateArguments(arguments);
        return new FFmpegCommand(_ffmpegPath, arguments);
    }

    public FFmpegCommand BuildImageConversion(MediaProbeResult source, ImageConversionOptions options)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);

        if (source.MediaType != MediaType.Image)
        {
            throw new InvalidOperationException("La operación de imagen requiere un archivo de imagen.");
        }

        ValidateImageOptions(options);
        var filters = new List<string>();
        if (options.CropEnabled)
        {
            filters.Add($"crop={options.CropWidth}:{options.CropHeight}:{options.CropX}:{options.CropY}");
        }

        if (options.TargetWidth > 0)
        {
            filters.Add($"scale={options.TargetWidth}:-2");
        }

        if (Math.Abs(options.Brightness) > 0.0001 ||
            Math.Abs(options.Contrast - 1) > 0.0001 ||
            Math.Abs(options.Saturation - 1) > 0.0001)
        {
            filters.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"eq=brightness={options.Brightness:0.###}:contrast={options.Contrast:0.###}:saturation={options.Saturation:0.###}"));
        }

        if (Math.Abs(options.Temperature) > 0.0001)
            filters.Add(string.Create(CultureInfo.InvariantCulture, $"colorbalance=rs={options.Temperature * .35:0.###}:bs={-options.Temperature * .35:0.###}"));
        var artisticFilter = options.Filter switch
        {
            "Monocromo" => "hue=s=0",
            "Negativo" => "negate",
            "Sepia" => "colorchannelmixer=.393:.769:.189:0:.349:.686:.168:0:.272:.534:.131",
            "Enfoque" => "unsharp=5:5:0.8",
            "Suave" => "gblur=sigma=1.2",
            "Original" => "",
            _ => throw new ArgumentException("Filtro de imagen desconocido.")
        };
        if (artisticFilter.Length > 0) filters.Add(artisticFilter);
        if (!string.IsNullOrWhiteSpace(options.ExtraFilter)) filters.Add(options.ExtraFilter);

        var arguments = new List<string> { "-y", "-i", options.InputPath };
        if (filters.Count > 0)
        {
            arguments.AddRange(["-vf", string.Join(',', filters)]);
        }

        arguments.AddRange(["-frames:v", "1"]);
        switch (options.Format)
        {
            case ImageOutputFormat.Png:
                arguments.AddRange(["-c:v", "png"]);
                break;
            case ImageOutputFormat.Jpeg:
                arguments.AddRange(["-c:v", "mjpeg", "-q:v", MapJpegQuality(options.Quality).ToString(CultureInfo.InvariantCulture)]);
                break;
            case ImageOutputFormat.WebP:
                arguments.AddRange(["-c:v", "libwebp", "-quality", options.Quality.ToString(CultureInfo.InvariantCulture)]);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(options.Format));
        }

        arguments.AddRange(["-progress", "pipe:1", "-nostats", options.OutputPath]);
        FFmpegArgumentValidator.ValidateArguments(arguments);
        return new FFmpegCommand(_ffmpegPath, arguments);
    }

    public FFmpegCommand BuildAudioLoudnessAnalysis(MediaProbeResult source, AudioConversionOptions options)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);
        ValidateAudioOptions(source, options);

        var arguments = CreateAudioInputArguments(options);
        arguments.AddRange([
            "-map", "0:a:0",
            "-vn",
            "-af", JoinAudioFilters(options, BuildLoudnessFilter(options, measurement: null, printFormat: "json")),
            "-progress", "pipe:1",
            "-nostats",
            "-f", "null",
            "NUL"
        ]);

        FFmpegArgumentValidator.ValidateArguments(arguments);
        return new FFmpegCommand(_ffmpegPath, arguments);
    }

    public FFmpegCommand BuildAudioConversion(MediaProbeResult source, AudioConversionOptions options)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);
        ValidateAudioOptions(source, options);

        var arguments = CreateAudioInputArguments(options);
        arguments.AddRange(["-map", "0:a:0", "-vn"]);

        var audioFilters = JoinAudioFilters(options, options.NormalizeLoudness
            ? BuildLoudnessFilter(options, options.LoudnessMeasurement, printFormat: "summary") : null);
        if (audioFilters.Length > 0) arguments.AddRange(["-af", audioFilters]);

        if (options.SampleRate > 0)
        {
            arguments.AddRange(["-ar", options.SampleRate.ToString(CultureInfo.InvariantCulture)]);
        }
        if (options.Channels > 0)
        {
            arguments.AddRange(["-ac", options.Channels.ToString(CultureInfo.InvariantCulture)]);
        }

        switch (options.Format)
        {
            case AudioOutputFormat.Wav:
                arguments.AddRange(["-c:a", "pcm_s24le"]);
                break;
            case AudioOutputFormat.Mp3:
                arguments.AddRange(["-c:a", "libmp3lame", "-b:a", "192k"]);
                break;
            case AudioOutputFormat.Aac:
                arguments.AddRange(["-c:a", "aac", "-b:a", "256k", "-movflags", "+faststart"]);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(options.Format));
        }

        arguments.AddRange(["-progress", "pipe:1", "-nostats", options.OutputPath]);
        FFmpegArgumentValidator.ValidateArguments(arguments);
        return new FFmpegCommand(_ffmpegPath, arguments);
    }

    public FFmpegCommand BuildAudioVisualization(
        MediaProbeResult source,
        string outputPath,
        AudioVisualizationKind kind,
        double trimStartSeconds = 0,
        double trimDurationSeconds = 0)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ValidateAudioSource(source);
        ValidateTrim(source, trimStartSeconds, trimDurationSeconds);

        if (!Path.IsPathFullyQualified(outputPath) ||
            !string.Equals(Path.GetExtension(outputPath), ".png", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("La visualización de audio debe usar una ruta PNG absoluta.", nameof(outputPath));
        }
        var directory = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException("La carpeta de visualización no existe.");
        }

        var arguments = new List<string> { "-y" };
        if (trimStartSeconds > 0)
        {
            arguments.AddRange(["-ss", FormatSeconds(trimStartSeconds)]);
        }
        arguments.AddRange(["-i", source.File.FullPath]);
        if (trimDurationSeconds > 0)
        {
            arguments.AddRange(["-t", FormatSeconds(trimDurationSeconds)]);
        }

        var filter = kind switch
        {
            AudioVisualizationKind.Waveform =>
                "aformat=channel_layouts=mono,showwavespic=s=1200x260:filter=peak:scale=sqrt:colors=0x72DCE8",
            AudioVisualizationKind.StereoWaveform =>
                "showwavespic=s=1200x360:filter=peak:scale=sqrt:split_channels=1:colors=0x72DCE8",
            AudioVisualizationKind.Spectrum =>
                "showfreqs=s=1200x360:mode=bar:ascale=log:fscale=log:colors=0xC6FF4D",
            AudioVisualizationKind.Spectrogram =>
                "showspectrumpic=s=1200x260:legend=disabled:scale=log:color=intensity",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        arguments.AddRange([
            "-filter_complex", filter,
            "-frames:v", "1",
            "-c:v", "png",
            "-progress", "pipe:1",
            "-nostats",
            outputPath
        ]);

        FFmpegArgumentValidator.ValidateArguments(arguments);
        return new FFmpegCommand(_ffmpegPath, arguments);
    }

    private static List<string> CreateAudioInputArguments(AudioConversionOptions options)
    {
        var arguments = new List<string> { "-y" };
        if (options.TrimStartSeconds > 0)
        {
            arguments.AddRange(["-ss", FormatSeconds(options.TrimStartSeconds)]);
        }
        arguments.AddRange(["-i", options.InputPath]);
        if (options.TrimDurationSeconds > 0)
        {
            arguments.AddRange(["-t", FormatSeconds(options.TrimDurationSeconds)]);
        }
        return arguments;
    }

    /// <summary>EQ precedes loudness measurement AND the final pass to preserve two-pass consistency.</summary>
    private static string JoinAudioFilters(AudioConversionOptions options, string? normalization)
    {
        var filters = new List<string>();
        if (!string.IsNullOrWhiteSpace(options.ExtraFilter)) filters.Add(options.ExtraFilter);
        foreach (var (frequency, gain) in new[] { (100, options.LowGainDb), (1000, options.MidGainDb), (8000, options.HighGainDb) })
        {
            if (!double.IsFinite(gain) || Math.Abs(gain) > 12) throw new ArgumentOutOfRangeException(nameof(gain));
            if (Math.Abs(gain) > .001) filters.Add(string.Create(CultureInfo.InvariantCulture, $"equalizer=f={frequency}:t=o:w=1:g={gain:0.###}"));
        }
        if (!double.IsFinite(options.GainDb) || Math.Abs(options.GainDb) > 12) throw new ArgumentOutOfRangeException(nameof(options.GainDb));
        if (Math.Abs(options.GainDb) > .001) filters.Add(string.Create(CultureInfo.InvariantCulture, $"volume={options.GainDb:0.###}dB"));
        if (!string.IsNullOrWhiteSpace(normalization)) filters.Add(normalization);
        return string.Join(',', filters);
    }

    private static string BuildLoudnessFilter(
        AudioConversionOptions options,
        AudioLoudnessMeasurement? measurement,
        string printFormat)
    {
        var filter = string.Create(
            CultureInfo.InvariantCulture,
            $"loudnorm=I={options.TargetLufs:0.###}:TP={options.TruePeakDb:0.###}:LRA={options.LoudnessRangeLu:0.###}");
        if (measurement is not null)
        {
            filter += string.Create(
                CultureInfo.InvariantCulture,
                $":measured_I={measurement.InputIntegratedLufs:0.###}" +
                $":measured_TP={measurement.InputTruePeakDb:0.###}" +
                $":measured_LRA={measurement.InputLoudnessRangeLu:0.###}" +
                $":measured_thresh={measurement.InputThresholdLufs:0.###}" +
                $":offset={measurement.TargetOffsetDb:0.###}:linear=true");
        }
        return $"{filter}:print_format={printFormat}";
    }

    private static void ValidateAudioOptions(MediaProbeResult source, AudioConversionOptions options)
    {
        ValidateAudioSource(source);
        if (!Path.IsPathFullyQualified(options.InputPath) || !Path.IsPathFullyQualified(options.OutputPath))
        {
            throw new ArgumentException("Las rutas de audio deben ser absolutas.");
        }
        if (!File.Exists(options.InputPath))
        {
            throw new FileNotFoundException("No se encontró el archivo de audio de entrada.", options.InputPath);
        }
        if (string.Equals(Path.GetFullPath(options.InputPath), Path.GetFullPath(options.OutputPath), StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("La salida no puede sobrescribir el audio original.");
        }

        ValidateTrim(source, options.TrimStartSeconds, options.TrimDurationSeconds);
        if (options.SampleRate is < 0 or > 192000 || options.SampleRate is > 0 and < 8000)
        {
            throw new ArgumentOutOfRangeException(nameof(options.SampleRate), "El sample rate debe ser original o estar entre 8 kHz y 192 kHz.");
        }
        if (options.Channels is < 0 or > 2)
        {
            throw new ArgumentOutOfRangeException(nameof(options.Channels), "Los canales deben conservarse, convertirse a mono o a estéreo.");
        }
        if (options.TargetLufs is < -36 or > -5)
        {
            throw new ArgumentOutOfRangeException(nameof(options.TargetLufs), "El objetivo LUFS debe estar entre -36 y -5.");
        }
        if (options.TruePeakDb is < -9 or > 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.TruePeakDb), "El true peak debe estar entre -9 y 0 dBTP.");
        }
        if (options.LoudnessRangeLu is < 1 or > 50)
        {
            throw new ArgumentOutOfRangeException(nameof(options.LoudnessRangeLu), "El rango de sonoridad debe estar entre 1 y 50 LU.");
        }

        var expectedExtension = options.Format switch
        {
            AudioOutputFormat.Wav => ".wav",
            AudioOutputFormat.Mp3 => ".mp3",
            AudioOutputFormat.Aac => ".m4a",
            _ => throw new ArgumentOutOfRangeException(nameof(options.Format))
        };
        if (!string.Equals(Path.GetExtension(options.OutputPath), expectedExtension, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"La salida de audio debe usar extensión {expectedExtension}.", nameof(options.OutputPath));
        }
        var directory = Path.GetDirectoryName(options.OutputPath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException("La carpeta de salida de audio no existe.");
        }
    }

    private static void ValidateAudioSource(MediaProbeResult source)
    {
        if (source.MediaType != MediaType.Audio || source.PrimaryAudioStream is null)
        {
            throw new InvalidOperationException("La operación requiere un archivo de audio válido.");
        }
    }

    private static void ValidateTrim(MediaProbeResult source, double trimStartSeconds, double trimDurationSeconds)
    {
        if (trimStartSeconds < 0 || trimDurationSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(trimStartSeconds), "El inicio y la duración no pueden ser negativos.");
        }
        if (source.Duration is { } duration && trimStartSeconds >= duration.TotalSeconds)
        {
            throw new ArgumentOutOfRangeException(nameof(trimStartSeconds), "El inicio del recorte debe estar dentro del audio.");
        }
    }

    private static void ValidateOptions(VideoConversionOptions options)
    {
        if (!Path.IsPathFullyQualified(options.InputPath) ||
            !Path.IsPathFullyQualified(options.OutputPath))
        {
            throw new ArgumentException("Las rutas de entrada y salida deben ser absolutas.");
        }

        if (!File.Exists(options.InputPath))
        {
            throw new FileNotFoundException("No se encontró el archivo de entrada.", options.InputPath);
        }

        if (string.Equals(
                Path.GetFullPath(options.InputPath),
                Path.GetFullPath(options.OutputPath),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("La ruta de salida debe ser diferente de la entrada.");
        }

        if (options.OutputMode == VideoOutputMode.Mp4 && (options.Crf is < 0 or > 51))
        {
            throw new ArgumentOutOfRangeException(nameof(options.Crf), "CRF debe estar entre 0 y 51.");
        }

        if (options.OutputMode == VideoOutputMode.Mp4 && !AllowedPresets.Contains(options.Preset))
        {
            throw new ArgumentException("El preset de x264 no es válido.", nameof(options.Preset));
        }

        if (options.TrimStartSeconds < 0 || options.TrimDurationSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.TrimStartSeconds), "El inicio y la duración del recorte no pueden ser negativos.");
        }

        var outputExtension = Path.GetExtension(options.OutputPath);
        var expectedExtension = options.OutputMode == VideoOutputMode.AudioMp3 ? ".mp3" : ".mp4";
        if (!string.Equals(outputExtension, expectedExtension, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"La salida debe usar extensión {expectedExtension}.", nameof(options.OutputPath));
        }

        var outputDirectory = Path.GetDirectoryName(options.OutputPath);
        if (string.IsNullOrWhiteSpace(outputDirectory) || !Directory.Exists(outputDirectory))
        {
            throw new DirectoryNotFoundException("La carpeta de salida no existe.");
        }
    }

    private static void ValidateImageOptions(ImageConversionOptions options)
    {
        if (!Path.IsPathFullyQualified(options.InputPath) || !Path.IsPathFullyQualified(options.OutputPath))
        {
            throw new ArgumentException("Las rutas de imagen deben ser absolutas.");
        }

        if (!File.Exists(options.InputPath))
        {
            throw new FileNotFoundException("No se encontró la imagen de entrada.", options.InputPath);
        }

        if (string.Equals(Path.GetFullPath(options.InputPath), Path.GetFullPath(options.OutputPath), StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("La salida no puede sobrescribir la imagen original.");
        }

        if (options.TargetWidth is < 0 or > 16384 || options.TargetWidth is > 0 and < 16)
        {
            throw new ArgumentOutOfRangeException(nameof(options.TargetWidth), "El ancho debe ser 0 o estar entre 16 y 16384 px.");
        }

        if (options.CropEnabled &&
            (options.CropX < 0 || options.CropY < 0 || options.CropWidth < 1 || options.CropHeight < 1))
        {
            throw new ArgumentOutOfRangeException(nameof(options.CropWidth), "El recorte necesita coordenadas no negativas y dimensiones positivas.");
        }

        if (options.Brightness is < -1 or > 1 || options.Contrast is < 0 or > 2 || options.Saturation is < 0 or > 3)
        {
            throw new ArgumentOutOfRangeException(nameof(options.Brightness), "Brillo, contraste o saturación están fuera del rango permitido.");
        }

        if (!double.IsFinite(options.Temperature) || Math.Abs(options.Temperature) > 1)
            throw new ArgumentOutOfRangeException(nameof(options.Temperature));
        if (options.Quality is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(options.Quality), "La calidad debe estar entre 1 y 100.");
        }

        var expectedExtension = options.Format switch
        {
            ImageOutputFormat.Png => ".png",
            ImageOutputFormat.Jpeg => ".jpg",
            ImageOutputFormat.WebP => ".webp",
            _ => throw new ArgumentOutOfRangeException(nameof(options.Format))
        };
        if (!string.Equals(Path.GetExtension(options.OutputPath), expectedExtension, StringComparison.OrdinalIgnoreCase) &&
            !(options.Format == ImageOutputFormat.Jpeg && string.Equals(Path.GetExtension(options.OutputPath), ".jpeg", StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException($"La extensión de salida debe ser {expectedExtension}.", nameof(options.OutputPath));
        }

        var directory = Path.GetDirectoryName(options.OutputPath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException("La carpeta de salida no existe.");
        }
    }

    private static int MapJpegQuality(int quality) => Math.Clamp(31 - (int)Math.Round(quality * 29d / 100d), 2, 31);

    private static string FormatSeconds(double seconds) => seconds.ToString("0.###", CultureInfo.InvariantCulture);
}
