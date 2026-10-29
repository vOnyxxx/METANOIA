namespace Metanoia.Core.Models;

public enum AudioOutputFormat
{
    Wav,
    Mp3,
    Aac
}

public enum AudioVisualizationKind
{
    Waveform,
    StereoWaveform,
    Spectrum,
    Spectrogram
}

public sealed record AudioLoudnessMeasurement
{
    public required double InputIntegratedLufs { get; init; }

    public required double InputTruePeakDb { get; init; }

    public required double InputLoudnessRangeLu { get; init; }

    public required double InputThresholdLufs { get; init; }

    public required double TargetOffsetDb { get; init; }
}

public sealed record AudioConversionOptions
{
    public required string InputPath { get; init; }

    public required string OutputPath { get; init; }
    public string ExtraFilter { get; init; } = "";

    public AudioOutputFormat Format { get; init; } = AudioOutputFormat.Wav;

    public double TrimStartSeconds { get; init; }

    public double TrimDurationSeconds { get; init; }

    public int SampleRate { get; init; }

    public int Channels { get; init; }

    public bool NormalizeLoudness { get; init; } = true;

    public double TargetLufs { get; init; } = -14;

    public double TruePeakDb { get; init; } = -1;

    public double LoudnessRangeLu { get; init; } = 11;

    public double LowGainDb { get; init; }
    public double MidGainDb { get; init; }
    public double HighGainDb { get; init; }
    public double GainDb { get; init; }

    public AudioLoudnessMeasurement? LoudnessMeasurement { get; init; }
}
