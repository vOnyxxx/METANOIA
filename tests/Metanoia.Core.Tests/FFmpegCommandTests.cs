using Metanoia.Core.Models;

namespace Metanoia.Core.Tests;

public sealed class FFmpegCommandTests
{
    [Fact]
    public void ToDisplayString_QuotesPathsWithoutChangingArgumentList()
    {
        var command = new FFmpegCommand(
            @"C:\Program Files\FFmpeg\ffmpeg.exe",
            ["-i", @"C:\Media files\input.mp4", @"C:\Output files\result.mp4"]);

        var display = command.ToDisplayString();

        Assert.Contains("\"C:\\Program Files\\FFmpeg\\ffmpeg.exe\"", display);
        Assert.Contains("\"C:\\Media files\\input.mp4\"", display);
        Assert.Equal(@"C:\Media files\input.mp4", command.Arguments[1]);
    }
}
