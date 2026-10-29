namespace Metanoia.Core.Models;

public sealed record FFmpegRunResult(
    bool Succeeded,
    bool WasCancelled,
    int ExitCode,
    string StandardError);
