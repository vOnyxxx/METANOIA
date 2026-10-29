using System.IO;
using Metanoia.Core.Models;

namespace Metanoia.App.ViewModels;

public sealed record QueueItemViewModel(
    Guid Id,
    string FileName,
    string OutputName,
    string Status,
    string Progress,
    double ProgressValue,
    string CreatedAt,
    JobStatus StatusCode)
{
    public static QueueItemViewModel FromJob(ProcessingJob job)
    {
        ArgumentNullException.ThrowIfNull(job);
        var percent = job.Progress?.Percent;
        return new QueueItemViewModel(
            job.Id,
            Path.GetFileName(job.InputPath),
            Path.GetFileName(job.OutputPath),
            TranslateStatus(job.Status),
            percent is null ? "—" : $"{percent.Value:0}%",
            percent ?? 0,
            job.CreatedAtUtc.ToLocalTime().ToString("dd MMM · HH:mm"),
            job.Status);
    }

    private static string TranslateStatus(JobStatus status) => status switch
    {
        JobStatus.Pending => "Pendiente",
        JobStatus.Analyzing => "Analizando",
        JobStatus.Processing => "Procesando",
        JobStatus.Completed => "Completado",
        JobStatus.Cancelled => "Cancelado",
        JobStatus.Error => "Error",
        _ => status.ToString()
    };
}
