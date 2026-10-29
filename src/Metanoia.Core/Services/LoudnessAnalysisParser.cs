using System.Globalization;
using System.Text.Json;
using Metanoia.Core.Models;

namespace Metanoia.Core.Services;

public sealed class LoudnessAnalysisParser
{
    public AudioLoudnessMeasurement Parse(string standardError)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(standardError);

        var markerIndex = standardError.LastIndexOf("\"input_i\"", StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            throw new FormatException("FFmpeg no devolvió mediciones loudnorm en formato JSON.");
        }

        var jsonStart = standardError.LastIndexOf('{', markerIndex);
        var jsonEnd = standardError.IndexOf('}', markerIndex);
        if (jsonStart < 0 || jsonEnd <= jsonStart)
        {
            throw new FormatException("El bloque JSON de loudnorm está incompleto.");
        }

        var values = JsonSerializer.Deserialize<Dictionary<string, string>>(
            standardError[jsonStart..(jsonEnd + 1)])
            ?? throw new FormatException("El bloque JSON de loudnorm está vacío.");

        return new AudioLoudnessMeasurement
        {
            InputIntegratedLufs = ParseFinite(values, "input_i"),
            InputTruePeakDb = ParseFinite(values, "input_tp"),
            InputLoudnessRangeLu = ParseFinite(values, "input_lra"),
            InputThresholdLufs = ParseFinite(values, "input_thresh"),
            TargetOffsetDb = ParseFinite(values, "target_offset")
        };
    }

    private static double ParseFinite(IReadOnlyDictionary<string, string> values, string key)
    {
        if (!values.TryGetValue(key, out var rawValue) ||
            !double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ||
            !double.IsFinite(value))
        {
            throw new FormatException($"La medición loudnorm '{key}' no es un número finito.");
        }

        return value;
    }
}
