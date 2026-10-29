namespace Metanoia.Core.Models;

public sealed record JobProgress(
    double? Percent,
    TimeSpan ProcessedDuration,
    long? Frame,
    string? Speed,
    string Phase);
