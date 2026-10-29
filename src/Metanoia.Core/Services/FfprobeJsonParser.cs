using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Metanoia.Core.Models;

namespace Metanoia.Core.Services;

public sealed class FfprobeJsonParser
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".tif", ".tiff"
    };
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public MediaProbeResult Parse(string json, FileInfo file)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        ArgumentNullException.ThrowIfNull(file);

        var document = JsonSerializer.Deserialize<FfprobeDocument>(json, SerializerOptions)
            ?? throw new FormatException("FFprobe devolvió un documento JSON vacío.");

        var streams = (document.Streams ?? [])
            .Select(MapStream)
            .ToArray();

        return new MediaProbeResult
        {
            File = new MediaFile(
                file.FullName,
                file.Name,
                file.Exists ? file.Length : ParseLong(document.Format?.Size) ?? 0,
                file.Exists ? file.LastWriteTimeUtc : DateTimeOffset.MinValue),
            MediaType = DetectMediaType(streams, file.Extension),
            FormatName = document.Format?.FormatName,
            FormatLongName = document.Format?.FormatLongName,
            Duration = ParseDuration(document.Format?.Duration),
            BitRate = ParseLong(document.Format?.BitRate),
            Streams = streams
        };
    }

    private static MediaStreamInfo MapStream(FfprobeStream stream)
    {
        return new MediaStreamInfo
        {
            Index = stream.Index,
            Type = stream.CodecType?.ToLowerInvariant() switch
            {
                "video" when stream.AverageFrameRate is null && stream.Width is null => MediaType.Image,
                "video" => MediaType.Video,
                "audio" => MediaType.Audio,
                _ => MediaType.Unknown
            },
            CodecName = stream.CodecName,
            CodecLongName = stream.CodecLongName,
            Width = stream.Width,
            Height = stream.Height,
            FramesPerSecond = ParseRational(stream.AverageFrameRate ?? stream.RealFrameRate),
            SampleRate = ParseInt(stream.SampleRate),
            Channels = stream.Channels,
            BitRate = ParseLong(stream.BitRate)
        };
    }

    private static MediaType DetectMediaType(IReadOnlyCollection<MediaStreamInfo> streams, string extension)
    {
        if (ImageExtensions.Contains(extension) && streams.Any(stream => stream.Type is MediaType.Video or MediaType.Image))
        {
            return MediaType.Image;
        }

        if (streams.Any(stream => stream.Type == MediaType.Video))
        {
            return MediaType.Video;
        }

        if (streams.Any(stream => stream.Type == MediaType.Audio))
        {
            return MediaType.Audio;
        }

        if (streams.Any(stream => stream.Type == MediaType.Image))
        {
            return MediaType.Image;
        }

        return MediaType.Unknown;
    }

    private static double? ParseRational(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var parts = value.Split('/', StringSplitOptions.TrimEntries);
        if (parts.Length == 2 &&
            double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var numerator) &&
            double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var denominator) &&
            denominator != 0)
        {
            return numerator / denominator;
        }

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            ? number
            : null;
    }

    private static TimeSpan? ParseDuration(string? value)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
            ? TimeSpan.FromSeconds(seconds)
            : null;
    }

    private static int? ParseInt(string? value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;
    }

    private static long? ParseLong(string? value)
    {
        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;
    }

    private sealed record FfprobeDocument(
        [property: JsonPropertyName("streams")] IReadOnlyList<FfprobeStream>? Streams,
        [property: JsonPropertyName("format")] FfprobeFormat? Format);

    private sealed record FfprobeStream(
        [property: JsonPropertyName("index")] int Index,
        [property: JsonPropertyName("codec_name")] string? CodecName,
        [property: JsonPropertyName("codec_long_name")] string? CodecLongName,
        [property: JsonPropertyName("codec_type")] string? CodecType,
        [property: JsonPropertyName("width")] int? Width,
        [property: JsonPropertyName("height")] int? Height,
        [property: JsonPropertyName("avg_frame_rate")] string? AverageFrameRate,
        [property: JsonPropertyName("r_frame_rate")] string? RealFrameRate,
        [property: JsonPropertyName("sample_rate")] string? SampleRate,
        [property: JsonPropertyName("channels")] int? Channels,
        [property: JsonPropertyName("bit_rate")] string? BitRate);

    private sealed record FfprobeFormat(
        [property: JsonPropertyName("format_name")] string? FormatName,
        [property: JsonPropertyName("format_long_name")] string? FormatLongName,
        [property: JsonPropertyName("duration")] string? Duration,
        [property: JsonPropertyName("size")] string? Size,
        [property: JsonPropertyName("bit_rate")] string? BitRate);
}
