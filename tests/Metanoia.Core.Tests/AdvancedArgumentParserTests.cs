using Metanoia.Core.Models;
using Metanoia.Core.Services;

namespace Metanoia.Core.Tests;

public sealed class AdvancedArgumentParserTests
{
    private readonly AdvancedArgumentParser _parser = new();

    [Fact]
    public void Parse_AcceptsAllowlistedOptionsAndQuotedValues()
    {
        var arguments = _parser.Parse("-movflags +faststart -metadata \"title=Mi proyecto\" -pix_fmt yuv420p");

        Assert.Equal(
            ["-movflags", "+faststart", "-metadata", "title=Mi proyecto", "-pix_fmt", "yuv420p"],
            arguments);
    }

    [Theory]
    [InlineData("-i otro.mp4")]
    [InlineData("-c:v libx265")]
    [InlineData("-metadata title=x && calc.exe")]
    [InlineData("ffmpeg.exe -shortest")]
    [InlineData("-metadata \"title=sin cierre")]
    public void Parse_RejectsUnsafeOrUnapprovedInput(string input)
    {
        Assert.Throws<ArgumentException>(() => _parser.Parse(input));
    }

    [Fact]
    public void Apply_InsertsArgumentsBeforeProtectedOutput()
    {
        var command = new FFmpegCommand("ffmpeg.exe", ["-y", "-i", "input.mp4", "output.mp4"]);

        var updated = _parser.Apply(command, "-movflags +faststart");

        Assert.Equal(
            ["-y", "-i", "input.mp4", "-movflags", "+faststart", "output.mp4"],
            updated.Arguments);
    }
}
