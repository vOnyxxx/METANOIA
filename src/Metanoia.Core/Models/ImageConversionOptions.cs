namespace Metanoia.Core.Models;

public enum ImageOutputFormat
{
    Png,
    Jpeg,
    WebP
}

public sealed record ImageConversionOptions
{
    public required string InputPath { get; init; }

    public required string OutputPath { get; init; }
    public string ExtraFilter { get; init; } = "";

    public ImageOutputFormat Format { get; init; } = ImageOutputFormat.Png;

    public int TargetWidth { get; init; }

    public bool CropEnabled { get; init; }

    public int CropX { get; init; }

    public int CropY { get; init; }

    public int CropWidth { get; init; }

    public int CropHeight { get; init; }

    public double Brightness { get; init; }

    public double Contrast { get; init; } = 1;

    public double Saturation { get; init; } = 1;

    public double Temperature { get; init; }
    public string Filter { get; init; } = "Original";

    public int Quality { get; init; } = 90;
}
