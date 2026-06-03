using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Analytics;
using Shoko.QueueProcessor.Chain;
using Shoko.QueueProcessor.Events;
using Shoko.QueueProcessor.Orchestration;
using Shoko.QueueProcessor.Storage;
using Shoko.QueueProcessor.Workers;
using Xunit;

namespace Shoko.QueueProcessor.Tests;

/// <summary>
/// Tests for pool-priority slot reservation:
/// <see cref="QueueOrchestrator"/> wires <see cref="WorkerPool.ShouldAttemptAcquisition"/>
/// so that higher-priority pools claim their runnable slots before lower-priority pools proceed.
/// </summary>
public class WorkerPoolPriorityTests
{
    // ── Fixture job types ─────────────────────────────────────────────────────

    private class HighPriorityJob : IQueueJob
    {
        public string TypeName => "HighPriorityJob";
        public string Title => "";
        public Dictionary<string, object> Details => [];
        public void PostInit() { }
        public Task Process() => Task.CompletedTask;
    }

    private class LowPriorityJob : IQueueJob
    {
        public string TypeName => "LowPriorityJob";
        public string Title => "";
        public Dictionary<string, object> Details => [];
        public void PostInit() { }
        public Task Process() => Task.CompletedTask;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static QueueOrchestrator MakeOrchestrator(int maxWorkers)
    {
        var repo = new Mock<IJobRepository>();
        repo.Setup(r => r.InsertBatchAsync(It.IsAny<IReadOnlyCollection<QueuedJob>>(), default)).Returns(Task.CompletedTask);
        repo.Setup(r => r.DeleteBatchAsync(It.IsAny<IReadOnlyCollection<Guid>>(), default)).Returns(Task.CompletedTask);
        var scopeFactory = MakeScopeFactory(repo);
        var buffer = new PersistenceBuffer(scopeFactory, NullLogger<PersistenceBuffer>.Instance, flushIntervalMs: int.MaxValue);
        var registry = new ConcurrencyRegistry(
            new Dictionary<Type, int>(),
            new Dictionary<Type, string?>(),
            new Dictionary<string, int>());
        var retry = new RetryPolicyResolver(new RetryPolicy { MaxRetries = 0 });
        var chainScopeRegistry = new ChainScopeRegistry(scopeFactory, NullLogger<ChainScopeRegistry>.Instance);
        return new QueueOrchestrator(
            NullLogger<QueueOrchestrator>.Instance,
            buffer, scopeFactory, registry, retry,
            new QueueMetrics(), new QueueStateEventHandler(), chainScopeRegistry, maxWorkers);
    }

    private static IServiceScopeFactory MakeScopeFactory(Mock<IJobRepository> repo)
    {
        var sp = new Mock<IServiceProvider>();
        sp.Setup(s => s.GetService(typeof(IJobRepository))).Returns(repo.Object);
        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(sp.Object);
        var factory = new Mock<IServiceScopeFactory>();
        factory.Setup(f => f.CreateScope()).Returns(scope.Object);
        return factory.Object;
    }

    private static WorkerPool MakePool(string name, int maxWorkers, int priority, Type jobType) =>
        new(name, maxWorkers, priority, [jobType], []);

    private static QueuedJob MakeJob(Type jobType) => new()
    {
        Id = Guid.NewGuid(),
        JobType = jobType.FullName + ", " + jobType.Assembly.GetName().Name,
        JobKey = $"key_{Guid.NewGuid()}",
        Priority = 0,
        QueuedAt = DateTimeOffset.UtcNow,
    };

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void HighestPriorityPool_AlwaysAttemptsAcquisition()
    {
        var orchestrator = MakeOrchestrator(10);
        var pool = MakePool("High", 1, priority: 1, typeof(HighPriorityJob));
        orchestrator.Initialize([], [pool]);

        Assert.True(pool.ShouldAttemptAcquisition?.Invoke());
    }

    [Fact]
    public void LowPriorityPool_HighPriorityHasNoRunnableJobs_Proceeds()
    {
        var orchestrator = MakeOrchestrator(10);
        var highPool = MakePool("High", 1, priority: 1, typeof(HighPriorityJob));
        var lowPool = MakePool("Low", 10, priority: AcquisitionAttribute.LowestPriority, typeof(LowPriorityJob));
        orchestrator.Initialize([], [highPool, lowPool]);

        // High pool is empty → reserved = 0, available = 10 → low pool proceeds
        Assert.True(lowPool.ShouldAttemptAcquisition?.Invoke());
    }

    [Fact]
    public void LowPriorityPool_ReservedSlotsLessThanAvailable_Proceeds()
    {
        // maxWorkers=10, high pool reserves 1 → 9 remain for low pool
        var orchestrator = MakeOrchestrator(10);
        var highPool = MakePool("High", 1, priority: 1, typeof(HighPriorityJob));
        var lowPool = MakePool("Low", 10, priority: AcquisitionAttribute.LowestPriority, typeof(LowPriorityJob));
        orchestrator.Initialize([], [highPool, lowPool]);

        highPool.AddToQueue(MakeJob(typeof(HighPriorityJob)));

        // reserved = 1, available = 10 → 10 > 1 → low pool proceeds
        Assert.True(lowPool.ShouldAttemptAcquisition?.Invoke());
    }

    [Fact]
    public void LowPriorityPool_ReservedSlotsEqualAvailable_Blocked()
    {
        // maxWorkers=2, high pool has 2 runnable jobs → reserved = 2, available = 2 → blocked
        var orchestrator = MakeOrchestrator(2);
        var highPool = MakePool("High", 2, priority: 1, typeof(HighPriorityJob));
        var lowPool = MakePool("Low", 2, priority: AcquisitionAttribute.LowestPriority, typeof(LowPriorityJob));
        orchestrator.Initialize([], [highPool, lowPool]);

        highPool.AddToQueue(MakeJob(typeof(HighPriorityJob)));
        highPool.AddToQueue(MakeJob(typeof(HighPriorityJob)));

        // reserved = 2, available = 2 → 2 > 2 is false → low pool blocked
        Assert.False(lowPool.ShouldAttemptAcquisition?.Invoke());
    }

    [Fact]
    public void LowPriorityPool_RunnableCountCappedAtPoolMaxWorkers()
    {
        // High pool maxWorkers=1 but has 5 runnable jobs → reserved = 1 (capped)
        var orchestrator = MakeOrchestrator(3);
        var highPool = MakePool("High", 1, priority: 1, typeof(HighPriorityJob));
        var lowPool = MakePool("Low", 3, priority: AcquisitionAttribute.LowestPriority, typeof(LowPriorityJob));
        orchestrator.Initialize([], [highPool, lowPool]);

        for (var i = 0; i < 5; i++)
            highPool.AddToQueue(MakeJob(typeof(HighPriorityJob)));

        // reserved = min(5, 1) = 1, available = 3 → 3 > 1 → low pool proceeds
        Assert.True(lowPool.ShouldAttemptAcquisition?.Invoke());
    }

    [Fact]
    public void MultipleHighPriorityPools_ReservationsAreAdditive()
    {
        // Two high-priority pools each claiming 1 slot → reserved = 2, maxWorkers = 2 → low pool blocked
        var orchestrator = MakeOrchestrator(2);
        var httpPool = MakePool("HTTP", 1, priority: 1, typeof(HighPriorityJob));
        var udpPool = MakePool("UDP", 1, priority: 2, typeof(LowPriorityJob)); // mid-priority
        var defaultPool = MakePool("Default", 2, priority: AcquisitionAttribute.LowestPriority, typeof(LowPriorityJob));

        // Note: both HighPriorityJob and LowPriorityJob used across pools here, so register
        // them independently without going through the orchestrator's type routing.
        // Instead re-wire TryRegisterExecuting manually to test ShouldAttemptAcquisition only.
        httpPool.TryRegisterExecuting = _ => false;
        udpPool.TryRegisterExecuting = _ => false;
        defaultPool.TryRegisterExecuting = _ => false;

        // Manually wire ShouldAttemptAcquisition using real pools at known priorities
        orchestrator.Initialize([], [httpPool, udpPool, defaultPool]);

        httpPool.AddToQueue(MakeJob(typeof(HighPriorityJob)));
        udpPool.AddToQueue(MakeJob(typeof(LowPriorityJob)));

        // From defaultPool's perspective: reserved = 1 (HTTP) + 1 (UDP) = 2, available = 2 → blocked
        Assert.False(defaultPool.ShouldAttemptAcquisition?.Invoke());
    }

    [Fact]
    public void MidPriorityPool_OnlyYieldsToHigherPriorityPools()
    {
        // UDP (priority 2) should yield only to HTTP (priority 1), not to Default (priority 999)
        var orchestrator = MakeOrchestrator(3);
        var httpPool = MakePool("HTTP", 1, priority: 1, typeof(HighPriorityJob));
        var udpPool = MakePool("UDP", 1, priority: 2, typeof(LowPriorityJob));
        var defaultPool = MakePool("Default", 3, priority: AcquisitionAttribute.LowestPriority, typeof(LowPriorityJob));

        httpPool.TryRegisterExecuting = _ => false;
        udpPool.TryRegisterExecuting = _ => false;
        defaultPool.TryRegisterExecuting = _ => false;

        orchestrator.Initialize([], [httpPool, udpPool, defaultPool]);

        httpPool.AddToQueue(MakeJob(typeof(HighPriorityJob)));

        // UDP: reserved = 1 (HTTP only), available = 3 → 3 > 1 → proceeds
        Assert.True(udpPool.ShouldAttemptAcquisition?.Invoke());
    }
}
