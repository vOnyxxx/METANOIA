using System.Diagnostics;
using Metanoia.Core.Models;
using Metanoia.Core.Services;
using Metanoia.Infrastructure.FFmpeg;

namespace Metanoia.IntegrationTests;

[Collection(FFmpegCollection.Name)]
public sealed class VerticalSliceIntegrationTests
{
    private static readonly SemaphoreSlim FixtureLock = new(1, 1);

    [Fact]
    public async Task ProbeBuildRunAndProbeOutput_CompletesRealVerticalSlice()
    {
        var tools = new FFToolsSettingsService();
        Assert.NotNull(tools.FFmpegPath);
        Assert.NotNull(tools.FFprobePath);
        var fixture = await EnsureFixtureAsync(tools.FFmpegPath!);
        var output = Path.Combine(TestPaths.Results, "vertical-slice-output.mp4");
        Directory.CreateDirectory(TestPaths.Results);
        DeleteIfExists(output);

        var parser = new FfprobeJsonParser();
        var probeService = new MediaProbeService(tools.FFprobePath!, parser);
        var source = await probeService.ProbeAsync(fixture);

        Assert.Equal(MediaType.Video, source.MediaType);
        Assert.NotNull(source.PrimaryVideoStream);
        Assert.NotNull(source.PrimaryAudioStream);
        Assert.True(source.Duration > TimeSpan.Zero);

        var command = new FFmpegCommandBuilder(tools.FFmpegPath!).BuildVideoConversion(
            source,
            new VideoConversionOptions
            {
                InputPath = fixture,
                OutputPath = output,
                Crf = 28,
                Preset = "ultrafast",
                Resolution = VideoResolutionMode.Original
            });

        var progressUpdates = new List<JobProgress>();
        var progress = new InlineProgress<JobProgress>(value => progressUpdates.Add(value));
        var runner = new FFmpegRunner(new FFmpegProgressParser());
        var run = await runner.RunAsync(command, source.Duration, progress);

        Assert.True(run.Succeeded, run.StandardError);
        Assert.False(run.WasCancelled);
        Assert.True(File.Exists(output));
        Assert.True(new FileInfo(output).Length > 0);
        Assert.NotEmpty(progressUpdates);

        var converted = await probeService.ProbeAsync(output);
        Assert.Equal(MediaType.Video, converted.MediaType);
        Assert.Equal("h264", converted.PrimaryVideoStream?.CodecName);
        Assert.NotNull(converted.PrimaryAudioStream);
    }

    [Fact]
    public async Task Runner_CancelsRealFfmpegProcessAndWaitsForExit()
    {
        var tools = new FFToolsSettingsService();
        Assert.NotNull(tools.FFmpegPath);
        var fixture = await EnsureFixtureAsync(tools.FFmpegPath!);
        var output = Path.Combine(TestPaths.Results, "cancelled-output.mp4");
        Directory.CreateDirectory(TestPaths.Results);
        DeleteIfExists(output);

        var command = new FFmpegCommand(
            tools.FFmpegPath!,
            [
                "-y",
                "-re",
                "-stream_loop", "-1",
                "-i", fixture,
                "-t", "30",
                "-c:v", "libx264",
                "-preset", "ultrafast",
                "-c:a", "aac",
                "-progress", "pipe:1",
                "-nostats",
                output
            ]);

        using var cancellation = new CancellationTokenSource();
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(900));
        var runner = new FFmpegRunner(new FFmpegProgressParser());

        var result = await runner.RunAsync(
            command,
            TimeSpan.FromSeconds(30),
            cancellationToken: cancellation.Token);

        Assert.True(result.WasCancelled);
        Assert.False(result.Succeeded);
        DeleteIfExists(output);
    }

    [Fact]
    public async Task ProbeBuildRunAndProbeOutput_CompletesImageWorkflow()
    {
        var tools = new FFToolsSettingsService();
        Assert.NotNull(tools.FFmpegPath);
        Assert.NotNull(tools.FFprobePath);
        var fixture = await EnsureImageFixtureAsync(tools.FFmpegPath!);
        var output = Path.Combine(TestPaths.Results, "v0.3-image-output.webp");
        Directory.CreateDirectory(TestPaths.Results);
        DeleteIfExists(output);

        var probeService = new MediaProbeService(tools.FFprobePath!, new FfprobeJsonParser());
        var source = await probeService.ProbeAsync(fixture);
        Assert.Equal(MediaType.Image, source.MediaType);

        var command = new FFmpegCommandBuilder(tools.FFmpegPath!).BuildImageConversion(
            source,
            new ImageConversionOptions
            {
                InputPath = fixture,
                OutputPath = output,
                Format = ImageOutputFormat.WebP,
                TargetWidth = 320,
                Brightness = 0.05,
                Contrast = 1.1,
                Saturation = 1.2,
                Quality = 85
            });
        var result = await new FFmpegRunner(new FFmpegProgressParser()).RunAsync(command, null);

        Assert.True(result.Succeeded, result.StandardError);
        Assert.True(File.Exists(output));
        Assert.True(new FileInfo(output).Length > 0);
        var converted = await probeService.ProbeAsync(output);
        Assert.Equal(MediaType.Image, converted.MediaType);
        Assert.Equal(320, converted.PrimaryVideoStream?.Width);
    }

    [Fact]
    public async Task VideoV04_Trims720pAndExtractsMp3()
    {
        var tools = new FFToolsSettingsService();
        Assert.NotNull(tools.FFmpegPath);
        Assert.NotNull(tools.FFprobePath);
        var fixture = await EnsureFixtureAsync(tools.FFmpegPath!);
        Directory.CreateDirectory(TestPaths.Results);
        var videoOutput = Path.Combine(TestPaths.Results, "v0.4-trim-720p.mp4");
        var audioOutput = Path.Combine(TestPaths.Results, "v0.4-extracted-audio.mp3");
        DeleteIfExists(videoOutput);
        DeleteIfExists(audioOutput);

        var probeService = new MediaProbeService(tools.FFprobePath!, new FfprobeJsonParser());
        var source = await probeService.ProbeAsync(fixture);
        var builder = new FFmpegCommandBuilder(tools.FFmpegPath!);
        var runner = new FFmpegRunner(new FFmpegProgressParser());

        var videoRun = await runner.RunAsync(builder.BuildVideoConversion(source, new VideoConversionOptions
        {
            InputPath = fixture,
            OutputPath = videoOutput,
            Resolution = VideoResolutionMode.Hd720p,
            TrimStartSeconds = 0.25,
            TrimDurationSeconds = 1,
            Preset = "ultrafast",
            Crf = 30
        }), TimeSpan.FromSeconds(1));
        Assert.True(videoRun.Succeeded, videoRun.StandardError);
        var video = await probeService.ProbeAsync(videoOutput);
        Assert.Equal(720, video.PrimaryVideoStream?.Height);
        Assert.InRange(video.Duration?.TotalSeconds ?? 0, 0.8, 1.2);

        var audioRun = await runner.RunAsync(builder.BuildVideoConversion(source, new VideoConversionOptions
        {
            InputPath = fixture,
            OutputPath = audioOutput,
            OutputMode = VideoOutputMode.AudioMp3,
            TrimDurationSeconds = 1
        }), TimeSpan.FromSeconds(1));
        Assert.True(audioRun.Succeeded, audioRun.StandardError);
        var audio = await probeService.ProbeAsync(audioOutput);
        Assert.NotNull(audio.PrimaryAudioStream);
        Assert.Null(audio.PrimaryVideoStream);
    }

    private static async Task<string> EnsureFixtureAsync(string ffmpegPath)
    {
        await FixtureLock.WaitAsync();
        try
        {
            Directory.CreateDirectory(TestPaths.Fixtures);
            var fixture = Path.Combine(TestPaths.Fixtures, "vertical-slice-source.mp4");
            if (File.Exists(fixture) && new FileInfo(fixture).Length > 1_000)
            {
                return fixture;
            }

            var arguments = new[]
            {
                "-y",
                "-f", "lavfi",
                "-i", "testsrc2=size=640x360:rate=30",
                "-f", "lavfi",
                "-i", "sine=frequency=880:sample_rate=48000",
                "-t", "2",
                "-c:v", "libx264",
                "-preset", "ultrafast",
                "-pix_fmt", "yuv420p",
                "-c:a", "aac",
                "-shortest",
                fixture
            };

            var result = await RunToolAsync(ffmpegPath, arguments);
            Assert.Equal(0, result.ExitCode);
            Assert.True(File.Exists(fixture), result.StandardError);
            Assert.True(new FileInfo(fixture).Length > 1_000, result.StandardError);
            return fixture;
        }
        finally
        {
            FixtureLock.Release();
        }
    }

    private static async Task<string> EnsureImageFixtureAsync(string ffmpegPath)
    {
        Directory.CreateDirectory(TestPaths.Fixtures);
        var fixture = Path.Combine(TestPaths.Fixtures, "v0.3-image-source.png");
        if (File.Exists(fixture) && new FileInfo(fixture).Length > 500)
        {
            return fixture;
        }

        var result = await RunToolAsync(ffmpegPath,
        [
            "-y", "-f", "lavfi", "-i", "testsrc2=size=640x480:rate=1",
            "-frames:v", "1", fixture
        ]);
        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(fixture), result.StandardError);
        return fixture;
    }

    private static async Task<ToolResult> RunToolAsync(string executable, IEnumerable<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        Assert.True(process.Start());
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new ToolResult(process.ExitCode, await outputTask, await errorTask);
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private sealed record ToolResult(int ExitCode, string StandardOutput, string StandardError);

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
