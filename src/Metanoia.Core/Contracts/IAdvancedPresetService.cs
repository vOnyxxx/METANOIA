using Metanoia.Core.Models;

namespace Metanoia.Core.Contracts;

public interface IAdvancedPresetService
{
    IReadOnlyList<AdvancedPreset> Load();

    void Save(AdvancedPreset preset);

    void Delete(Guid id);
}
