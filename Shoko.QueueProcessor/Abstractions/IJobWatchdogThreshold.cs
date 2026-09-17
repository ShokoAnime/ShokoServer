using System;

namespace Shoko.QueueProcessor.Abstractions;

/// <summary>
/// Declares how long one job type may run before the watchdog reports it as a possible deadlock,
/// in place of <see cref="QueueProcessorOptions.WatchdogTimeoutSeconds"/>. Register an
/// implementation in DI and the watchdog picks it up when it starts.
/// </summary>
/// <remarks>
/// <para>
/// This is the mechanism for a job whose own deadline is longer than the global threshold: the job
/// still gets watched, and the threshold sits above the deadline so that only an overrun is
/// reported. A job that wants no threshold at all, because it has no deadline to overrun, uses
/// <see cref="Concurrency.LongRunningAttribute"/> instead.
/// </para>
/// <para>
/// <see cref="GetThreshold"/> is asked on every poll rather than once at startup, so a threshold
/// worked out from a setting follows that setting while the server runs. It is called from the
/// watchdog's single polling task, so it must be cheap, must not block, and must not depend on
/// being called from a request or job scope.
/// </para>
/// </remarks>
public interface IJobWatchdogThreshold
{
    /// <summary>
    /// The job type the threshold applies to. Subtypes are not covered; declare one per type.
    /// </summary>
    Type JobType { get; }

    /// <summary>
    /// How long a job of <see cref="JobType"/> may run before the watchdog reports it.
    /// </summary>
    /// <param name="defaultThreshold">
    /// The global threshold, from <see cref="QueueProcessorOptions.WatchdogTimeoutSeconds"/>. Use it
    /// as the floor for a threshold that scales with a setting, so lowering the global one lowers
    /// this one too.
    /// </param>
    /// <returns>
    /// The threshold, or <c>null</c> to use <paramref name="defaultThreshold"/>. A value that is
    /// zero or negative is treated as <c>null</c>.
    /// </returns>
    TimeSpan? GetThreshold(TimeSpan defaultThreshold);
}
