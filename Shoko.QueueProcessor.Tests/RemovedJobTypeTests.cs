using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Analytics;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Chain;
using Shoko.QueueProcessor.Events;
using Shoko.QueueProcessor.Orchestration;
using Shoko.QueueProcessor.Storage;
using Shoko.QueueProcessor.Workers;
using Xunit;

namespace Shoko.QueueProcessor.Tests;

/// <summary>
/// Covers what <see cref="QueueOrchestrator.Initialize"/> does with a persisted job whose type it
/// cannot find: one from a loaded assembly had its type removed and is deleted, while one from an
/// assembly that is not loaded may be back, so it is kept.
/// </summary>
public class RemovedJobTypeTests
{
    private class StillHereJob : IQueueJob
    {
        public string TypeName => "StillHereJob";

        public string Title => "";

        public Dictionary<string, object> Details => [];

        public void PostInit() { }

        public Task Process() => Task.CompletedTask;
    }

    private static readonly string s_loadedAssembly = typeof(StillHereJob).Assembly.GetName().Name!;

    private static QueuedJob Job(string jobType) => new()
    {
        Id = Guid.NewGuid(),
        JobType = jobType,
        JobKey = $"key_{Guid.NewGuid()}",
        QueuedAt = DateTimeOffset.UtcNow,
    };

    private static (QueueOrchestrator Orchestrator, List<Guid> Deleted, SemaphoreSlim Signal) Orchestrator()
    {
        var deleted = new List<Guid>();
        var signal = new SemaphoreSlim(0);
        var repo = new Mock<IJobRepository>();
        repo.Setup(r => r.DeleteBatchAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<Guid>, CancellationToken>((ids, _) =>
            {
                lock (deleted)
                    deleted.AddRange(ids);
                signal.Release();
            })
            .Returns(Task.CompletedTask);

        var provider = new Mock<IServiceProvider>();
        provider.Setup(s => s.GetService(typeof(IJobRepository))).Returns(repo.Object);
        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(provider.Object);
        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

        var orchestrator = new QueueOrchestrator(
            NullLogger<QueueOrchestrator>.Instance,
            new PersistenceBuffer(scopeFactory.Object, NullLogger<PersistenceBuffer>.Instance, flushIntervalMs: int.MaxValue),
            scopeFactory.Object,
            new ConcurrencyRegistry(new Dictionary<Type, int>(), new Dictionary<Type, string?>(), new Dictionary<string, int>()),
            new RetryPolicyResolver(new RetryPolicy { MaxRetries = 0 }),
            new QueueMetrics(),
            new QueueStateEventHandler(),
            new ChainScopeRegistry(scopeFactory.Object, NullLogger<ChainScopeRegistry>.Instance),
            1
        );
        return (orchestrator, deleted, signal);
    }

    /// <summary>
    /// The deletion of the removed type's job signals that the pass is over, so the kept one is
    /// known to be left alone.
    /// </summary>
    /// <param name="keptType">The stored type of the job that must be kept.</param>
    [Theory]
    [InlineData("Some.Plugin.Job, Some.Plugin")]
    [InlineData("Some.Generic`1[[Some.Plugin.Provider, Some.Plugin]], {0}")]
    public async Task AJobWhoseTypeWasRemovedIsDeleted_OneWhoseAssemblyIsMissingIsKept(string keptType)
    {
        var (orchestrator, deleted, signal) = Orchestrator();
        var gone = Job($"Shoko.QueueProcessor.Tests.GoneJob, {s_loadedAssembly}");
        var kept = Job(string.Format(keptType, s_loadedAssembly));

        orchestrator.Initialize([gone, kept], [new WorkerPool("Pool", 1, 0, [typeof(StillHereJob)], [])]);

        Assert.True(await signal.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        lock (deleted)
            Assert.Equal([gone.Id], deleted);
    }

    [Theory]
    [InlineData("Some.Namespace.Job, Some.Assembly", "Some.Assembly")]
    [InlineData("Generic`1[[A, B]], Outer", "Outer|B")]
    [InlineData("NoAssembly", "")]
    public void TheAssembliesAreReadOffTheStoredType(string jobType, string expected)
        => Assert.Equal(expected, string.Join("|", JobTypeNames.AssemblyNames(jobType)));
}
