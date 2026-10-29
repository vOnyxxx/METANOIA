using Metanoia.Core.Services;

namespace Metanoia.Core.Tests;

public sealed class LoudnessAnalysisParserTests
{
    [Fact]
    public void Parse_ReadsFfmpegLoudnormJsonFromDiagnosticOutput()
    {
        const string diagnostic = """
            [Parsed_loudnorm_0] summary
            {
                "input_i" : "-22.43",
                "input_tp" : "-5.12",
                "input_lra" : "3.10",
                "input_thresh" : "-32.57",
                "output_i" : "-13.88",
                "output_tp" : "-1.00",
                "output_lra" : "2.90",
                "output_thresh" : "-24.10",
                "normalization_type" : "dynamic",
                "target_offset" : "-0.12"
            }
            """;

        var result = new LoudnessAnalysisParser().Parse(diagnostic);

        Assert.Equal(-22.43, result.InputIntegratedLufs, 2);
        Assert.Equal(-5.12, result.InputTruePeakDb, 2);
        Assert.Equal(3.10, result.InputLoudnessRangeLu, 2);
        Assert.Equal(-32.57, result.InputThresholdLufs, 2);
        Assert.Equal(-0.12, result.TargetOffsetDb, 2);
    }

    [Fact]
    public void Parse_RejectsInfiniteSilentMeasurement()
    {
        const string diagnostic = """
            {"input_i":"-inf","input_tp":"-inf","input_lra":"0.00","input_thresh":"-70.00","target_offset":"0.00"}
            """;

        Assert.Throws<FormatException>(() => new LoudnessAnalysisParser().Parse(diagnostic));
    }
}
