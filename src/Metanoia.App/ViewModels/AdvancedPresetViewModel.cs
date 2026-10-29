using Metanoia.Core.Models;

namespace Metanoia.App.ViewModels;

public sealed record AdvancedPresetViewModel(Guid Id, string Name, string Arguments, string CreatedAt)
{
    public static AdvancedPresetViewModel FromModel(AdvancedPreset preset) =>
        new(
            preset.Id,
            preset.Name,
            preset.Arguments,
            preset.CreatedAtUtc == DateTimeOffset.UnixEpoch
                ? "Incluido con v0.6"
                : preset.CreatedAtUtc.ToLocalTime().ToString("dd MMM yyyy · HH:mm"));
}
