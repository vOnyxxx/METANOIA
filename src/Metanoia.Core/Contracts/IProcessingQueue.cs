using Metanoia.Core.Models;

namespace Metanoia.Core.Contracts;

public interface IProcessingQueue
{
    event EventHandler? Changed;

    IReadOnlyList<ProcessingJob> Jobs { get; }

    void Enqueue(ProcessingJob job);

    void Update(ProcessingJob job);

    void Remove(Guid id);

    void ClearCompleted();
}
