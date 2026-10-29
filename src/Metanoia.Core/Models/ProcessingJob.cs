namespace Metanoia.Core.Models;

public sealed class ProcessingJob
{
    public ProcessingJob(string inputPath, string outputPath)
        : this(Guid.NewGuid(), inputPath, outputPath, DateTimeOffset.UtcNow, JobStatus.Pending, null, null)
    {
    }

    private ProcessingJob(
        Guid id,
        string inputPath,
        string outputPath,
        DateTimeOffset createdAtUtc,
        JobStatus status,
        JobProgress? progress,
        string? errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        InputPath = inputPath;
        OutputPath = outputPath;
        CreatedAtUtc = createdAtUtc;
        Status = status;
        Progress = progress;
        ErrorMessage = errorMessage;
    }

    public static ProcessingJob Restore(
        Guid id,
        string inputPath,
        string outputPath,
        DateTimeOffset createdAtUtc,
        JobStatus status,
        JobProgress? progress,
        string? errorMessage) =>
        new(id, inputPath, outputPath, createdAtUtc, status, progress, errorMessage);

    public Guid Id { get; }

    public string InputPath { get; }

    public string OutputPath { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public JobStatus Status { get; private set; } = JobStatus.Pending;

    public JobProgress? Progress { get; private set; }

    public string? ErrorMessage { get; private set; }

    public void UpdateStatus(JobStatus status, string? errorMessage = null)
    {
        Status = status;
        ErrorMessage = errorMessage;
    }

    public void UpdateProgress(JobProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        Progress = progress;
    }
}
