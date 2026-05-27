#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Analytics;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Orchestration;
using Shoko.QueueProcessor.Storage;

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

        // Serialize data: all public settable properties
        IQueueJob instance = Activator.CreateInstance<T>();
        configure?.Invoke((T)instance);
        var dataJson = JobDataSerializer.Serialize(instance);

        var typeName = typeof(T).FullName + ", " + typeof(T).Assembly.GetName().Name;

        var job = new QueuedJob
        {
            Id = Guid.NewGuid(),
            JobType = string.Intern(typeName),
            JobKey = key,
            JobDataJson = dataJson,
            Priority = prioritize ? 10 : 0,
            QueuedAt = DateTimeOffset.UtcNow,
            ScheduledAt = scheduledAt
        };

        return _orchestrator.EnqueueAsync(job, ct);
    }

    public Task EnqueueRange(
        IEnumerable<(Type JobType, string JobKey, string DataJson, int Priority, DateTimeOffset? ScheduledAt)> jobs,
        CancellationToken ct = default)
    {
        var queuedJobs = new List<QueuedJob>();
        foreach (var (jobType, jobKey, dataJson, priority, scheduledAt) in jobs)
        {
            if (_orchestrator.IsQueued(jobKey)) continue;
            var typeName = jobType.FullName + ", " + jobType.Assembly.GetName().Name;
            queuedJobs.Add(new QueuedJob
            {
                Id = Guid.NewGuid(),
                JobType = string.Intern(typeName),
                JobKey = jobKey,
                JobDataJson = dataJson,
                Priority = priority,
                QueuedAt = DateTimeOffset.UtcNow,
                ScheduledAt = scheduledAt
            });
        }
        return _orchestrator.EnqueueRangeAsync(queuedJobs, ct);
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
