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
/// Plugins resolve this registry from DI (constructor-inject it, or fetch it via
/// <see cref="IServiceProvider.GetService"/>) and call <see cref="Register{T}"/> to register
/// their own recurring jobs. The call site can live in a plugin's
/// <c>IPluginServiceRegistration.RegisterServices</c> body (using a startup hook) or in
/// <c>IPlugin.Load</c>. The plugin's job type itself must first be registered via
/// <c>QueueProcessorExtensions.AddQueueJobsFromAssembly</c>.
/// </remarks>
public class RecurringJobRegistry : IHostedService, IDisposable
{
    private readonly IQueueScheduler _scheduler;
    private readonly ILogger<RecurringJobRegistry> _logger;

    private readonly List<RecurringRegistration> _registrations = [];
    private readonly List<Timer> _timers = [];
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

        _registrations.Add(reg);

        if (_started)
            _ = ActivateRegistration(reg, CancellationToken.None);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _started = true;
        foreach (var reg in _registrations)
            await ActivateRegistration(reg, cancellationToken);

        _logger.LogInformation("RecurringJobRegistry started {Count} recurring jobs", _registrations.Count);
    }

    private async Task ActivateRegistration(RecurringRegistration reg, CancellationToken ct)
    {
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
        _timers.Add(timer);
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
