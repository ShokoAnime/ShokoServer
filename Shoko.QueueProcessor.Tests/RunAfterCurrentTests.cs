using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Analytics;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Chain;
using Shoko.QueueProcessor.Events;
using Shoko.QueueProcessor.Orchestration;
using Shoko.QueueProcessor.Storage;
using Shoko.QueueProcessor.Workers;
using Xunit;

namespace Shoko.QueueProcessor.Tests;

/// <summary>
/// Tests for <see cref="QueueOrchestrator.RegisterAfterParent"/>: deferred child jobs are held
/// until the parent completes, then released at maximum priority.
/// </summary>
public class RunAfterCurrentTests
{
    // ── Fixture job types ─────────────────────────────────────────────────────

    [JobKeyMember("ParentJob")]
    private class ParentJob : IQueueJob
    {
        public string TypeName => "ParentJob";
        public string Title => "";
        public Dictionary<string, object> Details => [];
        public void PostInit() { }
        public Task Process() => Task.CompletedTask;
    }

    [JobKeyMember("ChildJob")]
    private class ChildJob : IQueueJob
    {
        public string TypeName => "ChildJob";
        public string Title => "";
        public Dictionary<string, object> Details => [];
        public void PostInit() { }
        public Task Process() => Task.CompletedTask;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static (QueueOrchestrator Orchestrator, WorkerPool ParentPool, WorkerPool ChildPool) MakeSetup(int maxWorkers = 10)
    {
        var repo = new Mock<IJobRepository>();
        repo.Setup(r => r.InsertBatchAsync(It.IsAny<IReadOnlyCollection<QueuedJob>>(), default)).Returns(Task.CompletedTask);
        repo.Setup(r => r.DeleteBatchAsync(It.IsAny<IReadOnlyCollection<Guid>>(), default)).Returns(Task.CompletedTask);
        repo.Setup(r => r.UpdateRetryAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<DateTimeOffset>(), default)).Returns(Task.CompletedTask);
        repo.Setup(r => r.DeleteAsync(It.IsAny<Guid>(), default)).Returns(Task.CompletedTask);

        var scopeFactory = MakeScopeFactory(repo);
        var buffer = new PersistenceBuffer(scopeFactory, NullLogger<PersistenceBuffer>.Instance, flushIntervalMs: int.MaxValue);
        var registry = new ConcurrencyRegistry(
            new Dictionary<Type, int>(),
            new Dictionary<Type, string?>(),
            new Dictionary<string, int>());
        var retry = new RetryPolicyResolver(new RetryPolicy { MaxRetries = 0 });
        var chainScopeRegistry = new ChainScopeRegistry(scopeFactory);
        var orchestrator = new QueueOrchestrator(
            NullLogger<QueueOrchestrator>.Instance,
            buffer, scopeFactory, registry, retry,
            new QueueMetrics(), new QueueStateEventHandler(), chainScopeRegistry, maxWorkers);

        var parentPool = new WorkerPool("ParentPool", 2, AcquisitionAttribute.LowestPriority, [typeof(ParentJob)], []);
        var childPool = new WorkerPool("ChildPool", 2, AcquisitionAttribute.LowestPriority, [typeof(ChildJob)], []);

        orchestrator.Initialize([], [parentPool, childPool]);

        return (orchestrator, parentPool, childPool);
    }

    private static IServiceScopeFactory MakeScopeFactory(Mock<IJobRepository> repo)
    {
        var sp = new Mock<IServiceProvider>();
        sp.Setup(s => s.GetService(typeof(IJobRepository))).Returns(repo.Object);
        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(sp.Object);
        var factory = new Mock<IServiceScopeFactory>();
        factory.Setup(f => f.CreateScope()).Returns(scope.Object);
        return factory.Object;
    }

    private static QueuedJob MakeJob(Type jobType, string? jobKey = null, int priority = 0) => new()
    {
        Id = Guid.NewGuid(),
        JobType = jobType.FullName + ", " + jobType.Assembly.GetName().Name,
        JobKey = jobKey ?? $"key_{Guid.NewGuid()}",
        Priority = priority,
        QueuedAt = DateTimeOffset.UtcNow,
    };

    private static EnqueueContext MakeChildContext(string? jobKey = null, int priority = int.MaxValue)
    {
        var key = jobKey ?? JobKeyBuilder<ChildJob>.Create().Build();
        var instance = new ChildJob();
        return new EnqueueContext
        {
            Type = typeof(ChildJob),
            Job = new QueuedJob
            {
                Id = Guid.NewGuid(),
                JobType = typeof(ChildJob).FullName + ", " + typeof(ChildJob).Assembly.GetName().Name,
                JobKey = key,
                JobDataJson = null,
                Priority = priority,
                QueuedAt = DateTimeOffset.UtcNow,
            },
            DisplayItem = new QueueItem
            {
                Key = key,
                JobType = nameof(ChildJob),
                TypeName = instance.TypeName,
                Title = instance.Title,
                Details = instance.Details,
            }
        };
    }

    // Simulate acquiring and registering a job as executing in the orchestrator
    private static Guid AcquireAsExecuting(QueueOrchestrator orchestrator, WorkerPool pool, QueuedJob job)
    {
        pool.AddToQueue(job);
        var acquired = pool.TryAcquire();
        Assert.NotNull(acquired);
        return acquired!.Id;
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void RegisterAfterParent_NewJob_HeldUntilParentComplete()
    {
        var (orchestrator, parentPool, childPool) = MakeSetup();

        var parentJob = MakeJob(typeof(ParentJob));
        var parentId = AcquireAsExecuting(orchestrator, parentPool, parentJob);
        var childCtx = MakeChildContext();

        orchestrator.RegisterAfterParent(parentId, childCtx);

        // Child should NOT be in the pool yet
        Assert.Equal(0, childPool.WaitingCount);
        Assert.True(orchestrator.IsQueued(childCtx.Job.JobKey));

        // After parent completes, child should appear at int.MaxValue
        orchestrator.OnComplete(parentId);

        Assert.Equal(1, childPool.WaitingCount);
        var waiting = childPool.GetWaitingSnapshot();
        Assert.Equal(int.MaxValue, waiting[0].Priority);
    }

    [Fact]
    public async Task RegisterAfterParent_WaitingJob_RemovedFromQueueThenRestoredAtMaxPriority()
    {
        var (orchestrator, parentPool, childPool) = MakeSetup();

        // Enqueue child at normal priority through the proper path so the key is in _jobKeyIndex
        var childJobKey = JobKeyBuilder<ChildJob>.Create().Build();
        await orchestrator.EnqueueAsync(MakeChildContext(jobKey: childJobKey, priority: 0));
        Assert.Equal(1, childPool.WaitingCount);

        var parentJob = MakeJob(typeof(ParentJob));
        var parentId = AcquireAsExecuting(orchestrator, parentPool, parentJob);

        // RegisterAfterParent with the same key — should pull the waiting job
        orchestrator.RegisterAfterParent(parentId, MakeChildContext(jobKey: childJobKey));

        // Child should have been pulled from pool
        Assert.Equal(0, childPool.WaitingCount);

        orchestrator.OnComplete(parentId);

        // Child re-inserted at int.MaxValue
        Assert.Equal(1, childPool.WaitingCount);
        Assert.Equal(int.MaxValue, childPool.GetWaitingSnapshot()[0].Priority);
    }

    [Fact]
    public async Task RegisterAfterParent_ExecutingJob_NoOp()
    {
        var (orchestrator, parentPool, childPool) = MakeSetup();

        var parentJob = MakeJob(typeof(ParentJob));
        var parentId = AcquireAsExecuting(orchestrator, parentPool, parentJob);

        // Enqueue and acquire child through the proper path so its key is in _jobKeyIndex
        var childJobKey = JobKeyBuilder<ChildJob>.Create().Build();
        await orchestrator.EnqueueAsync(MakeChildContext(jobKey: childJobKey));
        childPool.TryAcquire();  // moves from sub-queue to _executingSet; key stays in _jobKeyIndex

        // RegisterAfterParent should be a no-op since child is executing
        orchestrator.RegisterAfterParent(parentId, MakeChildContext(jobKey: childJobKey));

        orchestrator.OnComplete(parentId);

        // Nothing deferred was released — child was executing, not held
        Assert.Equal(0, childPool.WaitingCount);
    }

    [Fact]
    public void RegisterAfterParent_Dedup_SameKeyMultipleCalls_OnlyOneChild()
    {
        var (orchestrator, parentPool, childPool) = MakeSetup();

        var parentJob = MakeJob(typeof(ParentJob));
        var parentId = AcquireAsExecuting(orchestrator, parentPool, parentJob);

        var jobKey = JobKeyBuilder<ChildJob>.Create().Build();

        // Register the same key multiple times
        orchestrator.RegisterAfterParent(parentId, MakeChildContext(jobKey: jobKey));
        orchestrator.RegisterAfterParent(parentId, MakeChildContext(jobKey: jobKey));
        orchestrator.RegisterAfterParent(parentId, MakeChildContext(jobKey: jobKey));

        orchestrator.OnComplete(parentId);

        // Only one child job should be in the pool
        Assert.Equal(1, childPool.WaitingCount);
    }

    [Fact]
    public async Task RegisterAfterParent_Dedup_KeyBlocksEarlyEnqueue()
    {
        var (orchestrator, parentPool, childPool) = MakeSetup();

        var parentJob = MakeJob(typeof(ParentJob));
        var parentId = AcquireAsExecuting(orchestrator, parentPool, parentJob);

        var jobKey = JobKeyBuilder<ChildJob>.Create().Build();
        orchestrator.RegisterAfterParent(parentId, MakeChildContext(jobKey: jobKey));

        // A concurrent Enqueue for the same key should be deduplicated (no-op)
        await orchestrator.EnqueueAsync(MakeChildContext(jobKey: jobKey));

        orchestrator.OnComplete(parentId);

        // Still only one child in pool
        Assert.Equal(1, childPool.WaitingCount);
    }

    [Fact]
    public async Task RegisterAfterParent_ParentRealFailure_ChildDiscarded()
    {
        var (orchestrator, parentPool, childPool) = MakeSetup();

        var parentJob = MakeJob(typeof(ParentJob));
        var parentId = AcquireAsExecuting(orchestrator, parentPool, parentJob);
        var childCtx = MakeChildContext();

        orchestrator.RegisterAfterParent(parentId, childCtx);
        Assert.True(orchestrator.IsQueued(childCtx.Job.JobKey));

        // Real failure (incrementRetry: true, but MaxRetries=0 → discard)
        await orchestrator.OnFailureAsync(parentId, new Exception("test failure"), incrementRetry: true);

        // Child should have been discarded and key freed
        Assert.Equal(0, childPool.WaitingCount);
        Assert.False(orchestrator.IsQueued(childCtx.Job.JobKey));
    }

    [Fact]
    public async Task RegisterAfterParent_ParentRequeue_ChildPreservedAndFiresOnSuccess()
    {
        var (orchestrator, parentPool, childPool) = MakeSetup(maxWorkers: 10);

        var parentJob = MakeJob(typeof(ParentJob));
        var parentId = AcquireAsExecuting(orchestrator, parentPool, parentJob);
        var childCtx = MakeChildContext();

        orchestrator.RegisterAfterParent(parentId, childCtx);

        // Transient failure — parent re-queues with same ID (incrementRetry: false)
        await orchestrator.OnFailureAsync(parentId, new Exception("transient"), incrementRetry: false);

        // Child registration preserved; child not yet in pool
        Assert.Equal(0, childPool.WaitingCount);

        // Parent runs again (same ID) and completes successfully.
        // Directly register so orchestrator knows it's executing again.
        orchestrator.TryRegisterExecuting(new QueuedJob
        {
            Id = parentId,
            JobType = parentJob.JobType,
            JobKey = parentJob.JobKey,
            Priority = parentJob.Priority,
            QueuedAt = DateTimeOffset.UtcNow,
        });

        orchestrator.OnComplete(parentId);

        // Child should now be in pool
        Assert.Equal(1, childPool.WaitingCount);
        Assert.Equal(int.MaxValue, childPool.GetWaitingSnapshot()[0].Priority);
    }

    [Fact]
    public async Task ClearAsync_DiscardsAfterParentRegistrations()
    {
        var (orchestrator, parentPool, childPool) = MakeSetup();

        var parentJob = MakeJob(typeof(ParentJob));
        var parentId = AcquireAsExecuting(orchestrator, parentPool, parentJob);
        var childCtx = MakeChildContext();

        orchestrator.RegisterAfterParent(parentId, childCtx);
        Assert.True(orchestrator.IsQueued(childCtx.Job.JobKey));

        await orchestrator.ClearAsync();

        // Child key should be freed after clear
        Assert.False(orchestrator.IsQueued(childCtx.Job.JobKey));
    }

    [Fact]
    public void RegisterAfterParent_RaceParentAlreadyComplete_ImmediateEnqueue()
    {
        var (orchestrator, parentPool, childPool) = MakeSetup();

        // Parent runs and completes before RegisterAfterParent is called
        var parentJob = MakeJob(typeof(ParentJob));
        var parentId = AcquireAsExecuting(orchestrator, parentPool, parentJob);
        orchestrator.OnComplete(parentId);

        var childCtx = MakeChildContext();

        // Register after parent is already done — should enqueue immediately
        orchestrator.RegisterAfterParent(parentId, childCtx);

        Assert.Equal(1, childPool.WaitingCount);
        Assert.Equal(int.MaxValue, childPool.GetWaitingSnapshot()[0].Priority);
    }
}
