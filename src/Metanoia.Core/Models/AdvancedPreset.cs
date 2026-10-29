namespace Metanoia.Core.Models;

public sealed record AdvancedPreset(
    Guid Id,
    string Name,
    string Arguments,
    DateTimeOffset CreatedAtUtc);
