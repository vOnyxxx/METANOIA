using System.Text;
using Metanoia.Core.Models;
using Metanoia.Infrastructure.Queue;

namespace Metanoia.IntegrationTests;

public sealed class ProcessingQueuePersistenceTests
{
    [Fact]
    public void Queue_RestoresPersistedJobStateAndProgress()
    {
        var storage = CreateStoragePath();
        try
        {
            var queue = new ProcessingQueue(storage);
            var job = new ProcessingJob("C:\\media\\input.mp4", "C:\\media\\output.mp4");
            queue.Enqueue(job);
            job.UpdateStatus(JobStatus.Completed);
            job.UpdateProgress(new JobProgress(100, TimeSpan.FromSeconds(12), 360, "1.2x", "end"));
            queue.Update(job);

            Assert.True(File.Exists(storage), queue.LastPersistenceError);
            Assert.Contains("\"Jobs\"", File.ReadAllText(storage), StringComparison.Ordinal);

            var restoredQueue = new ProcessingQueue(storage);
            var restored = Assert.Single(restoredQueue.Jobs);

            Assert.Equal(job.Id, restored.Id);
            Assert.Equal(JobStatus.Completed, restored.Status);
            Assert.Equal(100, restored.Progress?.Percent);
            Assert.Equal("C:\\media\\output.mp4", restored.OutputPath);
        }
        finally
        {
            DeleteStorageDirectory(storage);
        }
    }

    [Fact]
    public void Queue_ConvertsInterruptedProcessingJobToCancelledOnRestore()
    {
        var storage = CreateStoragePath();
        try
        {
            var queue = new ProcessingQueue(storage);
            var job = new ProcessingJob("C:\\media\\input.mp4", "C:\\media\\output.mp4");
            queue.Enqueue(job);
            job.UpdateStatus(JobStatus.Processing);
            queue.Update(job);

            Assert.True(File.Exists(storage), queue.LastPersistenceError);
            Assert.Contains("\"Jobs\"", File.ReadAllText(storage), StringComparison.Ordinal);

            var restored = Assert.Single(new ProcessingQueue(storage).Jobs);

            Assert.Equal(JobStatus.Cancelled, restored.Status);
        }
        finally
        {
            DeleteStorageDirectory(storage);
        }
    }

    [Fact]
    public void Queue_IgnoresMalformedJson()
    {
        var storage = CreateStoragePath();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(storage)!);
            File.WriteAllText(storage, "{ invalid json", Encoding.UTF8);

            var queue = new ProcessingQueue(storage);

            Assert.Empty(queue.Jobs);
        }
        finally
        {
            DeleteStorageDirectory(storage);
        }
    }

    [Fact]
    public void Queue_RemoveAndClearCompleted_PersistTheResult()
    {
        var storage = CreateStoragePath();
        try
        {
            var queue = new ProcessingQueue(storage);
            var completed = new ProcessingJob("C:\\media\\one.wav", "C:\\media\\one.mp3");
            completed.UpdateStatus(JobStatus.Completed);
            var active = new ProcessingJob("C:\\media\\two.mp4", "C:\\media\\two-out.mp4");
            active.UpdateStatus(JobStatus.Processing);
            queue.Enqueue(completed);
            queue.Enqueue(active);

            queue.ClearCompleted();
            Assert.Equal(active.Id, Assert.Single(queue.Jobs).Id);

            queue.Remove(active.Id);
            Assert.Empty(queue.Jobs);
            Assert.Empty(new ProcessingQueue(storage).Jobs);
        }
        finally
        {
            DeleteStorageDirectory(storage);
        }
    }

    private static string CreateStoragePath() => Path.Combine(
        Path.GetTempPath(),
        "metanoia-tests",
        Guid.NewGuid().ToString("N"),
        "queue.json");

    private static void DeleteStorageDirectory(string storagePath)
    {
        var directory = Path.GetDirectoryName(storagePath);
        if (directory is not null && Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }
}
