using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Storage;
using Shoko.QueueProcessor.Workers;
using Xunit;

namespace Shoko.QueueProcessor.Tests;

/// <summary>
/// Tests for <see cref="WorkerPool.TryAcquire"/> acquisition logic:
/// priority ordering, ScheduledAt filtering, acquisition-filter exclusions,
/// and orchestrator gate interaction.
/// </summary>
public class WorkerPoolAcquisitionTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static WorkerPool MakePool(Type jobType, IAcquisitionFilter? filter = null)
    {
        var filters = filter != null ? new List<IAcquisitionFilter> { filter } : new List<IAcquisitionFilter>();
        return new WorkerPool("TestPool", maxWorkers: 2, handledTypes: [jobType], acquisitionFilters: filters);
    }

    private static QueuedJob MakeJob(Type jobType, int priority = 0,
        DateTimeOffset? scheduledAt = null, int retryCount = 0) =>
        new()
        {
            Id = Guid.NewGuid(),
            JobType = jobType.FullName + ", " + jobType.Assembly.GetName().Name,
            JobKey = $"TestJob_{Guid.NewGuid()}",
            Priority = priority,
            QueuedAt = DateTimeOffset.UtcNow,
            ScheduledAt = scheduledAt,
            RetryCount = retryCount
        };

    private class JobTypeA : IQueueJob
    {
        public string TypeName => "JobTypeA";
        public string Title => "";
        public Dictionary<string, object> Details => [];
        public void PostInit() { }
        public Task Process() => Task.CompletedTask;
    }

    private class JobTypeB : IQueueJob
    {
        public string TypeName => "JobTypeB";
        public string Title => "";
        public Dictionary<string, object> Details => [];
        public void PostInit() { }
        public Task Process() => Task.CompletedTask;
    }

    // ── Basic acquisition ─────────────────────────────────────────────────────

    [Fact]
    public void TryAcquire_EmptyQueue_ReturnsNull()
    {
        var pool = MakePool(typeof(JobTypeA));
        pool.TryRegisterExecuting = _ => true;

        Assert.Null(pool.TryAcquire());
    }

    [Fact]
    public void TryAcquire_JobInQueue_ReturnsJob()
    {
        var pool = MakePool(typeof(JobTypeA));
        pool.TryRegisterExecuting = _ => true;
        var job = MakeJob(typeof(JobTypeA));
        pool.AddToQueue(job);

        var acquired = pool.TryAcquire();

        Assert.NotNull(acquired);
        Assert.Equal(job.Id, acquired!.Id);
    }

    [Fact]
    public void TryAcquire_JobInQueue_RemovesFromSubQueue()
    {
        var pool = MakePool(typeof(JobTypeA));
        pool.TryRegisterExecuting = _ => true;
        pool.AddToQueue(MakeJob(typeof(JobTypeA)));

        pool.TryAcquire();

        Assert.Equal(0, pool.WaitingCount);
    }

    // ── Priority ordering ─────────────────────────────────────────────────────

    [Fact]
    public void TryAcquire_HighPriorityJob_PickedFirst()
    {
        var pool = MakePool(typeof(JobTypeA));
        pool.TryRegisterExecuting = _ => true;

        var low = MakeJob(typeof(JobTypeA), priority: 0);
        var high = MakeJob(typeof(JobTypeA), priority: 10);
        pool.AddToQueue(low);
        pool.AddToQueue(high);

        var acquired = pool.TryAcquire();

        Assert.Equal(high.Id, acquired!.Id);
    }

    [Fact]
    public void TryAcquire_SamePriority_EarlierJobPickedFirst()
    {
        var pool = MakePool(typeof(JobTypeA));
        pool.TryRegisterExecuting = _ => true;

        var earlier = MakeJob(typeof(JobTypeA));
        Thread.Sleep(5); // ensure different QueuedAt
        var later = MakeJob(typeof(JobTypeA));
        pool.AddToQueue(later);
        pool.AddToQueue(earlier);

        var acquired = pool.TryAcquire();

        Assert.Equal(earlier.Id, acquired!.Id);
    }

    // ── ScheduledAt filtering ─────────────────────────────────────────────────

    [Fact]
    public void TryAcquire_FutureScheduledAt_Skipped()
    {
        var pool = MakePool(typeof(JobTypeA));
        pool.TryRegisterExecuting = _ => true;
        var futureJob = MakeJob(typeof(JobTypeA), scheduledAt: DateTimeOffset.UtcNow.AddHours(1));
        pool.AddToQueue(futureJob);

        Assert.Null(pool.TryAcquire());
    }

    [Fact]
    public void TryAcquire_PastScheduledAt_Acquired()
    {
        var pool = MakePool(typeof(JobTypeA));
        pool.TryRegisterExecuting = _ => true;
        var pastJob = MakeJob(typeof(JobTypeA), scheduledAt: DateTimeOffset.UtcNow.AddSeconds(-10));
        pool.AddToQueue(pastJob);

        var acquired = pool.TryAcquire();

        Assert.NotNull(acquired);
        Assert.Equal(pastJob.Id, acquired!.Id);
    }

    [Fact]
    public void TryAcquire_FutureJobBlockingHighPriority_LowerPriorityReadyJobAcquired()
    {
        var pool = MakePool(typeof(JobTypeA));
        pool.TryRegisterExecuting = _ => true;

        // High-priority job scheduled in the future (should be skipped)
        var futureHigh = MakeJob(typeof(JobTypeA), priority: 100, scheduledAt: DateTimeOffset.UtcNow.AddHours(1));
        // Lower-priority job ready now
        var readyLow = MakeJob(typeof(JobTypeA), priority: 0);
        pool.AddToQueue(futureHigh);
        pool.AddToQueue(readyLow);

        var acquired = pool.TryAcquire();

        // futureHigh is skipped; readyLow is acquired
        Assert.NotNull(acquired);
        Assert.Equal(readyLow.Id, acquired!.Id);
    }

    // ── Acquisition filter exclusions ─────────────────────────────────────────

    [Fact]
    public void TryAcquire_TypeExcludedByFilter_ReturnsNull()
    {
        var filter = new Mock<IAcquisitionFilter>();
        filter.Setup(f => f.GetTypesToExclude()).Returns([typeof(JobTypeA)]);
        filter.Setup(f => f.WatchedAttributeType).Returns((Type?)null);

        var pool = MakePool(typeof(JobTypeA), filter.Object);
        pool.TryRegisterExecuting = _ => true;
        pool.AddToQueue(MakeJob(typeof(JobTypeA)));

        Assert.Null(pool.TryAcquire());
    }

    [Fact]
    public void TryAcquire_FilterExcludesTypeA_TypeBStillAcquired()
    {
        var filter = new Mock<IAcquisitionFilter>();
        filter.Setup(f => f.GetTypesToExclude()).Returns([typeof(JobTypeA)]);
        filter.Setup(f => f.WatchedAttributeType).Returns((Type?)null);

        var pool = new WorkerPool("Mixed", 4, [typeof(JobTypeA), typeof(JobTypeB)], [filter.Object]);
        pool.TryRegisterExecuting = _ => true;

        pool.AddToQueue(MakeJob(typeof(JobTypeA)));  // blocked
        pool.AddToQueue(MakeJob(typeof(JobTypeB)));  // not blocked

        var acquired = pool.TryAcquire();

        Assert.NotNull(acquired);
        Assert.Contains("JobTypeB", acquired!.JobType);
    }

    // ── Orchestrator gate ─────────────────────────────────────────────────────

    [Fact]
    public void TryAcquire_OrchestratorDenies_ReturnsNull()
    {
        var pool = MakePool(typeof(JobTypeA));
        pool.TryRegisterExecuting = _ => false; // orchestrator always denies
        pool.AddToQueue(MakeJob(typeof(JobTypeA)));

        Assert.Null(pool.TryAcquire());
    }

    [Fact]
    public void TryAcquire_OrchestratorDeniesFirst_AcceptsSecond()
    {
        var callCount = 0;
        var pool = MakePool(typeof(JobTypeA));
        pool.TryRegisterExecuting = _ =>
        {
            callCount++;
            return callCount > 1; // deny first call, accept second
        };

        var job1 = MakeJob(typeof(JobTypeA));
        var job2 = MakeJob(typeof(JobTypeA));
        pool.AddToQueue(job1);
        pool.AddToQueue(job2);

        // First TryAcquire: picks job1, orchestrator denies
        // Scan continues to job2, orchestrator accepts
        var acquired = pool.TryAcquire();

        Assert.NotNull(acquired);
    }

    [Fact]
    public void TryAcquire_NoTryRegisterExecuting_ReturnsNull()
    {
        var pool = MakePool(typeof(JobTypeA));
        // TryRegisterExecuting not set (null) — pool not initialized
        pool.AddToQueue(MakeJob(typeof(JobTypeA)));

        Assert.Null(pool.TryAcquire());
    }

    // ── ClearQueue / RetryingCount ────────────────────────────────────────────

    [Fact]
    public void ClearQueue_RemovesAllJobs()
    {
        var pool = MakePool(typeof(JobTypeA));
        pool.AddToQueue(MakeJob(typeof(JobTypeA)));
        pool.AddToQueue(MakeJob(typeof(JobTypeA)));

        pool.ClearQueue();

        Assert.Equal(0, pool.WaitingCount);
    }

    [Fact]
    public void RetryingCount_CountsJobsWithRetryCountAboveZero()
    {
        var pool = MakePool(typeof(JobTypeA));
        pool.AddToQueue(MakeJob(typeof(JobTypeA), retryCount: 0));
        pool.AddToQueue(MakeJob(typeof(JobTypeA), retryCount: 1));
        pool.AddToQueue(MakeJob(typeof(JobTypeA), retryCount: 3));

        Assert.Equal(2, pool.RetryingCount);
    }

    // ── Signal / IdleWorkers ──────────────────────────────────────────────────

    [Fact]
    public void Signal_DoesNotThrow()
    {
        // Signal is a write to a bounded channel — should never throw
        var pool = MakePool(typeof(JobTypeA));
        var ex = Record.Exception(() =>
        {
            for (var i = 0; i < 10; i++) pool.Signal(); // send many signals; channel drops extras
        });
        Assert.Null(ex);
    }
}
