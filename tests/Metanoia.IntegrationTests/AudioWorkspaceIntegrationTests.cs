using System.Diagnostics;
using Metanoia.Core.Models;
using Metanoia.Core.Services;
using Metanoia.Infrastructure.FFmpeg;

namespace Metanoia.IntegrationTests;

[Collection(FFmpegCollection.Name)]
public sealed class AudioWorkspaceIntegrationTests
{
    [Fact]
    public async Task ProbeVisualizeMeasureNormalizeAndExport_CompletesRealAudioFlow()
    {
        var tools = new FFToolsSettingsService();
        Assert.NotNull(tools.FFmpegPath);
        Assert.NotNull(tools.FFprobePath);
        Directory.CreateDirectory(TestPaths.Fixtures);
        Directory.CreateDirectory(TestPaths.Results);

        var fixture = Path.Combine(TestPaths.Fixtures, "v0.5-audio-source.wav");
        var waveform = Path.Combine(TestPaths.Results, "v0.5-integration-waveform.png");
        var spectrogram = Path.Combine(TestPaths.Results, "v0.5-integration-spectrogram.png");
        var output = Path.Combine(TestPaths.Results, "v0.5-integration-normalized.wav");
        await EnsureAudioFixtureAsync(tools.FFmpegPath!, fixture);
        DeleteIfExists(waveform);
        DeleteIfExists(spectrogram);
        DeleteIfExists(output);

        var probeService = new MediaProbeService(tools.FFprobePath!, new FfprobeJsonParser());
        var source = await probeService.ProbeAsync(fixture);
        Assert.Equal(MediaType.Audio, source.MediaType);
        Assert.NotNull(source.PrimaryAudioStream);

        var builder = new FFmpegCommandBuilder(tools.FFmpegPath!);
        var runner = new FFmpegRunner(new FFmpegProgressParser());
        foreach (var (kind, path) in new[]
                 {
                     (AudioVisualizationKind.Waveform, waveform),
                     (AudioVisualizationKind.Spectrogram, spectrogram)
                 })
        {
            var visualization = builder.BuildAudioVisualization(source, path, kind);
            var result = await runner.RunAsync(visualization, source.Duration);
            Assert.True(result.Succeeded, result.StandardError);
            Assert.True(File.Exists(path));
            Assert.True(new FileInfo(path).Length > 0);
        }

        var firstPassOptions = new AudioConversionOptions
        {
            InputPath = fixture,
            OutputPath = output,
            Format = AudioOutputFormat.Wav,
            SampleRate = 48000,
            Channels = 2,
            NormalizeLoudness = true,
            TargetLufs = -14,
            TruePeakDb = -1
        };
        var analysis = await runner.RunAsync(
            builder.BuildAudioLoudnessAnalysis(source, firstPassOptions),
            source.Duration);
        Assert.True(analysis.Succeeded, analysis.StandardError);
        var measurement = new LoudnessAnalysisParser().Parse(analysis.StandardError);

        var export = await runner.RunAsync(
            builder.BuildAudioConversion(source, firstPassOptions with { LoudnessMeasurement = measurement }),
            source.Duration);
        Assert.True(export.Succeeded, export.StandardError);
        Assert.True(File.Exists(output));
        Assert.True(new FileInfo(output).Length > 0);

        var converted = await probeService.ProbeAsync(output);
        Assert.Equal(MediaType.Audio, converted.MediaType);
        Assert.Equal("pcm_s24le", converted.PrimaryAudioStream?.CodecName);
        Assert.Equal(48000, converted.PrimaryAudioStream?.SampleRate);
        Assert.Equal(2, converted.PrimaryAudioStream?.Channels);
    }

    private static async Task EnsureAudioFixtureAsync(string ffmpegPath, string fixture)
    {
        if (File.Exists(fixture) && new FileInfo(fixture).Length > 0)
        {
            return;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in new[]
                 {
                     "-y", "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=48000:duration=3",
                     "-af", "volume=0.08", "-c:a", "pcm_s16le", fixture
                 })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("No fue posible generar el fixture de audio.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        await outputTask;
        var error = await errorTask;
        Assert.True(process.ExitCode == 0, error);
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
