using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Analytics;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Chain;
using Shoko.QueueProcessor.Events;
using Shoko.QueueProcessor.Orchestration;
using Shoko.QueueProcessor.Scheduling;
using Shoko.QueueProcessor.Storage;
using Shoko.QueueProcessor.Workers;
using Xunit;

namespace Shoko.Tests.Queue;

// ── shared test job types ────────────────────────────────────────────────────
// FlagJob uses [JobKeyMember] on Id only, so two enqueues with the same Id but
// different Flag values collide on the same key — which is the scenario the merge
// system is designed to handle.

public class FlagJob : IQueueJob
{
    [JobKeyMember]
    public int Id { get; set; }
    public bool Flag { get; set; }

    public string TypeName => "Flag";
    public string Title => "Flag";
    public Dictionary<string, object> Details => new();
    public void PostInit() { }
    public Task Process() => Task.CompletedTask;
}

// Same shape but also implements IJobMerge: OR-semantics on Flag (false → true, never true → false).
public class MergeableFlagJob : IQueueJob, IJobMerge
{
    [JobKeyMember]
    public int Id { get; set; }
    public bool Flag { get; set; }

    public string TypeName => "MergeableFlag";
    public string Title => "MergeableFlag";
    public Dictionary<string, object> Details => new();
    public void PostInit() { }
    public Task Process() => Task.CompletedTask;

    public bool TryMerge(IQueueJob incoming)
    {
        if (incoming is not MergeableFlagJob other) return false;
        if (!Flag && other.Flag) { Flag = true; return true; }
        return false;
    }
}

// ── WorkerPool.TryGetAndUpdateData ───────────────────────────────────────────

public class WorkerPoolMergeTests
{
    private static WorkerPool CreatePool(Type jobType) =>
        new("test", maxWorkers: 4, workerPriority: 0,
            handledTypes: [jobType], acquisitionFilters: []);

    private static QueuedJob MakeJob(Type type, string key, string? json = null) => new()
    {
        Id = Guid.CreateVersion7(),
        JobType = type.FullName + ", " + type.Assembly.GetName().Name,
        JobKey = key,
        JobDataJson = json,
        Priority = 0,
        QueuedAt = DateTimeOffset.UtcNow,
    };

    [Fact]
    public void TryGetAndUpdateData_NotFound_ReturnsFalse()
    {
        var pool = CreatePool(typeof(FlagJob));

        var found = pool.TryGetAndUpdateData(Guid.NewGuid(), _ => "{}");

        Assert.False(found);
    }

    [Fact]
    public void TryGetAndUpdateData_Found_UpdaterReturnsNull_NoDataChange()
    {
        var pool = CreatePool(typeof(FlagJob));
        var job = MakeJob(typeof(FlagJob), "FlagJob::1", """{"Id":1,"Flag":false}""");
        pool.AddToQueue(job);

        var updaterCalled = false;
        var found = pool.TryGetAndUpdateData(job.Id, json =>
        {
            updaterCalled = true;
            return null;
        });

        Assert.True(found);
        Assert.True(updaterCalled);
        // Returning null means "no change" — the original JSON must be intact.
        var snapshot = pool.GetWaitingSnapshot();
        Assert.Single(snapshot);
        Assert.Equal("""{"Id":1,"Flag":false}""", snapshot[0].JobDataJson);
    }

    [Fact]
    public void TryGetAndUpdateData_Found_UpdaterReturnsNewJson_DataReplaced()
    {
        var pool = CreatePool(typeof(FlagJob));
        var job = MakeJob(typeof(FlagJob), "FlagJob::1", """{"Id":1,"Flag":false}""");
        pool.AddToQueue(job);

        var found = pool.TryGetAndUpdateData(job.Id, _ => """{"Id":1,"Flag":true}""");

        Assert.True(found);
        var snapshot = pool.GetWaitingSnapshot();
        Assert.Single(snapshot);
        Assert.Equal("""{"Id":1,"Flag":true}""", snapshot[0].JobDataJson);
    }
}

// ── PersistenceBuffer.OnUpdate ───────────────────────────────────────────────

public class PersistenceBufferUpdateTests
{
    private static (PersistenceBuffer Buffer, Mock<IJobRepository> Repo) CreateBuffer()
    {
        var repo = new Mock<IJobRepository>();
        repo.Setup(r => r.InsertBatchAsync(It.IsAny<IReadOnlyCollection<QueuedJob>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repo.Setup(r => r.DeleteBatchAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repo.Setup(r => r.UpdateDataBatchAsync(
                It.IsAny<IReadOnlyCollection<(Guid, string?)>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repo.Setup(r => r.ActivateChainChildrenAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sp = new Mock<IServiceProvider>();
        sp.Setup(x => x.GetService(typeof(IJobRepository))).Returns(repo.Object);

        var scope = new Mock<IServiceScope>();
        scope.Setup(x => x.ServiceProvider).Returns(sp.Object);

        var factory = new Mock<IServiceScopeFactory>();
        factory.Setup(f => f.CreateScope()).Returns(scope.Object);

        // High flush threshold + long timer so only explicit FlushNowAsync calls hit the repo.
        var buffer = new PersistenceBuffer(factory.Object, NullLogger<PersistenceBuffer>.Instance,
            flushIntervalMs: 60_000, maxFlushBatch: 10_000);

        return (buffer, repo);
    }

    private static QueuedJob MakeJob(string? json = null) => new()
    {
        Id = Guid.CreateVersion7(),
        JobType = "test",
        JobKey = "test::1",
        JobDataJson = json,
        Priority = 0,
        QueuedAt = DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task OnUpdate_PendingInsert_MutatesInPlace_NoUpdateBatchCall()
    {
        var (buffer, repo) = CreateBuffer();
        var job = MakeJob("""{"Flag":false}""");

        buffer.OnEnqueue(job);
        buffer.OnUpdate(job.Id, """{"Flag":true}""");

        await buffer.FlushNowAsync();

        // INSERT fired with the updated JSON — no separate UpdateDataBatchAsync call.
        repo.Verify(r => r.InsertBatchAsync(
            It.Is<IReadOnlyCollection<QueuedJob>>(jobs =>
                jobs.Count == 1 && CheckSingle(jobs, j => j.JobDataJson == """{"Flag":true}""")),
            It.IsAny<CancellationToken>()), Times.Once);

        repo.Verify(r => r.UpdateDataBatchAsync(
            It.IsAny<IReadOnlyCollection<(Guid, string?)>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // Helper that avoids capturing a ref in a lambda.
    private static bool CheckSingle(IReadOnlyCollection<QueuedJob> jobs, Func<QueuedJob, bool> predicate)
    {
        foreach (var j in jobs)
            if (predicate(j)) return true;
        return false;
    }

    [Fact]
    public async Task OnUpdate_PendingDelete_IsNoOp()
    {
        var (buffer, repo) = CreateBuffer();
        var job = MakeJob("""{"Flag":false}""");

        // Simulate: job was in DB (flush before complete), then completed.
        buffer.OnEnqueue(job);
        await buffer.FlushNowAsync();     // job now "in DB"; _pendingInserts cleared
        repo.Invocations.Clear();         // reset call counters

        buffer.OnComplete(job.Id);        // → _pendingDeletes
        buffer.OnUpdate(job.Id, """{"Flag":true}""");  // should be no-op

        await buffer.FlushNowAsync();

        repo.Verify(r => r.DeleteBatchAsync(
            It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1),
            It.IsAny<CancellationToken>()), Times.Once);

        repo.Verify(r => r.UpdateDataBatchAsync(
            It.IsAny<IReadOnlyCollection<(Guid, string?)>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OnUpdate_AlreadyFlushed_BatchesUpdate_FlushedViaUpdateDataBatch()
    {
        var (buffer, repo) = CreateBuffer();
        var job = MakeJob("""{"Flag":false}""");

        // Job is "in DB" — flush the insert so _pendingInserts is cleared.
        buffer.OnEnqueue(job);
        await buffer.FlushNowAsync();
        repo.Invocations.Clear();

        buffer.OnUpdate(job.Id, """{"Flag":true}""");
        await buffer.FlushNowAsync();

        repo.Verify(r => r.UpdateDataBatchAsync(
            It.Is<IReadOnlyCollection<(Guid Id, string? NewJson)>>(updates =>
                updates.Count == 1 && CheckSingleUpdate(updates, job.Id, """{"Flag":true}""")),
            It.IsAny<CancellationToken>()), Times.Once);

        repo.Verify(r => r.InsertBatchAsync(
            It.IsAny<IReadOnlyCollection<QueuedJob>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static bool CheckSingleUpdate(
        IReadOnlyCollection<(Guid Id, string? NewJson)> updates, Guid id, string json)
    {
        foreach (var u in updates)
            if (u.Id == id && u.NewJson == json) return true;
        return false;
    }

    [Fact]
    public async Task OnComplete_AfterOnUpdate_DeleteSupersedes_NoUpdateBatchCall()
    {
        var (buffer, repo) = CreateBuffer();
        var job = MakeJob("""{"Flag":false}""");

        // Job is in DB.
        buffer.OnEnqueue(job);
        await buffer.FlushNowAsync();
        repo.Invocations.Clear();

        buffer.OnUpdate(job.Id, """{"Flag":true}""");  // → _pendingUpdates
        buffer.OnComplete(job.Id);                     // DELETE supersedes; _pendingUpdates stripped
        await buffer.FlushNowAsync();

        repo.Verify(r => r.DeleteBatchAsync(
            It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1),
            It.IsAny<CancellationToken>()), Times.Once);

        repo.Verify(r => r.UpdateDataBatchAsync(
            It.IsAny<IReadOnlyCollection<(Guid, string?)>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

// ── QueueOrchestrator merge integration ─────────────────────────────────────

public class QueueOrchestratorMergeTests
{
    private static (QueueOrchestrator Orchestrator, QueueScheduler Scheduler, WorkerPool Pool, Mock<IJobRepository> Repo)
        CreateOrchestrator(params Type[] jobTypes)
    {
        var repo = new Mock<IJobRepository>();
        repo.Setup(r => r.InsertBatchAsync(It.IsAny<IReadOnlyCollection<QueuedJob>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repo.Setup(r => r.DeleteBatchAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repo.Setup(r => r.UpdateDataBatchAsync(
                It.IsAny<IReadOnlyCollection<(Guid, string?)>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repo.Setup(r => r.ActivateChainChildrenAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sp = new Mock<IServiceProvider>();
        sp.Setup(x => x.GetService(typeof(IJobRepository))).Returns(repo.Object);

        var scope = new Mock<IServiceScope>();
        scope.Setup(x => x.ServiceProvider).Returns(sp.Object);

        var factory = new Mock<IServiceScopeFactory>();
        factory.Setup(f => f.CreateScope()).Returns(scope.Object);

        var buffer = new PersistenceBuffer(factory.Object, NullLogger<PersistenceBuffer>.Instance,
            flushIntervalMs: 60_000, maxFlushBatch: 10_000);

        var chainRegistry = Mock.Of<IChainScopeRegistry>();
        var concurrency = ConcurrencyRegistry.Build(jobTypes);
        var orchestrator = new QueueOrchestrator(
            NullLogger<QueueOrchestrator>.Instance,
            buffer,
            factory.Object,
            concurrency,
            new RetryPolicyResolver(new RetryPolicy()),
            new QueueMetrics(),
            new QueueStateEventHandler(),
            chainRegistry,
            maxTotalWorkers: 10);

        var pool = new WorkerPool("test", maxWorkers: 4, workerPriority: 0,
            handledTypes: jobTypes, acquisitionFilters: []);

        orchestrator.Initialize([], [pool]);

        var scheduler = new QueueScheduler(orchestrator, chainRegistry);

        return (orchestrator, scheduler, pool, repo);
    }

    private static EnqueueContext BuildContext<T>(Action<T>? configure = null) where T : class, IQueueJob
    {
        var keyBuilder = JobKeyBuilder<T>.Create();
        if (configure != null) keyBuilder.UsingJobData(configure);
        var key = keyBuilder.Build();

        var instance = (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
        configure?.Invoke(instance);

        return new EnqueueContext
        {
            Type = typeof(T),
            Job = new QueuedJob
            {
                Id = Guid.CreateVersion7(),
                JobType = typeof(T).FullName + ", " + typeof(T).Assembly.GetName().Name,
                JobKey = key,
                JobDataJson = JobDataSerializer.Serialize(instance),
                Priority = 0,
                QueuedAt = DateTimeOffset.UtcNow,
            },
            DisplayItem = new QueueItem
            {
                Key = key,
                JobType = typeof(T).Name,
                TypeName = instance.TypeName,
                Title = instance.Title,
                Details = instance.Details,
            },
        };
    }

    [Fact]
    public async Task Collision_WithIJobMerge_MergesData()
    {
        var (orchestrator, _, pool, _) = CreateOrchestrator(typeof(MergeableFlagJob));

        // First enqueue: Flag = false.
        await orchestrator.EnqueueAsync(BuildContext<MergeableFlagJob>(j => { j.Id = 1; j.Flag = false; }));
        // Collision: same key (Id=1), Flag = true → should be merged into the waiting job.
        await orchestrator.EnqueueAsync(BuildContext<MergeableFlagJob>(j => { j.Id = 1; j.Flag = true; }));

        var snapshot = pool.GetWaitingSnapshot();
        Assert.Single(snapshot);

        var merged = (MergeableFlagJob)RuntimeHelpers.GetUninitializedObject(typeof(MergeableFlagJob));
        JobDataSerializer.Apply(merged, snapshot[0].JobDataJson);
        Assert.True(merged.Flag);
    }

    [Fact]
    public async Task Collision_WithIJobMerge_HandlerReturnsFalse_DataUnchanged()
    {
        var (orchestrator, _, pool, _) = CreateOrchestrator(typeof(MergeableFlagJob));

        // MergeableFlagJob.TryMerge uses OR-semantics: it only upgrades false → true.
        // Existing has Flag = true; incoming has Flag = false → TryMerge returns false, no change.
        // If the system incorrectly overwrote with the incoming value, Flag would become false.
        await orchestrator.EnqueueAsync(BuildContext<MergeableFlagJob>(j => { j.Id = 1; j.Flag = true; }));
        await orchestrator.EnqueueAsync(BuildContext<MergeableFlagJob>(j => { j.Id = 1; j.Flag = false; }));

        var snapshot = pool.GetWaitingSnapshot();
        Assert.Single(snapshot);

        var existing = (MergeableFlagJob)RuntimeHelpers.GetUninitializedObject(typeof(MergeableFlagJob));
        JobDataSerializer.Apply(existing, snapshot[0].JobDataJson);
        Assert.True(existing.Flag); // must still be true — the incoming false did not downgrade it
    }

    [Fact]
    public async Task Collision_RegisteredHandler_TakesTypedParameters_AndMergesData()
    {
        var (orchestrator, scheduler, pool, _) = CreateOrchestrator(typeof(FlagJob));

        // RegisterMergeHandler<T> on IQueueScheduler takes a Func<T, T, bool> — both parameters
        // are already the concrete type, requiring no cast inside the lambda.
        scheduler.RegisterMergeHandler<FlagJob>((existing, incoming) =>
        {
            if (!existing.Flag && incoming.Flag) { existing.Flag = true; return true; }
            return false;
        });

        await orchestrator.EnqueueAsync(BuildContext<FlagJob>(j => { j.Id = 1; j.Flag = false; }));
        await orchestrator.EnqueueAsync(BuildContext<FlagJob>(j => { j.Id = 1; j.Flag = true; }));

        var snapshot = pool.GetWaitingSnapshot();
        Assert.Single(snapshot);

        var merged = (FlagJob)RuntimeHelpers.GetUninitializedObject(typeof(FlagJob));
        JobDataSerializer.Apply(merged, snapshot[0].JobDataJson);
        Assert.True(merged.Flag);
    }

    [Fact]
    public async Task Collision_NoHandler_SilentNoOp_OriginalDataUnchanged()
    {
        // FlagJob does NOT implement IJobMerge and no handler is registered.
        var (orchestrator, _, pool, _) = CreateOrchestrator(typeof(FlagJob));

        await orchestrator.EnqueueAsync(BuildContext<FlagJob>(j => { j.Id = 1; j.Flag = false; }));
        await orchestrator.EnqueueAsync(BuildContext<FlagJob>(j => { j.Id = 1; j.Flag = true; }));

        // Still exactly one job, with the original data.
        var snapshot = pool.GetWaitingSnapshot();
        Assert.Single(snapshot);

        var existing = (FlagJob)RuntimeHelpers.GetUninitializedObject(typeof(FlagJob));
        JobDataSerializer.Apply(existing, snapshot[0].JobDataJson);
        Assert.False(existing.Flag);
    }

    [Fact]
    public async Task RegisteredHandler_TakesPriorityOverIJobMerge()
    {
        // MergeableFlagJob implements IJobMerge, but a registered handler should win.
        // The registered handler does the opposite: it sets Flag to false when incoming is false.
        var (orchestrator, _, pool, _) = CreateOrchestrator(typeof(MergeableFlagJob));

        var handlerInvoked = false;
        orchestrator.RegisterMergeHandler(typeof(MergeableFlagJob), (existing, incoming) =>
        {
            handlerInvoked = true;
            var e = (MergeableFlagJob)existing;
            var i = (MergeableFlagJob)incoming;
            // AND-semantics (opposite of IJobMerge's OR): false wins.
            if (e.Flag && !i.Flag) { e.Flag = false; return true; }
            return false;
        });

        // First enqueue: Flag = true.
        await orchestrator.EnqueueAsync(BuildContext<MergeableFlagJob>(j => { j.Id = 1; j.Flag = true; }));
        // Collision: incoming Flag = false → registered handler applies AND-semantics → Flag becomes false.
        await orchestrator.EnqueueAsync(BuildContext<MergeableFlagJob>(j => { j.Id = 1; j.Flag = false; }));

        Assert.True(handlerInvoked);

        var snapshot = pool.GetWaitingSnapshot();
        Assert.Single(snapshot);

        var merged = (MergeableFlagJob)RuntimeHelpers.GetUninitializedObject(typeof(MergeableFlagJob));
        JobDataSerializer.Apply(merged, snapshot[0].JobDataJson);
        // Registered handler ran, not IJobMerge; Flag is now false.
        Assert.False(merged.Flag);
    }
}
