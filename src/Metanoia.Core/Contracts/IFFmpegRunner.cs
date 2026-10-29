using Metanoia.Core.Models;

namespace Metanoia.Core.Contracts;

public interface IFFmpegRunner
{
    Task<FFmpegRunResult> RunAsync(
        FFmpegCommand command,
        TimeSpan? expectedDuration,
        IProgress<JobProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
