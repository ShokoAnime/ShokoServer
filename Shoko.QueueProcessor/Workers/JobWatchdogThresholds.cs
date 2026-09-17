using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Concurrency;

namespace Shoko.QueueProcessor.Workers;

/// <summary>
/// Works out how long each job type may run before <see cref="JobWatchdog"/> reports it: the
/// threshold a registered <see cref="IJobWatchdogThreshold"/> gives for that type, nothing at all
/// for a type marked <see cref="LongRunningAttribute"/>, and the global threshold for everything
/// else.
/// </summary>
/// <remarks>
/// A type that declares both is given its threshold. The attribute says only that the global one is
/// wrong, while a threshold says what the right one is, and watching a job against a threshold that
/// fits it keeps the signal the exemption throws away.
/// </remarks>
internal sealed class JobWatchdogThresholds
{
    private readonly TimeSpan _defaultThreshold;
    private readonly ILogger _logger;
    private readonly HashSet<Type> _exemptTypes;
    private readonly Dictionary<Type, IJobWatchdogThreshold> _declaredThresholds = [];
    private readonly HashSet<Type> _faultedTypes = [];

    /// <param name="defaultThreshold">The threshold for every type that declares nothing.</param>
    /// <param name="allJobTypes">Every registered job type, scanned for the exemption attribute.</param>
    /// <param name="declaredThresholds">The per-job thresholds registered in DI.</param>
    /// <param name="logger">The log a misdeclaration is reported to.</param>
    internal JobWatchdogThresholds(
        TimeSpan defaultThreshold,
        IEnumerable<Type> allJobTypes,
        IEnumerable<IJobWatchdogThreshold> declaredThresholds,
        ILogger logger)
    {
        _defaultThreshold = defaultThreshold;
        _logger = logger;
        _exemptTypes = allJobTypes
            .Where(type => type.GetCustomAttribute<LongRunningAttribute>() is not null)
            .ToHashSet();
        foreach (var threshold in declaredThresholds)
        {
            if (threshold.JobType is not { } jobType)
            {
                _logger.LogWarning("Ignored the watchdog threshold {Threshold}, which names no job type.", threshold.GetType().Name);
                continue;
            }

            if (!_declaredThresholds.TryAdd(jobType, threshold))
            {
                _logger.LogWarning(
                    "Ignored the watchdog threshold {Threshold} for job {JobType}, which already has {ExistingThreshold}.",
                    threshold.GetType().Name,
                    jobType.Name,
                    _declaredThresholds[jobType].GetType().Name
                );
                continue;
            }

            if (_exemptTypes.Contains(jobType))
                _logger.LogWarning(
                    "Job {JobType} is marked long-running and declares the watchdog threshold {Threshold}. The threshold is used and the job stays watched.",
                    jobType.Name,
                    threshold.GetType().Name
                );
        }
    }

    /// <summary>
    /// How long a job of the given type may run before it is reported.
    /// </summary>
    /// <remarks>
    /// Called from the watchdog's single polling task, and not safe to call from more than one
    /// thread: it remembers which thresholds have already thrown so each one is reported once.
    /// </remarks>
    /// <param name="jobType">The executing job's type.</param>
    /// <returns>The threshold, or <c>null</c> when the type is not watched at all.</returns>
    internal TimeSpan? For(Type jobType)
    {
        if (_declaredThresholds.TryGetValue(jobType, out var declared))
            return Evaluate(jobType, declared);

        return _exemptTypes.Contains(jobType) ? null : _defaultThreshold;
    }

    /// <summary>
    /// Ask one threshold, falling back to the global one for anything it cannot answer with.
    /// </summary>
    /// <param name="jobType">The executing job's type.</param>
    /// <param name="declared">The threshold registered for it.</param>
    /// <returns>The threshold to watch the job against.</returns>
    private TimeSpan Evaluate(Type jobType, IJobWatchdogThreshold declared)
    {
        try
        {
            if (declared.GetThreshold(_defaultThreshold) is { } threshold && threshold > TimeSpan.Zero)
                return threshold;
        }
        catch (Exception ex)
        {
            if (_faultedTypes.Add(jobType))
                _logger.LogError(ex, "Watchdog threshold {Threshold} for job {JobType} threw. Falling back to the global threshold.", declared.GetType().Name, jobType.Name);
        }

        return _defaultThreshold;
    }
}
