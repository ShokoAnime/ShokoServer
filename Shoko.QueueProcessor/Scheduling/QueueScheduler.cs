using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Builder;
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

    public QueueScheduler(QueueOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public bool IsPaused => _orchestrator.IsPaused;

    public Task Enqueue<T>(Action<T>? configure = null, bool prioritize = false,
        DateTimeOffset? scheduledAt = null, CancellationToken ct = default)
        where T : class, IQueueJob
    {
        // Build key: uses [JobKeyMember] annotations or all primitive properties
        var keyBuilder = JobKeyBuilder<T>.Create();
        if (configure != null) keyBuilder.UsingJobData(configure);
        var key = keyBuilder.Build();

        if (_orchestrator.IsQueued(key))
            return Task.CompletedTask;

        // RuntimeHelpers.GetUninitializedObject bypasses constructors so jobs are not required
        // to have a parameterless constructor — injected services are constructor parameters,
        // not settable properties, and are irrelevant for serialisation.
        IQueueJob instance = (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
        configure?.Invoke((T)instance);

        return _orchestrator.EnqueueAsync(
            BuildContext(typeof(T), key, instance, prioritize ? 10 : 0, scheduledAt),
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

        IQueueJob instance = (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
        configure?.Invoke((T)instance);

        var completionTask = _orchestrator.PrepareAndEnqueueImmediate(
            BuildContext(typeof(T), key, instance, int.MaxValue, null));

        if (onComplete != null)
            completionTask = completionTask.ContinueWith(
                t => onComplete(t.IsFaulted ? t.Exception!.InnerException ?? t.Exception : null),
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

        IQueueJob instance = (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
        configure?.Invoke((T)instance);

        if (parentId == Guid.Empty)
            return _orchestrator.EnqueueAsync(BuildContext(typeof(T), key, instance, 10, null), ct);

        _orchestrator.RegisterAfterParent(parentId, BuildContext(typeof(T), key, instance, int.MaxValue, null));
        return Task.CompletedTask;
    }

    public Task EnqueueRange(
        IEnumerable<(Type JobType, string JobKey, string DataJson, int Priority, DateTimeOffset? ScheduledAt)> jobs,
        CancellationToken ct = default)
    {
        var contexts = new List<EnqueueContext>();
        foreach (var (jobType, jobKey, dataJson, priority, scheduledAt) in jobs)
        {
            if (_orchestrator.IsQueued(jobKey)) continue;

            // No live instance was provided — rehydrate one to read TypeName/Title/Details once.
            // We still serialize from `instance` rather than reuse `dataJson` so the canonical
            // round-trip happens in one place.
            var instance = (IQueueJob)RuntimeHelpers.GetUninitializedObject(jobType);
            JobDataSerializer.Apply(instance, dataJson);

            contexts.Add(BuildContext(jobType, jobKey, instance, priority, scheduledAt));
        }
        return _orchestrator.EnqueueRangeAsync(contexts, ct);
    }

    /// <summary>
    /// Single place that assembles a full <see cref="EnqueueContext"/> from a live
    /// <see cref="IQueueJob"/> instance: persisted <see cref="QueuedJob"/> + display
    /// <see cref="QueueItem"/> + pre-resolved <see cref="Type"/>. PoolName is left blank — the
    /// orchestrator stamps it from pool routing.
    /// </summary>
    internal static EnqueueContext BuildContext(Type type, string jobKey, IQueueJob instance, int priority, DateTimeOffset? scheduledAt)
    {
        var asmQualified = type.FullName + ", " + type.Assembly.GetName().Name;
        var shortTypeName = type.Name;
        return new EnqueueContext
        {
            Type = type,
            Job = new QueuedJob
            {
                Id = Guid.NewGuid(),
                JobType = string.Intern(asmQualified),
                JobKey = jobKey,
                JobDataJson = JobDataSerializer.Serialize(instance),
                Priority = priority,
                QueuedAt = DateTimeOffset.UtcNow,
                ScheduledAt = scheduledAt
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

    public IJobChainBuilder CreateJobChain() => new JobChainBuilder(this, _orchestrator);

    public bool IsJobTypeBlocked(Type jobType) => _orchestrator.IsJobTypeBlocked(jobType);

    public Task Enqueue(Type jobType, Action<IQueueJob>? configure = null, bool prioritize = false)
    {
        var data = JobDataSerializer.DiffFromDefaultUntyped(jobType, configure);
        var key = JobKeyBuilder<IQueueJob>.BuildForType(jobType, data);

        if (_orchestrator.IsQueued(key))
            return Task.CompletedTask;

        var instance = (IQueueJob)RuntimeHelpers.GetUninitializedObject(jobType);
        configure?.Invoke(instance);

        return _orchestrator.EnqueueAsync(
            BuildContext(jobType, key, instance, prioritize ? 10 : 0, null));
    }

    public Task RunAfterCurrent(Type jobType, Action<IQueueJob>? configure = null)
    {
        var parentId = SubExecutionTracker.CurrentJobId.Value;

        var data = JobDataSerializer.DiffFromDefaultUntyped(jobType, configure);
        var key = JobKeyBuilder<IQueueJob>.BuildForType(jobType, data);

        var instance = (IQueueJob)RuntimeHelpers.GetUninitializedObject(jobType);
        configure?.Invoke(instance);

        if (parentId == Guid.Empty)
            return _orchestrator.EnqueueAsync(BuildContext(jobType, key, instance, 10, null));

        _orchestrator.RegisterAfterParent(parentId, BuildContext(jobType, key, instance, int.MaxValue, null));
        return Task.CompletedTask;
    }

    public Task Remove(string jobKey, CancellationToken ct = default)
        => _orchestrator.RemoveAsync(jobKey, ct);

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

    public Task<QueueState> GetState(int maxWaiting = 100, int offset = 0,
        bool includeBlocked = true, CancellationToken ct = default)
    {
        var executing = _orchestrator.GetExecuting();
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
}

/// <summary>
/// Sequential job chain builder. Builds contexts up-front then submits them to the orchestrator
/// so that all parent-child relationships are established before any job starts executing.
/// </summary>
internal sealed class JobChainBuilder : IJobChainBuilder
{
    private readonly QueueScheduler _scheduler;
    private readonly QueueOrchestrator _orchestrator;
    private readonly List<EnqueueContext> _entries = [];

    internal JobChainBuilder(QueueScheduler scheduler, QueueOrchestrator orchestrator)
    {
        _scheduler = scheduler;
        _orchestrator = orchestrator;
    }

    public IJobChainBuilder Then<T>(Action<T>? configure = null) where T : class, IQueueJob
    {
        var keyBuilder = JobKeyBuilder<T>.Create();
        if (configure != null) keyBuilder.UsingJobData(configure);
        var key = keyBuilder.Build();

        IQueueJob instance = (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
        configure?.Invoke((T)instance);

        _entries.Add(QueueScheduler.BuildContext(typeof(T), key, instance, int.MaxValue, null));
        return this;
    }

    public IJobChainBuilder Then(Type jobType, Action<IQueueJob>? configure = null)
    {
        var data = JobDataSerializer.DiffFromDefaultUntyped(jobType, configure);
        var key = JobKeyBuilder<IQueueJob>.BuildForType(jobType, data);

        var instance = (IQueueJob)RuntimeHelpers.GetUninitializedObject(jobType);
        configure?.Invoke(instance);

        _entries.Add(QueueScheduler.BuildContext(jobType, key, instance, int.MaxValue, null));
        return this;
    }

    public Task EnqueueAfterCurrent()
    {
        if (_entries.Count == 0) return Task.CompletedTask;

        var parentId = SubExecutionTracker.CurrentJobId.Value;
        if (parentId == Guid.Empty)
            return Enqueue();

        _orchestrator.RegisterAfterParent(parentId, _entries[0]);
        for (var i = 1; i < _entries.Count; i++)
            _orchestrator.RegisterChainAfterJob(_entries[i - 1].Job.Id, _entries[i]);

        return Task.CompletedTask;
    }

    public Task Enqueue()
    {
        if (_entries.Count == 0) return Task.CompletedTask;

        var first = _orchestrator.EnqueueAsync(_entries[0]);
        for (var i = 1; i < _entries.Count; i++)
            _orchestrator.RegisterChainAfterJob(_entries[i - 1].Job.Id, _entries[i]);

        return first;
    }
}
