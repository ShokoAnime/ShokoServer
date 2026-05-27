#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shoko.QueueProcessor.Abstractions;

namespace Shoko.QueueProcessor.Scheduling;

/// <summary>
/// Replaces <c>QuartzStartup.ScheduleRecurringJobs()</c>.
/// Holds a set of named recurring job registrations and re-enqueues them on their schedule
/// if they are not already waiting or executing.
/// </summary>
public class RecurringJobRegistry : IHostedService, IDisposable
{
    private readonly IQueueScheduler _scheduler;
    private readonly ILogger<RecurringJobRegistry> _logger;

    private readonly List<RecurringRegistration> _registrations = [];
    private readonly List<Timer> _timers = [];

    public RecurringJobRegistry(IQueueScheduler scheduler, ILogger<RecurringJobRegistry> logger)
    {
        _scheduler = scheduler;
        _logger = logger;
    }

    /// <summary>
    /// Registers a recurring job. Call this before <see cref="StartAsync"/> (e.g., at startup
    /// in a <c>systemService.Started</c> handler or DI setup).
    /// </summary>
    /// <typeparam name="T">The job type.</typeparam>
    /// <param name="interval">How often to (re-)enqueue the job.</param>
    /// <param name="configure">Optional job data configurator.</param>
    /// <param name="runImmediately">
    /// If true, enqueue once immediately on <see cref="StartAsync"/> in addition to the timer.
    /// </param>
    public void Register<T>(TimeSpan interval, Action<T>? configure = null, bool runImmediately = true)
        where T : class, IQueueJob
    {
        _registrations.Add(new RecurringRegistration(
            typeof(T),
            interval,
            runImmediately,
            ct => _scheduler.Enqueue(configure, ct: ct)));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var reg in _registrations)
        {
            if (reg.RunImmediately)
            {
                try { await reg.Enqueue(cancellationToken); }
                catch (Exception ex) { _logger.LogError(ex, "Failed to enqueue recurring job {Type}", reg.JobType.Name); }
            }

            var timer = new Timer(
                _ => _ = EnqueueSafe(reg, CancellationToken.None),
                null,
                reg.Interval,
                reg.Interval);
            _timers.Add(timer);
        }

        _logger.LogInformation("RecurringJobRegistry started {Count} recurring jobs", _registrations.Count);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var timer in _timers) timer.Change(Timeout.Infinite, Timeout.Infinite);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        foreach (var timer in _timers) timer.Dispose();
        _timers.Clear();
        GC.SuppressFinalize(this);
    }

    private async Task EnqueueSafe(RecurringRegistration reg, CancellationToken ct)
    {
        try { await reg.Enqueue(ct); }
        catch (Exception ex) { _logger.LogError(ex, "Failed to enqueue recurring job {Type}", reg.JobType.Name); }
    }

    private record RecurringRegistration(
        Type JobType,
        TimeSpan Interval,
        bool RunImmediately,
        Func<CancellationToken, Task> Enqueue);
}
