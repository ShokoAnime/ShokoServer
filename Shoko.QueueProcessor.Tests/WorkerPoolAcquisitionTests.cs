using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Acquisition.Attributes;
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
        return new WorkerPool("TestPool", maxWorkers: 2, workerPriority: AcquisitionAttribute.LowestPriority, handledTypes: [jobType], acquisitionFilters: filters);
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

    [Theory]
    [InlineData(3600, false)] // An hour from now
    [InlineData(-10, true)] // Ten seconds ago
    public void TryAcquire_OnlyAcquiresAJobOnceItsScheduledAtHasPassed(int scheduledOffsetSeconds, bool expectedAcquired)
    {
        var pool = MakePool(typeof(JobTypeA));
        pool.TryRegisterExecuting = _ => true;
        var job = MakeJob(typeof(JobTypeA), scheduledAt: DateTimeOffset.UtcNow.AddSeconds(scheduledOffsetSeconds));
        pool.AddToQueue(job);

        var acquired = pool.TryAcquire();

        if (expectedAcquired)
        {
            Assert.NotNull(acquired);
            Assert.Equal(job.Id, acquired!.Id);
        }
        else
        {
            Assert.Null(acquired);
        }
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
    public void TryAcquire_FilterExcludesTypeA_TypeBStillAcquired()
    {
        var filter = new Mock<IAcquisitionFilter>();
        filter.Setup(f => f.GetTypesToExclude()).Returns([typeof(JobTypeA)]);
        filter.Setup(f => f.WatchedAttributeType).Returns((Type?)null);

        var pool = new WorkerPool("Mixed", 4, AcquisitionAttribute.LowestPriority, [typeof(JobTypeA), typeof(JobTypeB)], [filter.Object]);
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

    // ── RunnableCount ─────────────────────────────────────────────────────────

    [Fact]
    public void RunnableCount_AfterAddJob_ReturnsOne()
    {
        var pool = MakePool(typeof(JobTypeA));
        pool.AddToQueue(MakeJob(typeof(JobTypeA)));
        Assert.Equal(1, pool.RunnableCount());
    }

    [Fact]
    public void RunnableCount_FutureScheduledAt_NotCounted()
    {
        var pool = MakePool(typeof(JobTypeA));
        pool.AddToQueue(MakeJob(typeof(JobTypeA), scheduledAt: DateTimeOffset.UtcNow.AddHours(1)));
        Assert.Equal(0, pool.RunnableCount());
    }

    [Fact]
    public void RunnableCount_CappedAtMaxWorkers()
    {
        // MaxWorkers = 2 (set in MakePool); add more jobs than that
        var pool = MakePool(typeof(JobTypeA));
        for (var i = 0; i < 5; i++)
            pool.AddToQueue(MakeJob(typeof(JobTypeA)));

        Assert.Equal(pool.MaxWorkers, pool.RunnableCount(pool.MaxWorkers));
    }

    [Fact]
    public void RunnableCount_AfterRemoveFromQueue_Decremented()
    {
        var pool = MakePool(typeof(JobTypeA));
        var job = MakeJob(typeof(JobTypeA));
        pool.AddToQueue(job);
        pool.AddToQueue(MakeJob(typeof(JobTypeA)));

        pool.RemoveFromQueue(job.Id);

        Assert.Equal(1, pool.RunnableCount());
    }

    [Fact]
    public void RunnableCount_FilterExcludesType_NotCounted()
    {
        var filter = new Mock<IAcquisitionFilter>();
        filter.Setup(f => f.GetTypesToExclude()).Returns([typeof(JobTypeA)]);
        filter.Setup(f => f.WatchedAttributeType).Returns((Type?)null);

        var pool = MakePool(typeof(JobTypeA), filter.Object);
        pool.AddToQueue(MakeJob(typeof(JobTypeA)));

        Assert.Equal(0, pool.RunnableCount());
    }

    // ── Holds ─────────────────────────────────────────────────────────────────

    [Fact]
    public void TryAcquire_HeldPool_WaitsUntilReleased()
    {
        var pool = MakePool(typeof(JobTypeA));
        pool.TryRegisterExecuting = _ => true;
        var job = MakeJob(typeof(JobTypeA));
        pool.AddToQueue(job);

        pool.SetHold(true, new HashSet<string>());
        Assert.Null(pool.TryAcquire());
        Assert.True(pool.GetStatus().IsHeld);
        Assert.Equal(1, pool.GetStatus().BlockedCount);

        pool.SetHold(false, new HashSet<string>());
        Assert.Same(job, pool.TryAcquire());
    }

    [Fact]
    public void TryAcquire_HeldJobType_LeavesTheOtherTypesRunning()
    {
        var pool = new WorkerPool("TestPool", maxWorkers: 2, workerPriority: AcquisitionAttribute.LowestPriority, handledTypes: [typeof(JobTypeA), typeof(JobTypeB)], acquisitionFilters: []);
        pool.TryRegisterExecuting = _ => true;
        var held = MakeJob(typeof(JobTypeA), priority: 10);
        var other = MakeJob(typeof(JobTypeB));
        pool.AddToQueue(held);
        pool.AddToQueue(other);

        pool.SetHold(false, new HashSet<string> { Builder.JobTypeNames.Key(typeof(JobTypeA)) });

        Assert.Same(other, pool.TryAcquire());
        Assert.Null(pool.TryAcquire());
        Assert.Equal([Builder.JobTypeNames.Key(typeof(JobTypeA))], pool.GetStatus().HeldTypeNames);
    }

    [Fact]
    public void QueueHolds_RaisesChangedOnlyWhenTheHoldsChange()
    {
        var holds = new Scheduling.QueueHolds();
        var raised = 0;
        holds.Changed += (_, _) => raised++;

        Assert.True(holds.SetPoolHeld("Default", true));
        Assert.False(holds.SetPoolHeld("default", true));
        Assert.True(holds.SetJobTypeHeld("DownloadImageJob", true));
        holds.Set(["DEFAULT"], ["DownloadImageJob"]);

        Assert.Equal(2, raised);
        Assert.True(holds.HeldPools.Contains("default"));
    }

    [Fact]
    public void QueueHolds_OnceThePoolsAreKnown_RefuseUnknownNamesButKeepRestoredOnes()
    {
        var holds = new Scheduling.QueueHolds();
        holds.Set(["Gone"], ["GoneJob"]);

        var (pools, jobTypes) = holds.SetKnown(["Default"], ["DownloadImageJob"]);

        Assert.Equal(["Gone"], pools);
        Assert.Equal(["GoneJob"], jobTypes);
        Assert.Throws<ArgumentException>(() => holds.SetPoolHeld("NoSuchPool", true));
        Assert.Throws<ArgumentException>(() => holds.SetJobTypeHeld("NoSuchJob", true));
        Assert.True(holds.SetPoolHeld("default", true));
        Assert.True(holds.SetPoolHeld("Gone", false));
        Assert.True(holds.SetJobTypeHeld("GoneJob", false));
    }
}
