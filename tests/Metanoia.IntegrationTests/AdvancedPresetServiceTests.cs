using Metanoia.Core.Models;
using Metanoia.Infrastructure.Settings;

namespace Metanoia.IntegrationTests;

public sealed class AdvancedPresetServiceTests
{
    [Fact]
    public void SaveLoadAndDelete_PersistsPreset()
    {
        var directory = Path.Combine(Path.GetTempPath(), "metanoia-tests", Guid.NewGuid().ToString("N"));
        var storage = Path.Combine(directory, "presets.json");
        try
        {
            var service = new AdvancedPresetService(storage);
            Assert.NotEmpty(service.Load());

            var preset = new AdvancedPreset(Guid.NewGuid(), "Test", "-movflags +faststart", DateTimeOffset.UtcNow);
            service.Save(preset);

            Assert.Contains(service.Load(), item => item.Id == preset.Id && item.Arguments == preset.Arguments);

            service.Delete(preset.Id);
            Assert.DoesNotContain(service.Load(), item => item.Id == preset.Id);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
