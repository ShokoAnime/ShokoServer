using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.ScheduledActions;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Workers;
using Shoko.Server.Scheduling.Jobs.Actions;
using Shoko.Server.Services;
using Xunit;

namespace Shoko.Tests.Scheduling.Jobs;

/// <summary>
/// Covers the job that runs a scheduled action: it resolves the action from its
/// own container, hands it the job's progress and cancellation, and skips an
/// unknown action or one refusing to run in the queue.
/// </summary>
public sealed class ScheduledActionJobTests : IDisposable
{
    #region Test Actions

    /// <summary>
    /// What the test action saw, shared across its instances.
    /// </summary>
    public sealed class Recorder
    {
        public bool Refuse { get; set; }

        public int Runs { get; set; }

        public CancellationToken Token { get; set; }

        public RunScope? Scope { get; set; }
    }

    /// <summary>
    /// A scoped dependency, to tell which container the action came from.
    /// </summary>
    public sealed class RunScope;

    public sealed class ReportingAction(Recorder recorder, RunScope scope) : IScheduledAction
    {
        public string Name => "Reporting";

        public Task<ActionValidationResult?> Validate(CancellationToken token)
            => Task.FromResult<ActionValidationResult?>(recorder.Refuse ? new("Not now.") : null);

        public Task Execute(IProgress<decimal> progress, CancellationToken token)
        {
            recorder.Runs++;
            recorder.Token = token;
            recorder.Scope = scope;
            progress.Report(0);
            progress.Report(50);
            progress.Report(100);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Keeps every value reported, in order.
    /// </summary>
    private sealed class ListProgress : IProgress<decimal>
    {
        public List<decimal> Values { get; } = [];

        public void Report(decimal value) => Values.Add(value);
    }

    #endregion

    #region Fixture

    private readonly Recorder _recorder = new();

    private readonly ListProgress _progress = new();

    private readonly CancellationTokenSource _cancellation = new();

    private readonly ScheduledActionRegistry _registry;

    private readonly ServiceProvider _services;

    public ScheduledActionJobTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_recorder);
        services.AddScoped<RunScope>();
        services.AddTransient<ReportingAction>();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        _services = services.BuildServiceProvider();
        _registry = new(Mock.Of<IQueueScheduler>(), Mock.Of<IPluginManager>(), _services);
        _registry.AddParts([(Guid.NewGuid(), typeof(ReportingAction))]);
    }

    public void Dispose()
    {
        _cancellation.Dispose();
        _services.Dispose();
    }

    private ScheduledActionJob Job(IServiceProvider? jobServices = null)
    {
        var job = new ScheduledActionJob(
            jobServices ?? _services,
            _registry,
            Mock.Of<IJobCancellationAccessor>(accessor => accessor.Token == _cancellation.Token),
            Mock.Of<IJobProgressAccessor>(accessor => accessor.Progress == _progress)
        )
        {
            ActionId = _registry.GetAction(typeof(ReportingAction))!.ID,
        };
        job.Setup(_services);
        return job;
    }

    #endregion

    #region Tests

    [Fact]
    public async Task TheJob_HandsTheActionItsProgressAndToken()
    {
        await Job().Execute();

        Assert.Equal(1, _recorder.Runs);
        Assert.Equal([0m, 50m, 100m], _progress.Values);
        Assert.Equal(_cancellation.Token, _recorder.Token);
    }

    [Fact]
    public async Task TheAction_IsResolvedFromTheJobsOwnContainer()
    {
        using var first = _services.CreateScope();
        using var second = _services.CreateScope();

        await Job(jobServices: first.ServiceProvider).Execute();
        var firstScope = _recorder.Scope;
        await Job(jobServices: second.ServiceProvider).Execute();

        Assert.Same(first.ServiceProvider.GetRequiredService<RunScope>(), firstScope);
        Assert.Same(second.ServiceProvider.GetRequiredService<RunScope>(), _recorder.Scope);
        Assert.NotSame(firstScope, _recorder.Scope);
    }

    [Fact]
    public async Task AnActionThatRefusesInTheQueue_IsSkipped()
    {
        _recorder.Refuse = true;

        await Job().Execute();

        Assert.Equal(0, _recorder.Runs);
        Assert.Empty(_progress.Values);
    }

    [Fact]
    public async Task AnUnknownAction_IsSkipped()
    {
        var job = Job();
        job.ActionId = Guid.NewGuid();

        await job.Execute();

        Assert.Equal(0, _recorder.Runs);
    }

    #endregion
}
