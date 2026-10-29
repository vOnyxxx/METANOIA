using Metanoia.Core.Contracts;

namespace Metanoia.Infrastructure.FFmpeg;

public sealed class FFToolsSettingsService : ISettingsService
{
    public FFToolsSettingsService()
    {
        FFmpegPath = FindExecutable("ffmpeg.exe");
        FFprobePath = FindExecutable("ffprobe.exe");
    }

    public string? FFmpegPath { get; }

    public string? FFprobePath { get; }

    public bool AreToolsAvailable =>
        !string.IsNullOrWhiteSpace(FFmpegPath) &&
        !string.IsNullOrWhiteSpace(FFprobePath);

    private static string? FindExecutable(string fileName)
    {
        var pathValue = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathValue))
        {
            return null;
        }

        foreach (var directory in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(directory.Trim().Trim('"'), fileName);
                if (File.Exists(candidate))
                {
                    return Path.GetFullPath(candidate);
                }
            }
            catch (Exception exception) when (
                exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                // Una entrada PATH inválida no debe impedir revisar las demás.
            }
        }

        return null;
    }
}
