using System.Text.Json;
using Metanoia.Core.Contracts;
using Metanoia.Core.Models;

namespace Metanoia.Infrastructure.Settings;

public sealed class AdvancedPresetService : IAdvancedPresetService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly IReadOnlyList<AdvancedPreset> BuiltInPresets =
    [
        new(Guid.Parse("79d6219c-a7c1-49d8-bdf2-6d6fbf8e0601"), "Web compatible", "-movflags +faststart -pix_fmt yuv420p", DateTimeOffset.UnixEpoch),
        new(Guid.Parse("79d6219c-a7c1-49d8-bdf2-6d6fbf8e0602"), "Preservar metadata", "-map_metadata 0 -map_chapters 0", DateTimeOffset.UnixEpoch),
        new(Guid.Parse("79d6219c-a7c1-49d8-bdf2-6d6fbf8e0603"), "Finalización corta", "-threads 0 -shortest", DateTimeOffset.UnixEpoch)
    ];
    private readonly string _storagePath;

    public AdvancedPresetService(string? storagePath = null)
    {
        _storagePath = storagePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "METANOIA",
            "advanced-presets-v0.6.json");
    }

    public IReadOnlyList<AdvancedPreset> Load()
    {
        try
        {
            if (!File.Exists(_storagePath))
            {
                return BuiltInPresets;
            }

            return JsonSerializer.Deserialize<List<AdvancedPreset>>(
                       File.ReadAllText(_storagePath), JsonOptions)
                   ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    public void Save(AdvancedPreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        var presets = Load().Where(item => item.Id != preset.Id).ToList();
        presets.Add(preset);
        Persist(presets);
    }

    public void Delete(Guid id)
    {
        Persist(Load().Where(item => item.Id != id).ToArray());
    }

    private void Persist(IReadOnlyCollection<AdvancedPreset> presets)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_storagePath)
            ?? throw new InvalidOperationException("La ruta de presets no tiene directorio."));
        File.WriteAllText(_storagePath, JsonSerializer.Serialize(presets, JsonOptions));
    }
}
