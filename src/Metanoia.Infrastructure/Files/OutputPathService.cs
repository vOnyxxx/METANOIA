using Metanoia.Core.Contracts;
using Metanoia.Core.Models;

namespace Metanoia.Infrastructure.Files;

public sealed class OutputPathService : IOutputPathService
{
    public string SuggestVideoOutputPath(string inputPath, VideoOutputMode mode = VideoOutputMode.Mp4)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        var fullInputPath = Path.GetFullPath(inputPath);
        var directory = Path.GetDirectoryName(fullInputPath)
            ?? throw new ArgumentException("La ruta de entrada no tiene una carpeta válida.", nameof(inputPath));
        var name = Path.GetFileNameWithoutExtension(fullInputPath);

        var suffix = mode == VideoOutputMode.AudioMp3 ? "_audio_metanoia.mp3" : "_metanoia.mp4";
        return Path.Combine(directory, $"{name}{suffix}");
    }

    public string SuggestImageOutputPath(string inputPath, ImageOutputFormat format)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        var fullInputPath = Path.GetFullPath(inputPath);
        var directory = Path.GetDirectoryName(fullInputPath)
            ?? throw new ArgumentException("La ruta de entrada no tiene una carpeta válida.", nameof(inputPath));
        var extension = format switch
        {
            ImageOutputFormat.Png => ".png",
            ImageOutputFormat.Jpeg => ".jpg",
            ImageOutputFormat.WebP => ".webp",
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
        return Path.Combine(directory, $"{Path.GetFileNameWithoutExtension(fullInputPath)}_metanoia{extension}");
    }

    public string SuggestAudioOutputPath(string inputPath, AudioOutputFormat format)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        var fullInputPath = Path.GetFullPath(inputPath);
        var directory = Path.GetDirectoryName(fullInputPath)
            ?? throw new ArgumentException("La ruta de entrada no tiene una carpeta válida.", nameof(inputPath));
        var extension = GetAudioExtension(format);
        return Path.Combine(directory, $"{Path.GetFileNameWithoutExtension(fullInputPath)}_metanoia{extension}");
    }

    public void ValidateVideoOutputPath(string inputPath, string outputPath, VideoOutputMode mode = VideoOutputMode.Mp4)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var fullInputPath = Path.GetFullPath(inputPath);
        var fullOutputPath = Path.GetFullPath(outputPath);
        if (string.Equals(fullInputPath, fullOutputPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("La salida no puede sobrescribir el archivo de entrada.", nameof(outputPath));
        }

        var expectedExtension = mode == VideoOutputMode.AudioMp3 ? ".mp3" : ".mp4";
        if (!string.Equals(Path.GetExtension(fullOutputPath), expectedExtension, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"La salida debe tener extensión {expectedExtension}.", nameof(outputPath));
        }

        var directory = Path.GetDirectoryName(fullOutputPath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException("La carpeta de salida no existe.");
        }
    }

    public void ValidateImageOutputPath(string inputPath, string outputPath, ImageOutputFormat format)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        var fullInputPath = Path.GetFullPath(inputPath);
        var fullOutputPath = Path.GetFullPath(outputPath);
        if (string.Equals(fullInputPath, fullOutputPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("La salida no puede sobrescribir la imagen original.", nameof(outputPath));
        }

        var validExtensions = format == ImageOutputFormat.Jpeg ? new[] { ".jpg", ".jpeg" } :
            new[] { format == ImageOutputFormat.Png ? ".png" : ".webp" };
        if (!validExtensions.Contains(Path.GetExtension(fullOutputPath), StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException("La extensión no coincide con el formato seleccionado.", nameof(outputPath));
        }

        var directory = Path.GetDirectoryName(fullOutputPath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException("La carpeta de salida no existe.");
        }
    }

    public void ValidateAudioOutputPath(string inputPath, string outputPath, AudioOutputFormat format)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        var fullInputPath = Path.GetFullPath(inputPath);
        var fullOutputPath = Path.GetFullPath(outputPath);
        if (string.Equals(fullInputPath, fullOutputPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("La salida no puede sobrescribir el audio original.", nameof(outputPath));
        }
        var expectedExtension = GetAudioExtension(format);
        if (!string.Equals(Path.GetExtension(fullOutputPath), expectedExtension, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"La salida debe tener extensión {expectedExtension}.", nameof(outputPath));
        }
        var directory = Path.GetDirectoryName(fullOutputPath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException("La carpeta de salida no existe.");
        }
    }

    private static string GetAudioExtension(AudioOutputFormat format) => format switch
    {
        AudioOutputFormat.Wav => ".wav",
        AudioOutputFormat.Mp3 => ".mp3",
        AudioOutputFormat.Aac => ".m4a",
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };
}
