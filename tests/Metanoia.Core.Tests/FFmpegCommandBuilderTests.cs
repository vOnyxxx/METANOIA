using Metanoia.Core.Models;
using Metanoia.Core.Services;

namespace Metanoia.Core.Tests;

public sealed class FFmpegCommandBuilderTests
{
    [Fact]
    public void BuildVideoConversion_UsesDeterministicSafeArgumentList()
    {
        var fixture = CreateEmptyFixture("input video.mp4");
        var output = Path.Combine(Path.GetDirectoryName(fixture)!, "output video.mp4");
        var builder = new FFmpegCommandBuilder("ffmpeg.exe");

        var command = builder.BuildVideoConversion(
            CreateProbeResult(fixture),
            new VideoConversionOptions
            {
                InputPath = fixture,
                OutputPath = output,
                Resolution = VideoResolutionMode.FullHd1080p,
                Crf = 20,
                Preset = "slow"
            });

        Assert.Equal("ffmpeg.exe", command.Executable);
        Assert.Contains("scale=-2:1080", command.Arguments);
        Assert.Contains("libx264", command.Arguments);
        Assert.Contains("pipe:1", command.Arguments);
        Assert.Equal(output, command.Arguments[^1]);
        Assert.DoesNotContain("cmd.exe", command.ToDisplayString(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(52)]
    public void BuildVideoConversion_RejectsCrfOutsideFfmpegRange(int crf)
    {
        var fixture = CreateEmptyFixture("crf-input.mp4");
        var builder = new FFmpegCommandBuilder("ffmpeg.exe");

        Assert.Throws<ArgumentOutOfRangeException>(() => builder.BuildVideoConversion(
            CreateProbeResult(fixture),
            new VideoConversionOptions
            {
                InputPath = fixture,
                OutputPath = Path.Combine(Path.GetDirectoryName(fixture)!, "crf-output.mp4"),
                Crf = crf,
                Preset = "medium"
            }));
    }

    [Fact]
    public void BuildVideoConversion_RejectsUnknownPreset()
    {
        var fixture = CreateEmptyFixture("preset-input.mp4");
        var builder = new FFmpegCommandBuilder("ffmpeg.exe");

        Assert.Throws<ArgumentException>(() => builder.BuildVideoConversion(
            CreateProbeResult(fixture),
            new VideoConversionOptions
            {
                InputPath = fixture,
                OutputPath = Path.Combine(Path.GetDirectoryName(fixture)!, "preset-output.mp4"),
                Preset = "medium && calc"
            }));
    }

    [Fact]
    public void BuildVideoConversion_AddsTrimAnd720pWithInvariantArguments()
    {
        var fixture = CreateEmptyFixture("trim-input.mp4");
        var output = Path.Combine(Path.GetDirectoryName(fixture)!, "trim-output.mp4");
        var command = new FFmpegCommandBuilder("ffmpeg.exe").BuildVideoConversion(
            CreateProbeResult(fixture),
            new VideoConversionOptions
            {
                InputPath = fixture,
                OutputPath = output,
                Resolution = VideoResolutionMode.Hd720p,
                TrimStartSeconds = 0.25,
                TrimDurationSeconds = 1.5
            });

        Assert.Contains("0.25", command.Arguments);
        Assert.Contains("1.5", command.Arguments);
        Assert.Contains("scale=-2:720", command.Arguments);
    }

    [Fact]
    public void BuildVideoConversion_ExtractsMp3WithoutVideoMapping()
    {
        var fixture = CreateEmptyFixture("audio-input.mp4");
        var output = Path.Combine(Path.GetDirectoryName(fixture)!, "audio-output.mp3");
        var command = new FFmpegCommandBuilder("ffmpeg.exe").BuildVideoConversion(
            CreateProbeResult(fixture),
            new VideoConversionOptions
            {
                InputPath = fixture,
                OutputPath = output,
                OutputMode = VideoOutputMode.AudioMp3
            });

        Assert.Contains("libmp3lame", command.Arguments);
        Assert.Contains("-vn", command.Arguments);
        Assert.DoesNotContain("libx264", command.Arguments);
        Assert.Equal(output, command.Arguments[^1]);
    }

    [Fact]
    public void BuildVideoConversion_Mp3IgnoresVideoOnlyCrfAndPreset()
    {
        var fixture = CreateEmptyFixture("audio-video-options.mp4");
        var output = Path.Combine(Path.GetDirectoryName(fixture)!, "audio-video-options.mp3");

        var command = new FFmpegCommandBuilder("ffmpeg.exe").BuildVideoConversion(
            CreateProbeResult(fixture),
            new VideoConversionOptions
            {
                InputPath = fixture,
                OutputPath = output,
                OutputMode = VideoOutputMode.AudioMp3,
                Crf = -1,
                Preset = "not-a-video-preset"
            });

        Assert.Contains("libmp3lame", command.Arguments);
        Assert.DoesNotContain("not-a-video-preset", command.Arguments);
    }

    [Theory]
    [InlineData(-0.1, 0)]
    [InlineData(0, -1)]
    public void BuildVideoConversion_RejectsNegativeTrim(double start, double duration)
    {
        var fixture = CreateEmptyFixture("negative-trim.mp4");
        Assert.Throws<ArgumentOutOfRangeException>(() => new FFmpegCommandBuilder("ffmpeg.exe").BuildVideoConversion(
            CreateProbeResult(fixture),
            new VideoConversionOptions
            {
                InputPath = fixture,
                OutputPath = Path.Combine(Path.GetDirectoryName(fixture)!, "negative-trim-output.mp4"),
                TrimStartSeconds = start,
                TrimDurationSeconds = duration
            }));
    }

    [Fact]
    public void BuildVideoConversion_RejectsTrimStartOutsideSourceDuration()
    {
        var fixture = CreateEmptyFixture("trim-outside-source.mp4");

        Assert.Throws<ArgumentOutOfRangeException>(() => new FFmpegCommandBuilder("ffmpeg.exe").BuildVideoConversion(
            CreateProbeResult(fixture),
            new VideoConversionOptions
            {
                InputPath = fixture,
                OutputPath = Path.Combine(Path.GetDirectoryName(fixture)!, "trim-outside-output.mp4"),
                TrimStartSeconds = 2
            }));
    }

    [Fact]
    public void BuildVideoConversion_RejectsMp3WhenSourceHasNoAudioStream()
    {
        var fixture = CreateEmptyFixture("video-without-audio.mp4");
        var source = CreateProbeResult(fixture) with
        {
            Streams = CreateProbeResult(fixture).Streams
                .Where(stream => stream.Type != MediaType.Audio)
                .ToArray()
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new FFmpegCommandBuilder("ffmpeg.exe").BuildVideoConversion(
                source,
                new VideoConversionOptions
                {
                    InputPath = fixture,
                    OutputPath = Path.Combine(Path.GetDirectoryName(fixture)!, "missing-audio.mp3"),
                    OutputMode = VideoOutputMode.AudioMp3
                }));

        Assert.Contains("pista de audio", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildImageConversion_CreatesDeterministicFilterChainAndWebPOutput()
    {
        var fixture = CreateEmptyFixture("source-image.png");
        var output = Path.Combine(Path.GetDirectoryName(fixture)!, "image-output.webp");
        var builder = new FFmpegCommandBuilder("ffmpeg.exe");

        var command = builder.BuildImageConversion(
            CreateImageProbeResult(fixture),
            new ImageConversionOptions
            {
                InputPath = fixture,
                OutputPath = output,
                Format = ImageOutputFormat.WebP,
                TargetWidth = 1280,
                CropEnabled = true,
                CropX = 10,
                CropY = 20,
                CropWidth = 640,
                CropHeight = 480,
                Brightness = 0.1,
                Contrast = 1.2,
                Saturation = 0.8,
                Quality = 88
            });

        Assert.Contains("crop=640:480:10:20,scale=1280:-2,eq=brightness=0.1:contrast=1.2:saturation=0.8", command.Arguments);
        Assert.Contains("libwebp", command.Arguments);
        Assert.Contains("88", command.Arguments);
        Assert.Equal(output, command.Arguments[^1]);
    }

    [Theory]
    [InlineData(-1.1, 1, 1)]
    [InlineData(0, 2.1, 1)]
    [InlineData(0, 1, 3.1)]
    public void BuildImageConversion_RejectsColorValuesOutsideAllowedRange(double brightness, double contrast, double saturation)
    {
        var fixture = CreateEmptyFixture("invalid-color.png");
        var builder = new FFmpegCommandBuilder("ffmpeg.exe");

        Assert.Throws<ArgumentOutOfRangeException>(() => builder.BuildImageConversion(
            CreateImageProbeResult(fixture),
            new ImageConversionOptions
            {
                InputPath = fixture,
                OutputPath = Path.Combine(Path.GetDirectoryName(fixture)!, "invalid-color-output.png"),
                Brightness = brightness,
                Contrast = contrast,
                Saturation = saturation
            }));
    }

    private static MediaProbeResult CreateProbeResult(string inputPath) => new()
    {
        File = new MediaFile(inputPath, Path.GetFileName(inputPath), 0, DateTimeOffset.UtcNow),
        MediaType = MediaType.Video,
        Duration = TimeSpan.FromSeconds(2),
        Streams =
        [
            new MediaStreamInfo
            {
                Index = 0,
                Type = MediaType.Video,
                CodecName = "h264",
                Width = 640,
                Height = 360,
                FramesPerSecond = 30
            },
            new MediaStreamInfo
            {
                Index = 1,
                Type = MediaType.Audio,
                CodecName = "aac",
                SampleRate = 48000,
                Channels = 1
            }
        ]
    };

    private static MediaProbeResult CreateImageProbeResult(string inputPath) => new()
    {
        File = new MediaFile(inputPath, Path.GetFileName(inputPath), 0, DateTimeOffset.UtcNow),
        MediaType = MediaType.Image,
        Streams =
        [
            new MediaStreamInfo
            {
                Index = 0,
                Type = MediaType.Video,
                CodecName = "png",
                Width = 1920,
                Height = 1080
            }
        ]
    };

    private static string CreateEmptyFixture(string fileName)
    {
        var directory = Path.Combine(Path.GetTempPath(), "METANOIA", "tests", "core-command-builder");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        File.WriteAllBytes(path, []);
        return path;
    }
}
