using Metanoia.Core.Models;
using Metanoia.Core.Services;

namespace Metanoia.Core.Tests;

public sealed class FfprobeJsonParserTests
{
    [Fact]
    public void Parse_NormalizesVideoAudioAndFormatProperties()
    {
        const string json = """
        {
          "streams": [
            {
              "index": 0,
              "codec_name": "h264",
              "codec_long_name": "H.264",
              "codec_type": "video",
              "width": 1920,
              "height": 1080,
              "avg_frame_rate": "30000/1001",
              "bit_rate": "4000000"
            },
            {
              "index": 1,
              "codec_name": "aac",
              "codec_type": "audio",
              "sample_rate": "48000",
              "channels": 2,
              "bit_rate": "192000"
            }
          ],
          "format": {
            "format_name": "mov,mp4,m4a,3gp,3g2,mj2",
            "duration": "10.500000",
            "size": "123456",
            "bit_rate": "4192000"
          }
        }
        """;
        var file = new FileInfo(Path.Combine(TestPaths.Fixtures, "parser-does-not-need-existing-file.mp4"));

        var result = new FfprobeJsonParser().Parse(json, file);

        Assert.Equal(MediaType.Video, result.MediaType);
        Assert.Equal(TimeSpan.FromSeconds(10.5), result.Duration);
        Assert.Equal(1920, result.PrimaryVideoStream?.Width);
        Assert.NotNull(result.PrimaryVideoStream?.FramesPerSecond);
        Assert.Equal(30000d / 1001d, result.PrimaryVideoStream!.FramesPerSecond!.Value, 6);
        Assert.Equal(48000, result.PrimaryAudioStream?.SampleRate);
        Assert.Equal(2, result.PrimaryAudioStream?.Channels);
    }

    [Fact]
    public void Parse_ToleratesPartialFfprobeResponse()
    {
        const string json = """{ "streams": [{ "index": 0, "codec_type": "audio" }], "format": {} }""";
        var file = new FileInfo(Path.Combine(TestPaths.Fixtures, "partial.wav"));

        var result = new FfprobeJsonParser().Parse(json, file);

        Assert.Equal(MediaType.Audio, result.MediaType);
        Assert.Null(result.Duration);
        Assert.Null(result.PrimaryAudioStream?.SampleRate);
    }

    [Fact]
    public void Parse_DetectsStillImageFromExtensionEvenWhenFfprobeReportsVideoStream()
    {
        const string json = """
        { "streams": [{ "index": 0, "codec_type": "video", "codec_name": "png", "width": 800, "height": 600, "avg_frame_rate": "25/1" }], "format": { "format_name": "png_pipe" } }
        """;
        var file = new FileInfo(Path.Combine(TestPaths.Fixtures, "still-image.png"));

        var result = new FfprobeJsonParser().Parse(json, file);

        Assert.Equal(MediaType.Image, result.MediaType);
        Assert.Equal(800, result.PrimaryVideoStream?.Width);
        Assert.Equal(600, result.PrimaryVideoStream?.Height);
    }
}
