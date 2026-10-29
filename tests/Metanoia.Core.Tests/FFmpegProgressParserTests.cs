using Metanoia.Core.Services;

namespace Metanoia.Core.Tests;

public sealed class FFmpegProgressParserTests
{
    [Fact]
    public void Parse_ConvertsMicrosecondsAndCalculatesClampedPercentage()
    {
        var values = new Dictionary<string, string>
        {
            ["frame"] = "75",
            ["out_time_us"] = "2500000",
            ["speed"] = "1.25x",
            ["progress"] = "continue"
        };

        var result = new FFmpegProgressParser().Parse(values, TimeSpan.FromSeconds(10));

        Assert.Equal(TimeSpan.FromSeconds(2.5), result.ProcessedDuration);
        Assert.Equal(25, result.Percent);
        Assert.Equal(75, result.Frame);
        Assert.Equal("1.25x", result.Speed);
    }

    [Fact]
    public void Parse_ReturnsUnknownPercentageWhenDurationIsMissing()
    {
        var result = new FFmpegProgressParser().Parse(
            new Dictionary<string, string> { ["out_time"] = "00:00:01.000000" },
            null);

        Assert.Null(result.Percent);
        Assert.Equal(TimeSpan.FromSeconds(1), result.ProcessedDuration);
    }
}
