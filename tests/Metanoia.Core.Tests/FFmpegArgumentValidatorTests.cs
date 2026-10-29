using Metanoia.Core.Services;

namespace Metanoia.Core.Tests;

public sealed class FFmpegArgumentValidatorTests
{
    [Theory]
    [InlineData("|")]
    [InlineData("input.mp4 && calc.exe")]
    [InlineData(">")]
    [InlineData("output.mp4\nwhoami")]
    public void ValidateArguments_RejectsShellSyntax(string argument)
    {
        Assert.Throws<ArgumentException>(() =>
            FFmpegArgumentValidator.ValidateArguments(["-i", argument]));
    }

    [Fact]
    public void ValidateArguments_AllowsRequiredProgressPipeAsLiteralArgument()
    {
        FFmpegArgumentValidator.ValidateArguments(["-progress", "pipe:1", "-nostats"]);
    }

    [Fact]
    public void ValidateExecutable_RejectsAlternativeExecutable()
    {
        Assert.Throws<ArgumentException>(() => FFmpegArgumentValidator.ValidateExecutable("cmd.exe"));
    }
}
