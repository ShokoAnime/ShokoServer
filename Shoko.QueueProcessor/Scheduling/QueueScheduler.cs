using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Chain;
using Shoko.QueueProcessor.Orchestration;
using Shoko.QueueProcessor.Storage;
using Shoko.QueueProcessor.Workers;

namespace Shoko.QueueProcessor.Scheduling;

/// <summary>
/// Thin facade over <see cref="QueueOrchestrator"/> that implements <see cref="IQueueScheduler"/>.
/// Handles job construction (type name, key, data) and delegates state to the orchestrator.
/// </summary>
public sealed class QueueScheduler : IQueueScheduler
{
    private readonly QueueOrchestrator _orchestrator;
    private readonly IChainScopeRegistry _chainScopeRegistry;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IJobActorAccessor? _actorAccessor;

    public QueueScheduler(QueueOrchestrator orchestrator, IChainScopeRegistry chainScopeRegistry,
        IServiceScopeFactory scopeFactory, IJobActorAccessor? actorAccessor = null)
    {
        _orchestrator = orchestrator;
        _chainScopeRegistry = chainScopeRegistry;
        _scopeFactory = scopeFactory;
        _actorAccessor = actorAccessor;
    }

    public bool IsPaused => _orchestrator.IsPaused;

    public Task Enqueue<T>(Action<T>? configure = null, bool prioritize = false,
        DateTimeOffset? scheduledAt = null, CancellationToken ct = default)
        where T : class, IQueueJob
        => EnqueueWithPriority(configure, QueuePriority.For(typeof(T), prioritize), scheduledAt, ct);

    public Task EnqueueWithPriority<T>(Action<T>? configure, int priority, DateTimeOffset? scheduledAt = null, CancellationToken ct = default)
        where T : class, IQueueJob
    {
        // Build key: uses [JobKeyMember] annotations or all primitive properties
        var keyBuilder = JobKeyBuilder<T>.Create();
        if (configure != null) keyBuilder.UsingJobData(configure);
        var key = keyBuilder.Build();

        // Built through DI, as the worker builds it: BuildContext reads Title, Details and
        // TypeName off the instance, and those may read injected services. The scope has to
        // outlive that read. Only public read/write properties are serialised, so nothing
        // the constructor injects reaches the stored job data.
        using var scope = _scopeFactory.CreateScope();
        var instance = (T)scope.ServiceProvider.GetRequiredService(typeof(T));
        configure?.Invoke(instance);

        return _orchestrator.EnqueueAsync(
            BuildContext(typeof(T), key, instance, priority, scheduledAt, _actorAccessor?.Capture()),
            ct);
    }

    public async Task EnqueueImmediate<T>(
        Action<T>? configure = null,
        Func<Exception?, Task>? onComplete = null,
        CancellationToken ct = default)
        where T : class, IQueueJob
    {
        if (_orchestrator.IsJobTypeBlocked(typeof(T)))
            throw new JobBlockedException(typeof(T));

        var keyBuilder = JobKeyBuilder<T>.Create();
        if (configure != null) keyBuilder.UsingJobData(configure);
        var key = keyBuilder.Build();

        using var scope = _scopeFactory.CreateScope();
        var instance = (T)scope.ServiceProvider.GetRequiredService(typeof(T));
        configure?.Invoke(instance);

        var completionTask = _orchestrator.PrepareAndEnqueueImmediate(
            BuildContext(typeof(T), key, instance, QueuePriority.Immediate, null, _actorAccessor?.Capture()));

        if (onComplete != null)
            completionTask = completionTask.ContinueWith(
                t => onComplete(t.IsFaulted ? t.Exception!.InnerException ?? t.Exception : t.IsCanceled ? new OperationCanceledException("The job was cancelled.") : null),
                ct, TaskContinuationOptions.None, TaskScheduler.Default).Unwrap();

        await completionTask.WaitAsync(ct);
    }

    public Task RunAfterCurrent<T>(Action<T>? configure = null, CancellationToken ct = default)
        where T : class, IQueueJob
    {
        var parentId = SubExecutionTracker.CurrentJobId.Value;

        var keyBuilder = JobKeyBuilder<T>.Create();
        if (configure != null) keyBuilder.UsingJobData(configure);
        var key = keyBuilder.Build();

        using var scope = _scopeFactory.CreateScope();
        var instance = (T)scope.ServiceProvider.GetRequiredService(typeof(T));
        configure?.Invoke(instance);

        if (parentId == Guid.Empty)
            return _orchestrator.EnqueueAsync(
                BuildContext(typeof(T), key, instance, QueuePriority.For(typeof(T), prioritize: true), null, _actorAccessor?.Capture()),
                ct
            );

        _orchestrator.RegisterAfterParent(parentId, BuildContext(typeof(T), key, instance, QueuePriority.Immediate, null, _actorAccessor?.Capture()));
        return Task.CompletedTask;
    }

    public Task EnqueueRange(
        IEnumerable<(Type JobType, string JobKey, string DataJson, int Priority, DateTimeOffset? ScheduledAt)> jobs,
        CancellationToken ct = default)
    {
        var contexts = new List<EnqueueContext>();
        using var scope = _scopeFactory.CreateScope();
        foreach (var (jobType, jobKey, dataJson, priority, scheduledAt) in jobs)
        {
            if (_orchestrator.IsQueued(jobKey)) continue;

            // No live instance was provided — rehydrate one to read TypeName/Title/Details once.
            // We still serialize from `instance` rather than reuse `dataJson` so the canonical
            // round-trip happens in one place.
            var instance = (IQueueJob)scope.ServiceProvider.GetRequiredService(jobType);
            JobDataSerializer.Apply(instance, dataJson);

            contexts.Add(BuildContext(jobType, jobKey, instance, priority, scheduledAt, _actorAccessor?.Capture()));
        }
        return _orchestrator.EnqueueRangeAsync(contexts, ct);
    }

    /// <summary>
    /// Single place that assembles a full <see cref="EnqueueContext"/> from a live
    /// <see cref="IQueueJob"/> instance: persisted <see cref="QueuedJob"/> + display
    /// <see cref="QueueItem"/> + pre-resolved <see cref="Type"/>. PoolName is left blank — the
    /// orchestrator stamps it from pool routing. The actor is captured by the caller, in the
    /// flow that queued the job.
    /// </summary>
    internal static EnqueueContext BuildContext(Type type, string jobKey, IQueueJob instance, int priority, DateTimeOffset? scheduledAt, JobActor? actor = null)
    {
        var asmQualified = JobTypeNames.Stored(type);
        var shortTypeName = JobTypeNames.Short(type);
        return new EnqueueContext
        {
            Type = type,
            Job = new QueuedJob
            {
                Id = Guid.CreateVersion7(),
                JobType = string.Intern(asmQualified),
                JobKey = jobKey,
                JobDataJson = JobDataSerializer.Serialize(instance),
                Priority = priority,
                QueuedAt = DateTimeOffset.UtcNow,
                ScheduledAt = scheduledAt,
                Actor = actor,
            },
            DisplayItem = new QueueItem
            {
                Key = jobKey,
                JobType = shortTypeName,
                TypeName = string.IsNullOrEmpty(instance.TypeName) ? shortTypeName : instance.TypeName,
                Title = instance.Title,
                Details = instance.Details,
                RetryCount = 0
            }
        };
    }

    public IJobChainBuilder CreateJobChain() => new JobChainBuilder(_orchestrator, _chainScopeRegistry, _scopeFactory, _actorAccessor);

    public bool IsJobTypeBlocked(Type jobType) => _orchestrator.IsJobTypeBlocked(jobType);

    public Task Enqueue(Type jobType, Action<IQueueJob>? configure = null, bool prioritize = false)
        => EnqueueWithPriority(jobType, configure, QueuePriority.For(jobType, prioritize));

    public Task EnqueueWithPriority(Type jobType, Action<IQueueJob>? configure, int priority)
    {
        var data = JobDataSerializer.DiffFromDefaultUntyped(jobType, configure);
        var key = JobKeyBuilder<IQueueJob>.BuildForType(jobType, data);

        using var scope = _scopeFactory.CreateScope();
        var instance = (IQueueJob)scope.ServiceProvider.GetRequiredService(jobType);
        configure?.Invoke(instance);

        return _orchestrator.EnqueueAsync(
            BuildContext(jobType, key, instance, priority, null, _actorAccessor?.Capture()));
    }

    public Task RunAfterCurrent(Type jobType, Action<IQueueJob>? configure = null)
    {
        var parentId = SubExecutionTracker.CurrentJobId.Value;

        var data = JobDataSerializer.DiffFromDefaultUntyped(jobType, configure);
        var key = JobKeyBuilder<IQueueJob>.BuildForType(jobType, data);

        using var scope = _scopeFactory.CreateScope();
        var instance = (IQueueJob)scope.ServiceProvider.GetRequiredService(jobType);
        configure?.Invoke(instance);

        if (parentId == Guid.Empty)
            return _orchestrator.EnqueueAsync(
                BuildContext(jobType, key, instance, QueuePriority.For(jobType, prioritize: true), null, _actorAccessor?.Capture())
            );

        _orchestrator.RegisterAfterParent(parentId, BuildContext(jobType, key, instance, QueuePriority.Immediate, null, _actorAccessor?.Capture()));
        return Task.CompletedTask;
    }

    public Task Remove(string jobKey, CancellationToken ct = default)
        => _orchestrator.RemoveAsync(jobKey, ct);

    public Task<JobCancellationResult> Cancel(string jobKey, CancellationToken ct = default)
        => _orchestrator.CancelAsync(jobKey, ct);

    public Task Remove<T>(Action<T>? configure = null, CancellationToken ct = default)
        where T : class, IQueueJob
    {
        var keyBuilder = JobKeyBuilder<T>.Create();
        if (configure != null) keyBuilder.UsingJobData(configure);
        return _orchestrator.RemoveAsync(keyBuilder.Build(), ct);
    }

    public Task Clear(CancellationToken ct = default) => _orchestrator.ClearAsync(ct);

    public Task Pause() { _orchestrator.Pause(); return Task.CompletedTask; }

    public Task Resume() { _orchestrator.Resume(); return Task.CompletedTask; }

    public Task Halt(string reason) { _orchestrator.Halt(reason); return Task.CompletedTask; }

    public bool IsHalted => _orchestrator.IsHalted;

    public Task<QueueState> GetState(int maxWaiting = 100, int offset = 0,
        bool includeBlocked = true, CancellationToken ct = default)
    {
        var poolStatus = _orchestrator.GetPoolStatus();
        var metrics = _orchestrator.GetMetrics();

        return Task.FromResult(new QueueState
        {
            TotalWaiting = _orchestrator.WaitingCount,
            TotalExecuting = _orchestrator.ExecutingCount,
            MaxWorkers = 0, // filled by WorkerPoolManager if needed
            IsPaused = _orchestrator.IsPaused,
            PoolStatus = poolStatus,
            Metrics = metrics
        });
    }

    public bool IsQueued(string jobKey) => _orchestrator.IsQueued(jobKey);

    public void RegisterMergeHandler<T>(Func<T, T, bool> handler) where T : class, IQueueJob
        => _orchestrator.RegisterMergeHandler(typeof(T), (e, i) => handler((T)e, (T)i));
}

/// <summary>
/// Sequential job chain builder. Builds contexts up-front then submits them to the orchestrator
/// so that all parent-child relationships are established before any job starts executing.
/// </summary>
internal sealed class JobChainBuilder : IJobChainBuilder
{
    private readonly QueueOrchestrator _orchestrator;
    private readonly IChainScopeRegistry _chainScopeRegistry;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly List<EnqueueContext> _entries = [];
    private readonly Guid _chainId = Guid.CreateVersion7();
    private readonly JobActor? _actor;

    internal JobChainBuilder(QueueOrchestrator orchestrator, IChainScopeRegistry chainScopeRegistry,
        IServiceScopeFactory scopeFactory, IJobActorAccessor? actorAccessor = null)
    {
        _orchestrator = orchestrator;
        _chainScopeRegistry = chainScopeRegistry;
        _scopeFactory = scopeFactory;
        // Captured once, when the chain is created: every job of the chain runs for the same actor.
        _actor = actorAccessor?.Capture();
    }

    public IJobChainBuilder Then<T>(Action<T>? configure = null) where T : class, IQueueJob
    {
        var keyBuilder = JobKeyBuilder<T>.Create();
        if (configure != null) keyBuilder.UsingJobData(configure);
        var key = keyBuilder.Build();

        using var scope = _scopeFactory.CreateScope();
        var instance = (T)scope.ServiceProvider.GetRequiredService(typeof(T));
        configure?.Invoke(instance);

        var ctx = QueueScheduler.BuildContext(typeof(T), key, instance, QueuePriority.Immediate, null, _actor);
        ctx.Job.ChainId = _chainId;
        ctx.Job.IsChainFinally = typeof(T).GetCustomAttribute<ChainFinallyAttribute>() != null;
        _entries.Add(ctx);
        return this;
    }

    public IJobChainBuilder Then(Type jobType, Action<IQueueJob>? configure = null)
    {
        var data = JobDataSerializer.DiffFromDefaultUntyped(jobType, configure);
        var key = JobKeyBuilder<IQueueJob>.BuildForType(jobType, data);

        using var scope = _scopeFactory.CreateScope();
        var instance = (IQueueJob)scope.ServiceProvider.GetRequiredService(jobType);
        configure?.Invoke(instance);

        var ctx = QueueScheduler.BuildContext(jobType, key, instance, QueuePriority.Immediate, null, _actor);
        ctx.Job.ChainId = _chainId;
        ctx.Job.IsChainFinally = jobType.GetCustomAttribute<ChainFinallyAttribute>() != null;
        _entries.Add(ctx);
        return this;
    }

    public async Task EnqueueAfterCurrent()
    {
        if (_entries.Count == 0) return;

        SeedChainScope();

        var parentId = SubExecutionTracker.CurrentJobId.Value;
        if (parentId == Guid.Empty)
        {
            // As RunAfterCurrent does outside a worker: the head is queued as prioritized.
            await EnqueueHead(prioritize: true);
            return;
        }

        _orchestrator.RegisterAfterParent(parentId, _entries[0]);
        for (var i = 1; i < _entries.Count; i++)
            _orchestrator.RegisterChainAfterJob(_entries[i - 1].Job.Id, _entries[i]);
    }

    public async Task Enqueue()
    {
        if (_entries.Count == 0) return;

        SeedChainScope();

        await EnqueueHead(prioritize: false);
    }

    /// <summary>
    /// Queues the head of the chain at its type's priority and registers the rest to follow
    /// it, each at <see cref="QueuePriority.Immediate"/> once its parent completes.
    /// </summary>
    /// <param name="prioritize">Whether the head is queued with its prioritized priority.</param>
    /// <returns>A task that completes once the chain is queued.</returns>
    /// <exception cref="InvalidOperationException">The head type's <see cref="JobPriorityAttribute"/> is invalid.</exception>
    private async Task EnqueueHead(bool prioritize)
    {
        var head = _entries[0];
        head.Job.Priority = QueuePriority.For(head.Type, prioritize);
        await _orchestrator.EnqueueAsync(head);
        for (var i = 1; i < _entries.Count; i++)
            _orchestrator.RegisterChainAfterJob(_entries[i - 1].Job.Id, _entries[i]);
    }

    private void SeedChainScope()
    {
        if (_entries.Count == 0) return;
        _chainScopeRegistry.GetOrCreateChainScope(_chainId);
    }
}
