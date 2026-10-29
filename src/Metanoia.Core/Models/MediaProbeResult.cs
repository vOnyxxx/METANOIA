namespace Metanoia.Core.Models;

public sealed record MediaProbeResult
{
    public required MediaFile File { get; init; }

    public MediaType MediaType { get; init; }

    public string? FormatName { get; init; }

    public string? FormatLongName { get; init; }

    public TimeSpan? Duration { get; init; }

    public long? BitRate { get; init; }

    public IReadOnlyList<MediaStreamInfo> Streams { get; init; } = [];

    public MediaStreamInfo? PrimaryVideoStream =>
        Streams.FirstOrDefault(stream => stream.Type == MediaType.Video);

    public MediaStreamInfo? PrimaryAudioStream =>
        Streams.FirstOrDefault(stream => stream.Type == MediaType.Audio);
}
