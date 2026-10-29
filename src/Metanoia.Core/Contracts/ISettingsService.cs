namespace Metanoia.Core.Contracts;

public interface ISettingsService
{
    string? FFmpegPath { get; }

    string? FFprobePath { get; }

    bool AreToolsAvailable { get; }
}
