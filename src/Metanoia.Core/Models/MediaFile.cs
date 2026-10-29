namespace Metanoia.Core.Models;

public sealed record MediaFile(
    string FullPath,
    string Name,
    long SizeBytes,
    DateTimeOffset LastWriteTimeUtc);
