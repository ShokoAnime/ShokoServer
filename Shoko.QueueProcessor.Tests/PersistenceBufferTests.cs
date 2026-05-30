using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.QueueProcessor.Orchestration;
using Shoko.QueueProcessor.Storage;
using Xunit;

namespace Shoko.QueueProcessor.Tests;

/// <summary>
/// Tests for <see cref="PersistenceBuffer"/> coalescing logic.
/// The key invariant: a job that is enqueued AND completed before the flush fires
/// generates zero DB writes.
/// </summary>
public class PersistenceBufferTests
{
    private (PersistenceBuffer Buffer, Mock<IJobRepository> Repo) Make(int flushIntervalMs = 60_000, int maxBatch = 500)
    {
        var repo = new Mock<IJobRepository>(MockBehavior.Strict);
        var buffer = new PersistenceBuffer(
            repo.Object,
            NullLogger<PersistenceBuffer>.Instance,
            flushIntervalMs,
            maxBatch);
        return (buffer, repo);
    }

    private static QueuedJob FakeJob(Guid? id = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            JobType = "FakeJob, FakeAssembly",
            JobKey = $"key_{Guid.NewGuid()}",
            Priority = 0,
            QueuedAt = DateTimeOffset.UtcNow
        };

    // ── Cancel-out (fast-job) path ────────────────────────────────────────────

    [Fact]
    public async Task EnqueueThenComplete_BeforeFlush_NoDbWrites()
    {
        var (buffer, repo) = Make(flushIntervalMs: 60_000); // very long timer — won't fire
        // Repo should never be called
        repo.Setup(r => r.InsertBatchAsync(It.IsAny<IReadOnlyCollection<QueuedJob>>(), It.IsAny<CancellationToken>()))
            .Throws(new Exception("INSERT should not be called"));
        repo.Setup(r => r.DeleteBatchAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .Throws(new Exception("DELETE should not be called"));

        var job = FakeJob();
        buffer.OnEnqueue(job);
        buffer.OnComplete(job.Id);

        // Flush: both buffers should be empty after cancel-out
        repo.Setup(r => r.InsertBatchAsync(It.IsAny<IReadOnlyCollection<QueuedJob>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repo.Setup(r => r.DeleteBatchAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await buffer.FlushNowAsync();

        repo.Verify(r => r.InsertBatchAsync(It.IsAny<IReadOnlyCollection<QueuedJob>>(), It.IsAny<CancellationToken>()), Times.Never);
        repo.Verify(r => r.DeleteBatchAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);

        await buffer.DisposeAsync();
    }

    [Fact]
    public async Task EnqueueOnly_FlushedAsInsert()
    {
        var (buffer, repo) = Make();
        var job = FakeJob();

        IReadOnlyCollection<QueuedJob>? capturedInserts = null;
        repo.Setup(r => r.InsertBatchAsync(It.IsAny<IReadOnlyCollection<QueuedJob>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<QueuedJob>, CancellationToken>((jobs, _) => capturedInserts = jobs)
            .Returns(Task.CompletedTask);

        buffer.OnEnqueue(job);
        await buffer.FlushNowAsync();

        Assert.NotNull(capturedInserts);
        Assert.Contains(capturedInserts!, j => j.Id == job.Id);
        repo.Verify(r => r.DeleteBatchAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);

        await buffer.DisposeAsync();
    }

    [Fact]
    public async Task CompleteOnly_FlushedAsDelete()
    {
        var (buffer, repo) = Make();
        var id = Guid.NewGuid();

        IReadOnlyCollection<Guid>? capturedDeletes = null;
        repo.Setup(r => r.DeleteBatchAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<Guid>, CancellationToken>((ids, _) => capturedDeletes = ids)
            .Returns(Task.CompletedTask);

        buffer.OnComplete(id);
        await buffer.FlushNowAsync();

        Assert.NotNull(capturedDeletes);
        Assert.Contains(capturedDeletes!, i => i == id);
        repo.Verify(r => r.InsertBatchAsync(It.IsAny<IReadOnlyCollection<QueuedJob>>(), It.IsAny<CancellationToken>()), Times.Never);

        await buffer.DisposeAsync();
    }

    [Fact]
    public async Task MultipleEnqueueComplete_MixedOutcome()
    {
        var (buffer, repo) = Make();

        var fastJob = FakeJob();     // enqueue + complete → cancelled out
        var slowJob = FakeJob();     // enqueue only → INSERT
        var oldJob = FakeJob();      // complete only → DELETE

        repo.Setup(r => r.InsertBatchAsync(It.IsAny<IReadOnlyCollection<QueuedJob>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repo.Setup(r => r.DeleteBatchAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        buffer.OnEnqueue(fastJob);
        buffer.OnEnqueue(slowJob);
        buffer.OnComplete(fastJob.Id);  // cancel out
        buffer.OnComplete(oldJob.Id);

        await buffer.FlushNowAsync();

        repo.Verify(r => r.InsertBatchAsync(
            It.Is<IReadOnlyCollection<QueuedJob>>(jobs =>
                jobs.Count == 1 && jobs.GetEnumerator().MoveNext() &&
                !Enumerable.Any(jobs, j => j.Id == fastJob.Id)),
            It.IsAny<CancellationToken>()), Times.Once);

        repo.Verify(r => r.DeleteBatchAsync(
            It.Is<IReadOnlyCollection<Guid>>(ids =>
                Enumerable.Any(ids, i => i == oldJob.Id) &&
                !Enumerable.Any(ids, i => i == fastJob.Id)),
            It.IsAny<CancellationToken>()), Times.Once);

        await buffer.DisposeAsync();
    }

    // ── Force-flush on max batch size ─────────────────────────────────────────

    [Fact]
    public async Task OnEnqueue_MaxBatchReached_TriggersImmediateFlush()
    {
        // Use maxBatch=3 so we hit it quickly
        var (buffer, repo) = Make(flushIntervalMs: 60_000, maxBatch: 3);

        var insertedCount = 0;
        repo.Setup(r => r.InsertBatchAsync(It.IsAny<IReadOnlyCollection<QueuedJob>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<QueuedJob>, CancellationToken>((jobs, _) => insertedCount += jobs.Count)
            .Returns(Task.CompletedTask);

        buffer.OnEnqueue(FakeJob());
        buffer.OnEnqueue(FakeJob());
        buffer.OnEnqueue(FakeJob()); // this triggers the force flush

        // Give the async flush a moment (it's fire-and-forget)
        await Task.Delay(100);

        Assert.Equal(3, insertedCount);

        await buffer.DisposeAsync();
    }

    // ── IsPendingInsert ───────────────────────────────────────────────────────

    [Fact]
    public void IsPendingInsert_AfterEnqueue_ReturnsTrue()
    {
        var (buffer, _) = Make();
        var job = FakeJob();
        buffer.OnEnqueue(job);

        Assert.True(buffer.IsPendingInsert(job.Id));
    }

    [Fact]
    public void IsPendingInsert_AfterCancelOut_ReturnsFalse()
    {
        var (buffer, _) = Make();
        var job = FakeJob();
        buffer.OnEnqueue(job);
        buffer.OnComplete(job.Id); // cancels out

        Assert.False(buffer.IsPendingInsert(job.Id));
    }

    [Fact]
    public void IsPendingInsert_UnknownId_ReturnsFalse()
    {
        var (buffer, _) = Make();
        Assert.False(buffer.IsPendingInsert(Guid.NewGuid()));
    }

    // ── DisposeAsync flush ────────────────────────────────────────────────────

    [Fact]
    public async Task DisposeAsync_FlushesRemainingInserts()
    {
        var (buffer, repo) = Make(flushIntervalMs: 60_000);
        var job = FakeJob();

        IReadOnlyCollection<QueuedJob>? capturedInserts = null;
        repo.Setup(r => r.InsertBatchAsync(It.IsAny<IReadOnlyCollection<QueuedJob>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<QueuedJob>, CancellationToken>((jobs, _) => capturedInserts = jobs)
            .Returns(Task.CompletedTask);

        buffer.OnEnqueue(job);
        await buffer.DisposeAsync(); // should flush

        Assert.NotNull(capturedInserts);
        Assert.Contains(capturedInserts!, j => j.Id == job.Id);
    }
}
