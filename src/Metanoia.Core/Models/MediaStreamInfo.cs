namespace Metanoia.Core.Models;

public sealed record MediaStreamInfo
{
    public int Index { get; init; }

    public MediaType Type { get; init; }

    public string? CodecName { get; init; }

    public string? CodecLongName { get; init; }

    public int? Width { get; init; }

    public int? Height { get; init; }

    public double? FramesPerSecond { get; init; }

    public int? SampleRate { get; init; }

    public int? Channels { get; init; }

    public long? BitRate { get; init; }
}
