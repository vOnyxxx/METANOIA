using Metanoia.Core.Models;
using Metanoia.Core.Services;
using Metanoia.Infrastructure.FFmpeg;

namespace Metanoia.IntegrationTests;

[Collection(FFmpegCollection.Name)]
public sealed class InterfaceToolsIntegrationTests
{
    [Fact]
    public async Task OggCanBeDecodedEqualizedAndConvertedToFlac()
    {
        var tools = new FFToolsSettingsService(); var runner = new FFmpegRunner(new FFmpegProgressParser());
        var sourcePath = Path.Combine(TestPaths.Fixtures, "interface-source.ogg"); Directory.CreateDirectory(TestPaths.Results);
        var generated = await runner.RunAsync(new FFmpegCommand(tools.FFmpegPath!, ["-y", "-f", "lavfi", "-i", "sine=frequency=100:duration=2", "-c:a", "libvorbis", sourcePath]), null);
        Assert.True(generated.Succeeded, generated.StandardError);
        var probe = new MediaProbeService(tools.FFprobePath!, new FfprobeJsonParser()); var source = await probe.ProbeAsync(sourcePath);
        var output = Path.Combine(TestPaths.Results, "interface-ogg-eq.flac");
        var typed = new FFmpegCommandBuilder(tools.FFmpegPath!).BuildAudioConversion(source, new AudioConversionOptions { InputPath = sourcePath, OutputPath = Path.ChangeExtension(output, ".wav"), NormalizeLoudness = false, LowGainDb = -6, HighGainDb = 3 });
        var result = await runner.RunAsync(ContainerCommandAdapter.Adapt(typed, output, "flac", true, false), source.Duration);
        Assert.True(result.Succeeded, result.StandardError);
        var actual = await probe.ProbeAsync(output); Assert.Equal("flac", actual.PrimaryAudioStream?.CodecName); Assert.Equal(2, actual.Duration!.Value.TotalSeconds, 1);
    }
}
