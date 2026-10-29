using Metanoia.Core.Models;

namespace Metanoia.Core.Contracts;

public interface IMediaProbeService
{
    Task<MediaProbeResult> ProbeAsync(string inputPath, CancellationToken cancellationToken = default);
}
