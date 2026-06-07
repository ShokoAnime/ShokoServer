using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shoko.QueueProcessor.Abstractions;

namespace Shoko.QueueProcessor.Scheduling;

/// <summary>
/// Holds a set of named recurring job registrations and re-enqueues them on their schedule
/// if they are not already waiting or executing.
/// </summary>
/// <remarks>
/// <para>
/// Plugins resolve this registry from DI (constructor-inject it, or fetch it via
/// <see cref="IServiceProvider.GetService"/>) and call <see cref="Register{T}"/> to register
/// their own recurring jobs. The call site can live in a plugin's
/// <c>IPluginServiceRegistration.RegisterServices</c> body (using a startup hook) or in
/// <c>IPlugin.Load</c>. The plugin's job type itself must first be registered via
/// <c>QueueProcessorExtensions.AddQueueJobsFromAssembly</c>.
/// </para>
/// <para>
/// Lifecycle: registrations made before <see cref="StartAsync"/> sit dormant and get armed
/// when the web host boots the registry. Registrations made after are armed immediately.
/// Jobs that need the main database should carry <c>[DatabaseRequired]</c> — the acquisition
/// filter will hold them out of the worker pool until startup signals the DB is ready.
/// Jobs without that attribute (e.g. a network-availability probe) start running with the
/// queue.
/// </para>
/// </remarks>
public class RecurringJobRegistry : IHostedService, IDisposable
{
    private readonly IQueueScheduler _scheduler;
    private readonly ILogger<RecurringJobRegistry> _logger;

    private readonly object _lock = new();
    private readonly Dictionary<Type, JobEntry> _entries = new();
    private volatile bool _started;

    public RecurringJobRegistry(IQueueScheduler scheduler, ILogger<RecurringJobRegistry> logger)
    {
        _scheduler = scheduler;
        _logger = logger;
    }

    /// <summary>
    /// Registers a recurring job. Safe to call before or after <see cref="StartAsync"/>:
    /// if the registry has already started, the timer is armed immediately and the job is
    /// enqueued right away when <paramref name="runImmediately"/> is <c>true</c>.
    /// </summary>
    /// <typeparam name="T">The job type.</typeparam>
    /// <param name="interval">How often to (re-)enqueue the job.</param>
    /// <param name="configure">Optional job data configurator.</param>
    /// <param name="runImmediately">
    /// If true, enqueue once immediately (or right now if already started) in addition to the timer.
    /// </param>
    public void Register<T>(TimeSpan interval, Action<T>? configure = null, bool runImmediately = true)
        where T : class, IQueueJob
    {
        var reg = new RecurringRegistration(
            typeof(T),
            interval,
            runImmediately,
            ct => _scheduler.Enqueue(configure, ct: ct));

        var entry = new JobEntry(reg);

        bool activateNow;
        lock (_lock)
        {
            _entries[typeof(T)] = entry;
            activateNow = _started;
        }

        if (activateNow)
            _ = ActivateEntry(entry, CancellationToken.None);
    }

    /// <summary>
    /// Updates the interval for an already-registered job. If the job has not been registered
    /// yet (e.g. it was initially skipped because its frequency was set to Never), it is
    /// registered now with <paramref name="interval"/> and <c>runImmediately = false</c>.
    /// </summary>
    public void Reschedule<T>(TimeSpan interval) where T : class, IQueueJob
    {
        lock (_lock)
        {
            if (_entries.TryGetValue(typeof(T), out var entry))
            {
                entry.Timer?.Change(interval, interval);
                return;
            }
        }

        // Job was never registered (e.g. initial frequency was Never). Register it now.
        Register<T>(interval, runImmediately: false);
    }

    /// <summary>
    /// Stops the recurring timer for a registered job without removing the registration.
    /// The job can be re-enabled later via <see cref="Reschedule{T}"/>.
    /// </summary>
    public void Unschedule<T>() where T : class, IQueueJob
    {
        lock (_lock)
        {
            if (_entries.TryGetValue(typeof(T), out var entry))
                entry.Timer?.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        JobEntry[] toActivate;
        lock (_lock)
        {
            _started = true;
            toActivate = [.. _entries.Values];
        }

        foreach (var entry in toActivate)
            await ActivateEntry(entry, cancellationToken);

        _logger.LogInformation("RecurringJobRegistry started {Count} recurring jobs", toActivate.Length);
    }

    private async Task ActivateEntry(JobEntry entry, CancellationToken ct)
    {
        var reg = entry.Registration;
        if (reg.RunImmediately)
        {
            try { await reg.Enqueue(ct); }
            catch (Exception ex) { _logger.LogError(ex, "Failed to enqueue recurring job {Type}", reg.JobType.Name); }
        }

        var timer = new Timer(
            _ => _ = EnqueueSafe(reg, CancellationToken.None),
            null,
            reg.Interval,
            reg.Interval);

        lock (_lock) entry.Timer = timer;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            foreach (var entry in _entries.Values)
                entry.Timer?.Change(Timeout.Infinite, Timeout.Infinite);
        }
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var entry in _entries.Values)
                entry.Timer?.Dispose();
            _entries.Clear();
        }
        GC.SuppressFinalize(this);
    }

    private async Task EnqueueSafe(RecurringRegistration reg, CancellationToken ct)
    {
        try { await reg.Enqueue(ct); }
        catch (Exception ex) { _logger.LogError(ex, "Failed to enqueue recurring job {Type}", reg.JobType.Name); }
    }

    private sealed class JobEntry(RecurringRegistration registration)
    {
        public RecurringRegistration Registration { get; } = registration;
        public Timer? Timer { get; set; }
    }

    private record RecurringRegistration(
        Type JobType,
        TimeSpan Interval,
        bool RunImmediately,
        Func<CancellationToken, Task> Enqueue);
}
