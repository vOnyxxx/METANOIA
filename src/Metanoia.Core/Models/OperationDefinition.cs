namespace Metanoia.Core.Models;

public sealed record OperationDefinition(
    string Id,
    string DisplayName,
    string Description,
    MediaType SupportedMediaType);
