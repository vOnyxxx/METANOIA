namespace Metanoia.Core.Models;

public enum VideoResolutionMode
{
    Original,
    Hd720p,
    FullHd1080p
}

public enum VideoOutputMode
{
    Mp4,
    AudioMp3
}

public sealed record VideoConversionOptions
{
    public required string InputPath { get; init; }

    public required string OutputPath { get; init; }
    public string ExtraFilter { get; init; } = "";

    public VideoResolutionMode Resolution { get; init; } = VideoResolutionMode.Original;

    public VideoOutputMode OutputMode { get; init; } = VideoOutputMode.Mp4;

    public double TrimStartSeconds { get; init; }

    public double TrimDurationSeconds { get; init; }

    public int Crf { get; init; } = 23;

    public string Preset { get; init; } = "medium";
}
