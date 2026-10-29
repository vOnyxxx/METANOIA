using Metanoia.Core.Models;

namespace Metanoia.Core.Contracts;

public interface IFFmpegCommandBuilder
{
    FFmpegCommand BuildVideoConversion(MediaProbeResult source, VideoConversionOptions options);

    FFmpegCommand BuildImageConversion(MediaProbeResult source, ImageConversionOptions options);

    FFmpegCommand BuildAudioLoudnessAnalysis(MediaProbeResult source, AudioConversionOptions options);

    FFmpegCommand BuildAudioConversion(MediaProbeResult source, AudioConversionOptions options);

    FFmpegCommand BuildAudioVisualization(
        MediaProbeResult source,
        string outputPath,
        AudioVisualizationKind kind,
        double trimStartSeconds = 0,
        double trimDurationSeconds = 0);
}
