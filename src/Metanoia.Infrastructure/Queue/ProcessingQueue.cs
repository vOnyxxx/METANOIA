using Metanoia.Core.Contracts;
using Metanoia.Core.Models;
using System.Text.Json;

namespace Metanoia.Infrastructure.Queue;

public sealed class ProcessingQueue : IProcessingQueue
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly object _sync = new();
    private readonly List<ProcessingJob> _jobs = [];
    private readonly string _storagePath;

    public ProcessingQueue(string? storagePath = null)
    {
        _storagePath = storagePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "METANOIA",
            "queue-v0.2.json");
        LoadPersistedJobs();
    }

    public event EventHandler? Changed;

    public string? LastPersistenceError { get; private set; }

    public IReadOnlyList<ProcessingJob> Jobs
    {
        get
        {
            lock (_sync)
            {
                return _jobs.ToArray();
            }
        }
    }

    public void Enqueue(ProcessingJob job)
    {
        ArgumentNullException.ThrowIfNull(job);
        lock (_sync)
        {
            _jobs.Add(job);
            TryPersistLocked();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Update(ProcessingJob job)
    {
        ArgumentNullException.ThrowIfNull(job);
        lock (_sync)
        {
            if (_jobs.All(existing => existing.Id != job.Id))
            {
                _jobs.Add(job);
            }

            TryPersistLocked();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Remove(Guid id)
    {
        lock (_sync)
        {
            _jobs.RemoveAll(job => job.Id == id);
            TryPersistLocked();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void ClearCompleted()
    {
        lock (_sync)
        {
            _jobs.RemoveAll(job => job.Status is JobStatus.Completed or JobStatus.Cancelled or JobStatus.Error);
            TryPersistLocked();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void LoadPersistedJobs()
    {
        try
        {
            if (!File.Exists(_storagePath))
            {
                return;
            }

            var document = JsonSerializer.Deserialize<QueueDocument>(
                File.ReadAllText(_storagePath),
                JsonOptions);
            if (document?.Jobs is null)
            {
                return;
            }

            foreach (var entry in document.Jobs)
            {
                if (string.IsNullOrWhiteSpace(entry.InputPath) ||
                    string.IsNullOrWhiteSpace(entry.OutputPath))
                {
                    continue;
                }

                var restoredStatus = entry.Status == JobStatus.Processing
                    ? JobStatus.Cancelled
                    : entry.Status;
                _jobs.Add(ProcessingJob.Restore(
                    entry.Id,
                    entry.InputPath,
                    entry.OutputPath,
                    entry.CreatedAtUtc,
                    restoredStatus,
                    entry.Progress,
                    entry.ErrorMessage));
            }
        }
        catch (JsonException)
        {
            // Una cola dañada no debe impedir que METANOIA inicie.
        }
        catch (IOException)
        {
            // La cola se considera vacía si Windows no permite leerla temporalmente.
        }
        catch (UnauthorizedAccessException)
        {
            // La aplicación conserva funcionamiento en memoria sin persistencia.
        }
    }

    private void TryPersistLocked()
    {
        var temporaryPath = _storagePath + ".tmp";
        string? serializedDocument = null;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_storagePath)
                ?? throw new InvalidOperationException("La ruta de la cola no tiene directorio."));

            var document = new QueueDocument(
                1,
                _jobs.Select(job => new QueueEntry(
                    job.Id,
                    job.InputPath,
                    job.OutputPath,
                    job.CreatedAtUtc,
                    job.Status,
                    job.Progress,
                    job.ErrorMessage)).ToArray());

            serializedDocument = JsonSerializer.Serialize(document, JsonOptions);
            File.WriteAllText(temporaryPath, serializedDocument);
            File.Move(temporaryPath, _storagePath, true);
            LastPersistenceError = null;
        }
        catch (IOException exception)
        {
            TryPersistDirectly(serializedDocument, temporaryPath, exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            TryPersistDirectly(serializedDocument, temporaryPath, exception);
        }
    }

    private void TryPersistDirectly(string? serializedDocument, string temporaryPath, Exception originalException)
    {
        TryDeleteTemporaryFile(temporaryPath);
        if (serializedDocument is null)
        {
            LastPersistenceError = originalException.Message;
            return;
        }

        try
        {
            File.WriteAllText(_storagePath, serializedDocument);
            LastPersistenceError = null;
        }
        catch (IOException fallbackException)
        {
            LastPersistenceError = $"{originalException.Message} Fallback: {fallbackException.Message}";
        }
        catch (UnauthorizedAccessException fallbackException)
        {
            LastPersistenceError = $"{originalException.Message} Fallback: {fallbackException.Message}";
        }
    }

    private static void TryDeleteTemporaryFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // El archivo temporal se reemplazará en el siguiente guardado.
        }
        catch (UnauthorizedAccessException)
        {
            // No afecta el funcionamiento de la cola en memoria.
        }
    }

    private sealed record QueueDocument(int SchemaVersion, IReadOnlyList<QueueEntry> Jobs);

    private sealed record QueueEntry(
        Guid Id,
        string InputPath,
        string OutputPath,
        DateTimeOffset CreatedAtUtc,
        JobStatus Status,
        JobProgress? Progress,
        string? ErrorMessage);
}
