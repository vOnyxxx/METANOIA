using Metanoia.Core.Models;
using Metanoia.Core.Services;

namespace Metanoia.Core.Tests;

public sealed class AudioCommandBuilderTests
{
    [Fact]
    public void BuildAudioLoudnessAnalysis_CreatesFirstPassJsonMeasurement()
    {
        var fixture = CreateEmptyFixture("loudness-input.wav");
        var options = CreateOptions(fixture, "loudness-output.wav");

        var command = new FFmpegCommandBuilder("ffmpeg.exe")
            .BuildAudioLoudnessAnalysis(CreateProbeResult(fixture), options);

        Assert.Contains(command.Arguments, argument => argument.Contains("loudnorm=I=-14:TP=-1:LRA=11", StringComparison.Ordinal));
        Assert.Contains(command.Arguments, argument => argument.Contains("print_format=json", StringComparison.Ordinal));
        Assert.Contains("NUL", command.Arguments);
        Assert.Contains("pipe:1", command.Arguments);
    }

    [Fact]
    public void BuildAudioConversion_UsesMeasuredValuesForSecondPass()
    {
        var fixture = CreateEmptyFixture("measured-input.wav");
        var options = CreateOptions(fixture, "measured-output.wav") with
        {
            LoudnessMeasurement = new AudioLoudnessMeasurement
            {
                InputIntegratedLufs = -22.4,
                InputTruePeakDb = -5.2,
                InputLoudnessRangeLu = 3.1,
                InputThresholdLufs = -32.5,
                TargetOffsetDb = 0.2
            }
        };

        var command = new FFmpegCommandBuilder("ffmpeg.exe")
            .BuildAudioConversion(CreateProbeResult(fixture), options);
        var filter = Assert.Single(command.Arguments.Where(argument => argument.StartsWith("loudnorm=", StringComparison.Ordinal)));

        Assert.Contains("measured_I=-22.4", filter, StringComparison.Ordinal);
        Assert.Contains("measured_TP=-5.2", filter, StringComparison.Ordinal);
        Assert.Contains("offset=0.2", filter, StringComparison.Ordinal);
        Assert.Contains("linear=true", filter, StringComparison.Ordinal);
        Assert.Contains("pcm_s24le", command.Arguments);
    }

    [Theory]
    [InlineData(AudioOutputFormat.Wav, ".wav", "pcm_s24le")]
    [InlineData(AudioOutputFormat.Mp3, ".mp3", "libmp3lame")]
    [InlineData(AudioOutputFormat.Aac, ".m4a", "aac")]
    public void BuildAudioConversion_MapsFormatsAndTechnicalOptions(
        AudioOutputFormat format,
        string extension,
        string codec)
    {
        var fixture = CreateEmptyFixture($"format-{format}.wav");
        var options = CreateOptions(fixture, $"format-output{extension}") with
        {
            Format = format,
            NormalizeLoudness = false,
            SampleRate = 48000,
            Channels = 2,
            TrimStartSeconds = 0.25,
            TrimDurationSeconds = 1.5
        };

        var command = new FFmpegCommandBuilder("ffmpeg.exe")
            .BuildAudioConversion(CreateProbeResult(fixture), options);

        Assert.Contains(codec, command.Arguments);
        Assert.Contains("48000", command.Arguments);
        Assert.Contains("2", command.Arguments);
        Assert.Contains("0.25", command.Arguments);
        Assert.Contains("1.5", command.Arguments);
        Assert.Equal(options.OutputPath, command.Arguments[^1]);
    }

    [Theory]
    [InlineData(AudioVisualizationKind.Waveform, "showwavespic")]
    [InlineData(AudioVisualizationKind.Spectrogram, "showspectrumpic")]
    public void BuildAudioVisualization_UsesDeterministicPngFilter(
        AudioVisualizationKind kind,
        string expectedFilter)
    {
        var fixture = CreateEmptyFixture($"visual-{kind}.wav");
        var output = Path.Combine(Path.GetDirectoryName(fixture)!, $"visual-{kind}.png");

        var command = new FFmpegCommandBuilder("ffmpeg.exe").BuildAudioVisualization(
            CreateProbeResult(fixture),
            output,
            kind,
            trimStartSeconds: 0.2,
            trimDurationSeconds: 1);

        Assert.Contains(command.Arguments, argument => argument.Contains(expectedFilter, StringComparison.Ordinal));
        Assert.Contains("png", command.Arguments);
        Assert.Equal(output, command.Arguments[^1]);
    }

    [Theory]
    [InlineData(-37, -1)]
    [InlineData(-14, 1)]
    public void BuildAudioConversion_RejectsUnsafeLoudnessTargets(double targetLufs, double truePeak)
    {
        var fixture = CreateEmptyFixture("invalid-loudness.wav");
        var options = CreateOptions(fixture, "invalid-loudness-output.wav") with
        {
            TargetLufs = targetLufs,
            TruePeakDb = truePeak
        };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new FFmpegCommandBuilder("ffmpeg.exe").BuildAudioConversion(CreateProbeResult(fixture), options));
    }

    private static AudioConversionOptions CreateOptions(string inputPath, string outputName) => new()
    {
        InputPath = inputPath,
        OutputPath = Path.Combine(Path.GetDirectoryName(inputPath)!, outputName),
        Format = AudioOutputFormat.Wav,
        NormalizeLoudness = true,
        TargetLufs = -14,
        TruePeakDb = -1,
        LoudnessRangeLu = 11
    };

    private static MediaProbeResult CreateProbeResult(string inputPath) => new()
    {
        File = new MediaFile(inputPath, Path.GetFileName(inputPath), 0, DateTimeOffset.UtcNow),
        MediaType = MediaType.Audio,
        Duration = TimeSpan.FromSeconds(3),
        Streams =
        [
            new MediaStreamInfo
            {
                Index = 0,
                Type = MediaType.Audio,
                CodecName = "pcm_s16le",
                SampleRate = 48000,
                Channels = 1
            }
        ]
    };

    private static string CreateEmptyFixture(string fileName)
    {
        var directory = Path.Combine(Path.GetTempPath(), "METANOIA", "tests", "audio-command-builder");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        File.WriteAllBytes(path, []);
        return path;
    }
}
