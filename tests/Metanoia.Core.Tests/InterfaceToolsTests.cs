using Metanoia.Core.Models;
using Metanoia.Core.Services;

namespace Metanoia.Core.Tests;

public sealed class InterfaceToolsTests
{
    [Theory]
    [InlineData(0, 0, 1000, 1000, 0, 0, 1920, 1080)]
    [InlineData(250, 359.375, 750, 640.625, 480, 270, 960, 540)]
    [InlineData(750, 640.625, 250, 359.375, 480, 270, 960, 540)]
    public void CropMapsLetterboxedDragToSource(double sx, double sy, double ex, double ey, int x, int y, int width, int height)
    {
        var crop = ImageCropGeometry.FromViewport(1920, 1080, 1000, 1000, sx, sy, ex, ey);
        Assert.Equal(new ImageCropRegion(x, y, width, height), crop);
    }
    [Fact] public void CropOutsideImageDoesNotProduceRegion() => Assert.Null(ImageCropGeometry.FromViewport(1920, 1080, 1000, 1000, 0, 0, 1000, 100));
    [Fact] public void TinyCropDoesNotProduceRegion() => Assert.Null(ImageCropGeometry.FromViewport(100, 100, 1000, 1000, 100, 100, 101, 101));
    [Fact]
    public void ContainerSwitchPreservesFiltersButDropsCodecSpecificFlags()
    {
        var input = new FFmpegCommand("ffmpeg.exe", ["-i", "input.wav", "-af", "equalizer=f=100:g=6,loudnorm=I=-14", "-c:a", "pcm_s24le", "-progress", "pipe:1", "output.wav"]);
        var output = ContainerCommandAdapter.Adapt(input, "output.flac", "flac", true, false);
        Assert.Contains("equalizer=f=100:g=6,loudnorm=I=-14,asetnsamples=n=4608:p=0", output.Arguments);
        Assert.DoesNotContain("pcm_s24le", output.Arguments);
        Assert.Equal("flac", output.Arguments[^2]);
        Assert.Equal("output.flac", output.Arguments[^1]);
    }
    [Fact]
    public void EqIsAppliedBeforeBothLoudnessPasses()
    {
        var path = Path.Combine(Path.GetTempPath(), "metanoia-eq-unit.wav"); File.WriteAllBytes(path, []);
        var source = new MediaProbeResult { File = new MediaFile(path, "input.wav", 0, DateTimeOffset.UtcNow), MediaType = MediaType.Audio, Duration = TimeSpan.FromSeconds(2), Streams = [new MediaStreamInfo { Index = 0, Type = MediaType.Audio }] };
        var options = new AudioConversionOptions { InputPath = path, OutputPath = Path.ChangeExtension(path, ".out.wav"), LowGainDb = 6, MidGainDb = -3, GainDb = 1 };
        var builder = new FFmpegCommandBuilder("ffmpeg.exe");
        foreach (var command in new[] { builder.BuildAudioLoudnessAnalysis(source, options), builder.BuildAudioConversion(source, options) })
        {
            var filter = command.Arguments[command.Arguments.ToList().IndexOf("-af") + 1];
            Assert.True(filter.IndexOf("equalizer=", StringComparison.Ordinal) < filter.IndexOf("loudnorm=", StringComparison.Ordinal));
            Assert.Contains("g=-3", filter); Assert.Contains("volume=1dB", filter);
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.BuildAudioConversion(source, options with { LowGainDb = double.NaN }));
    }
}
