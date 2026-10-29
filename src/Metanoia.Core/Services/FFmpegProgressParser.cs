using System.Globalization;
using Metanoia.Core.Models;

namespace Metanoia.Core.Services;

public sealed class FFmpegProgressParser
{
    public JobProgress Parse(IReadOnlyDictionary<string, string> values, TimeSpan? expectedDuration)
    {
        ArgumentNullException.ThrowIfNull(values);

        var processedDuration = ParseProcessedDuration(values);
        var percent = CalculatePercent(processedDuration, expectedDuration);
        long? frame = values.TryGetValue("frame", out var frameValue) &&
                      long.TryParse(frameValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var frameNumber)
            ? frameNumber
            : null;

        values.TryGetValue("speed", out var speed);
        values.TryGetValue("progress", out var phase);

        return new JobProgress(percent, processedDuration, frame, speed, phase ?? "continue");
    }

    private static TimeSpan ParseProcessedDuration(IReadOnlyDictionary<string, string> values)
    {
        if (TryParseMicroseconds(values, "out_time_us", out var time) ||
            TryParseMicroseconds(values, "out_time_ms", out time))
        {
            return time;
        }

        if (values.TryGetValue("out_time", out var formatted) &&
            TimeSpan.TryParse(formatted, CultureInfo.InvariantCulture, out time))
        {
            return time;
        }

        return TimeSpan.Zero;
    }

    private static bool TryParseMicroseconds(
        IReadOnlyDictionary<string, string> values,
        string key,
        out TimeSpan result)
    {
        if (values.TryGetValue(key, out var raw) &&
            long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var microseconds))
        {
            result = TimeSpan.FromTicks(microseconds * 10);
            return true;
        }

        result = TimeSpan.Zero;
        return false;
    }

    private static double? CalculatePercent(TimeSpan processed, TimeSpan? expected)
    {
        if (expected is null || expected <= TimeSpan.Zero)
        {
            return null;
        }

        return Math.Clamp(processed.TotalMilliseconds / expected.Value.TotalMilliseconds * 100d, 0d, 100d);
    }
}
