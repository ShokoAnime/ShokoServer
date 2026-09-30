using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.QueueProcessor;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Analytics;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Chain;
using Shoko.QueueProcessor.Events;
using Shoko.QueueProcessor.Orchestration;
using Shoko.QueueProcessor.Scheduling;
using Shoko.QueueProcessor.Storage;
using Shoko.QueueProcessor.Workers;
using Shoko.Server.API.v3.Controllers;
using Shoko.Server.Settings;
using Xunit;

using QueueModel = Shoko.Server.API.v3.Models.Shoko.Queue;

namespace Shoko.Tests.API;

/// <summary>
/// Covers cancelling and removing queue items through APIv3, over a real queue with no workers
/// running: a job is made to run by acquiring it from its pool.
/// </summary>
public class QueueControllerTests
{
    #region Fixture

    /// <summary>
    /// A job that observes cancellation.
    /// </summary>
    public sealed class CancellableJob(IJobCancellationAccessor cancellation) : IQueueJob
    {
        public int Id { get; set; }

        public string TypeName => nameof(CancellableJob);

        public string Title => $"Cancellable {Id}";

        public Task Process() => Task.Delay(Timeout.Infinite, cancellation.Token);
    }

    /// <summary>
    /// A job that does not observe cancellation.
    /// </summary>
    public sealed class PlainJob : IQueueJob
    {
        public int Id { get; set; }

        public string TypeName => nameof(PlainJob);

        public string Title => $"Plain {Id}";

        public Task Process() => Task.CompletedTask;
    }

    /// <summary>
    /// The controller over a real queue with no workers running.
    /// </summary>
    internal sealed class Fixture : IDisposable
    {
        private readonly ServiceProvider _provider;

        public WorkerPool Pool { get; }

        public QueueScheduler Scheduler { get; }

        public QueueStateEventHandler Events { get; } = new();

        public QueueOrchestrator Orchestrator { get; }

        public QueueHandler Handler { get; }

        public QueueController Controller { get; }

        public Fixture()
        {
            var repo = new Mock<IJobRepository>();
            repo.Setup(r => r.InsertBatchAsync(It.IsAny<IReadOnlyCollection<QueuedJob>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            repo.Setup(r => r.DeleteBatchAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var services = new ServiceCollection();
            services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
            services.AddSingleton(repo.Object);
            services.AddScoped<JobCancellationAccessor>();
            services.AddScoped<IJobCancellationAccessor>(sp => sp.GetRequiredService<JobCancellationAccessor>());
            services.AddTransient<CancellableJob>();
            services.AddTransient<PlainJob>();
            _provider = services.BuildServiceProvider();

            var scopeFactory = _provider.GetRequiredService<IServiceScopeFactory>();
            var buffer = new PersistenceBuffer(scopeFactory, NullLogger<PersistenceBuffer>.Instance, flushIntervalMs: int.MaxValue);
            var concurrency = new ConcurrencyRegistry(new Dictionary<Type, int>(), new Dictionary<Type, string?>(), new Dictionary<string, int>());
            var chainScopes = new ChainScopeRegistry(scopeFactory, NullLogger<ChainScopeRegistry>.Instance);
            var orchestrator = Orchestrator = new QueueOrchestrator(
                NullLogger<QueueOrchestrator>.Instance, buffer, scopeFactory, concurrency,
                new RetryPolicyResolver(new RetryPolicy()), new QueueMetrics(), Events,
                chainScopes, maxTotalWorkers: 4);
            Pool = new WorkerPool("TestPool", maxWorkers: 4, AcquisitionAttribute.LowestPriority, [typeof(CancellableJob), typeof(PlainJob)], []);
            orchestrator.Initialize([], [Pool]);
            Scheduler = new QueueScheduler(orchestrator, chainScopes, scopeFactory);

            // The pool manager is only used to pause and resume, which these tests never do.
            Handler = new QueueHandler(Scheduler, orchestrator, null!, _provider);
            Controller = new QueueController(new Mock<ISettingsProvider>().Object, Handler);
        }

        public async Task<string> Enqueue<T>(int id, bool run = false) where T : class, IQueueJob
        {
            var property = typeof(T).GetProperty("Id")!;
            await Scheduler.Enqueue<T>(job => property.SetValue(job, id));
            if (run)
                Assert.NotNull(Pool.TryAcquire());
            return JobKeyBuilder<T>.Create().UsingJobData(job => property.SetValue(job, id)).Build();
        }

        public void Dispose() => _provider.Dispose();
    }

    #endregion

    #region Cancel

    [Fact]
    public async Task CancelItem_WaitingItem_RemovesIt()
    {
        using var fixture = new Fixture();
        var key = await fixture.Enqueue<PlainJob>(1);

        var result = await fixture.Controller.CancelItem(key);

        var body = Assert.IsType<QueueModel.CancelResult>(result.Value);
        Assert.Equal(key, body.Key);
        Assert.Equal(QueueModel.CancelResultType.Removed, body.Result);
        Assert.False(fixture.Scheduler.IsQueued(key));
    }

    [Fact]
    public async Task CancelItem_RunningCancellableItem_RequestsCancellationAndShowsIt()
    {
        using var fixture = new Fixture();
        var key = await fixture.Enqueue<CancellableJob>(1, run: true);

        var result = await fixture.Controller.CancelItem(key);

        Assert.Equal(QueueModel.CancelResultType.CancellationRequested, Assert.IsType<QueueModel.CancelResult>(result.Value).Result);
        var item = Assert.Single(fixture.Controller.GetQueue().CurrentlyExecuting);
        Assert.Equal(key, item.Key);
        Assert.True(item.IsRunning);
        Assert.True(item.IsCancellable);
        Assert.True(item.IsCancellationRequested);
        Assert.True(fixture.Scheduler.IsQueued(key));
    }

    [Fact]
    public async Task CancelItem_RunningItemThatCannotBeCancelled_IsAConflict()
    {
        using var fixture = new Fixture();
        var key = await fixture.Enqueue<PlainJob>(1, run: true);

        var result = await fixture.Controller.CancelItem(key);

        Assert.IsType<ConflictObjectResult>(result.Result);
        var item = Assert.Single(fixture.Controller.GetQueue().CurrentlyExecuting);
        Assert.False(item.IsCancellable);
        Assert.False(item.IsCancellationRequested);
    }

    [Fact]
    public async Task CancelItem_UnknownKey_IsNotFound()
    {
        using var fixture = new Fixture();

        var result = await fixture.Controller.CancelItem("nothing");

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    #endregion

    #region Remove

    [Fact]
    public async Task RemoveItem_WaitingItem_RemovesItAndFreesItsKey()
    {
        using var fixture = new Fixture();
        var key = await fixture.Enqueue<PlainJob>(1);

        Assert.IsType<NoContentResult>(await fixture.Controller.RemoveItem(key));

        Assert.False(fixture.Scheduler.IsQueued(key));
        await fixture.Enqueue<PlainJob>(1);
        Assert.True(fixture.Scheduler.IsQueued(key));
    }

    [Fact]
    public async Task RemoveItem_RunningItem_IsAConflictAndLeavesItRunning()
    {
        using var fixture = new Fixture();
        var key = await fixture.Enqueue<CancellableJob>(1, run: true);

        Assert.IsType<ConflictObjectResult>(await fixture.Controller.RemoveItem(key));

        var item = Assert.Single(fixture.Controller.GetQueue().CurrentlyExecuting);
        Assert.False(item.IsCancellationRequested);
    }

    [Fact]
    public async Task RemoveItem_UnknownKey_IsNotFound()
    {
        using var fixture = new Fixture();

        Assert.IsType<NotFoundObjectResult>(await fixture.Controller.RemoveItem("nothing"));
    }

    #endregion

    #region Items

    [Fact]
    public async Task GetItemsInQueue_ShowsWaitingItemsAsCancellable()
    {
        using var fixture = new Fixture();
        await fixture.Enqueue<PlainJob>(1);

        var items = fixture.Controller.GetItemsInQueue().Value!.List;

        var item = Assert.Single(items);
        Assert.False(item.IsRunning);
        Assert.True(item.IsCancellable);
        Assert.False(item.IsCancellationRequested);
        Assert.Null(item.Progress);
    }

    #endregion
}
