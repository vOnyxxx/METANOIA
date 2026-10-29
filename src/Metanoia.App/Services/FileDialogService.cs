using System.IO;
using Microsoft.Win32;
using Metanoia.Core.Models;

namespace Metanoia.App.Services;

public sealed class FileDialogService : IFileDialogService
{
    public string? SelectInputMedia()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Seleccionar archivo multimedia para METANOIA",
            Filter = "Archivos multimedia|*.mp4;*.mov;*.mkv;*.avi;*.webm;*.m4v;*.jpg;*.jpeg;*.png;*.webp;*.bmp;*.tif;*.tiff;*.wav;*.mp3;*.aac;*.m4a;*.flac;*.ogg|Video|*.mp4;*.mov;*.mkv;*.avi;*.webm;*.m4v|Imagen|*.jpg;*.jpeg;*.png;*.webp;*.bmp;*.tif;*.tiff|Audio|*.wav;*.mp3;*.aac;*.m4a;*.flac;*.ogg|Todos los archivos|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? SelectOutputVideo(string suggestedPath, VideoOutputMode mode)
    {
        var extractAudio = mode == VideoOutputMode.AudioMp3;
        var dialog = new SaveFileDialog
        {
            Title = extractAudio ? "Extraer audio con METANOIA" : "Guardar video convertido",
            Filter = extractAudio ? "Audio MP3|*.mp3" : "Video MP4|*.mp4",
            DefaultExt = extractAudio ? ".mp3" : ".mp4",
            AddExtension = true,
            FileName = Path.GetFileName(suggestedPath),
            InitialDirectory = Path.GetDirectoryName(suggestedPath)
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? SelectOutputImage(string suggestedPath, string format)
    {
        var normalized = format.ToUpperInvariant();
        var (filter, extension) = normalized switch
        {
            "JPG" or "JPEG" => ("Imagen JPEG|*.jpg;*.jpeg", ".jpg"),
            "WEBP" => ("Imagen WebP|*.webp", ".webp"),
            _ => ("Imagen PNG|*.png", ".png")
        };
        var dialog = new SaveFileDialog
        {
            Title = "Exportar imagen con METANOIA",
            Filter = filter,
            DefaultExt = extension,
            AddExtension = true,
            FileName = Path.GetFileName(suggestedPath),
            InitialDirectory = Path.GetDirectoryName(suggestedPath)
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? SelectOutputAudio(string suggestedPath, AudioOutputFormat format)
    {
        var (filter, extension, title) = format switch
        {
            AudioOutputFormat.Wav => ("Audio WAV 24-bit|*.wav", ".wav", "Exportar WAV con METANOIA"),
            AudioOutputFormat.Mp3 => ("Audio MP3 192 kbps|*.mp3", ".mp3", "Exportar MP3 con METANOIA"),
            AudioOutputFormat.Aac => ("Audio AAC en contenedor M4A|*.m4a", ".m4a", "Exportar AAC con METANOIA"),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
        var dialog = new SaveFileDialog
        {
            Title = title,
            Filter = filter,
            DefaultExt = extension,
            AddExtension = true,
            FileName = Path.GetFileName(suggestedPath),
            InitialDirectory = Path.GetDirectoryName(suggestedPath)
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
