using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.QueueProcessor.Concurrency;
using Shoko.QueueProcessor.Orchestration;

namespace Shoko.QueueProcessor.Workers;

/// <summary>
/// Background task that periodically scans executing jobs and logs a warning for any that have
/// been running longer than the configured timeout. Jobs decorated with
/// <see cref="LongRunningAttribute"/> are exempt.
/// <para>
/// When a stuck job last called <see cref="Abstractions.IJobFactory.Execute{T}"/>,
/// the call stack captured at that point is included in the warning to help identify the deadlock
/// site without requiring thread suspension.
/// </para>
/// </summary>
internal sealed class JobWatchdog
{
    private readonly QueueOrchestrator _orchestrator;
    private readonly ILogger _logger;
    private readonly TimeSpan _timeout;
    private readonly HashSet<Type> _exemptTypes;
    private readonly HashSet<(Guid Id, DateTime StartedAt)> _alreadyWarned = [];
    private Task? _watchTask;

    private const int PollIntervalSeconds = 15;

    internal JobWatchdog(
        QueueOrchestrator orchestrator,
        ILogger logger,
        TimeSpan timeout,
        IEnumerable<Type> allJobTypes)
    {
        _orchestrator = orchestrator;
        _logger = logger;
        _timeout = timeout;
        _exemptTypes = allJobTypes
            .Where(t => t.GetCustomAttribute<LongRunningAttribute>() is not null)
            .ToHashSet();
    }

    internal void Start(CancellationToken ct)
    {
        _watchTask = Task.Run(() => RunAsync(ct), ct);
    }

    internal async Task StopAsync()
    {
        if (_watchTask != null)
            await _watchTask.ConfigureAwait(false);
    }

    private async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(PollIntervalSeconds), ct).ConfigureAwait(false);
                Check();
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Watchdog check threw an unexpected exception");
            }
        }
    }

    private void Check()
    {
        var now = DateTime.UtcNow;
        var executing = _orchestrator.GetExecuting();
        var executingKeys = executing.Select(e => (e.Id, e.StartedAt)).ToHashSet();

        _alreadyWarned.RemoveWhere(key => !executingKeys.Contains(key));

        foreach (var entry in executing)
        {
            if (_exemptTypes.Contains(entry.JobType)) continue;

            var elapsed = now - entry.StartedAt;
            if (elapsed < _timeout) continue;

            if (_alreadyWarned.Add((entry.Id, entry.StartedAt)))
            {
                // First detection — log error once with full context for analytics/aggregation
                _logger.LogError(
                    "Possible deadlock detected in job {JobType} — running for {Elapsed:g}.\n{Stack}",
                    entry.JobType.Name,
                    elapsed,
                    SubExecutionTracker.GetStack(entry.Id) ?? "(no sub-execution context captured)");
            }
            else
            {
                // Heartbeat — job is still stuck on every subsequent poll
                _logger.LogWarning(
                    "Job {JobType} [{JobKey}] still running after {Elapsed:g}",
                    entry.JobType.Name, entry.JobKey, elapsed);
            }
        }
    }
}
