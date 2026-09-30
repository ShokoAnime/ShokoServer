using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Analytics;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Chain;
using Shoko.QueueProcessor.Events;
using Shoko.QueueProcessor.Orchestration;
using Shoko.QueueProcessor.Scheduling;
using Shoko.QueueProcessor.Storage;
using Shoko.QueueProcessor.Storage.Contexts;
using Shoko.QueueProcessor.Workers;
using Xunit;

namespace Shoko.QueueProcessor.Tests;

/// <summary>
/// Covers closed generic job types, one per type argument: how they are named, registered,
/// limited and stored, and that a stored one finds its type again.
/// </summary>
public class GenericJobTypeTests
{
    #region Fixtures

    public class ProviderA;

    public class ProviderB;

    /// <summary>
    /// Another plugin, whose provider shares <see cref="ProviderA"/>'s simple name.
    /// </summary>
    public static class OtherPlugin
    {
        public class ProviderA;
    }

    public class PerProviderJob<TProvider> : IQueueJob where TProvider : class
    {
        public int AnimeID { get; set; }

        public string TypeName => "Per Provider";

        public string Title => "";

        public Dictionary<string, object> Details => [];

        public void PostInit() { }

        public Task Process() => Task.CompletedTask;
    }

    private sealed class FixedLimits(Dictionary<Type, int> limits) : IJobConcurrencyProvider
    {
        public int? GetConcurrencyLimit(Type jobType)
            => limits.TryGetValue(jobType, out var limit) ? limit : null;
    }

    #endregion

    #region Names

    [Fact]
    public void AClosedTypeIsStoredWithoutAssemblyVersions()
    {
        var stored = JobTypeNames.Stored(typeof(PerProviderJob<ProviderA>));
        var assembly = typeof(ProviderA).Assembly.GetName().Name;

        Assert.DoesNotContain("Version=", stored);
        Assert.Equal(
            $"{typeof(PerProviderJob<>).FullName}[[{typeof(ProviderA).FullName}, {assembly}]], {assembly}",
            stored
        );
    }

    [Fact]
    public void APlainTypeKeepsItsOldNames()
    {
        Assert.Equal(typeof(ProviderA).FullName + ", " + typeof(ProviderA).Assembly.GetName().Name, JobTypeNames.Stored(typeof(ProviderA)));
        Assert.Equal(nameof(ProviderA), JobTypeNames.Short(typeof(ProviderA)));
    }

    [Fact]
    public void ProvidersOfTheSameSimpleNameGetDifferentKeys()
    {
        var own = typeof(PerProviderJob<ProviderA>);
        var other = typeof(PerProviderJob<OtherPlugin.ProviderA>);

        Assert.Equal(JobTypeNames.Short(own), JobTypeNames.Short(other));
        Assert.NotEqual(JobTypeNames.Short(own), JobTypeNames.Short(typeof(PerProviderJob<ProviderB>)));
        Assert.NotEqual(JobTypeNames.Key(own), JobTypeNames.Key(other));
        Assert.Equal($"PerProviderJob<{typeof(ProviderA).FullName}>", JobTypeNames.Key(own));
        Assert.Equal(nameof(ProviderA), JobTypeNames.Key(typeof(ProviderA)));
    }

    [Fact]
    public void AClosedTypesKeyHasNoAssemblyVersion()
    {
        var key = JobKeyBuilder<IQueueJob>.BuildForType(typeof(PerProviderJob<ProviderA>), new() { ["AnimeID"] = 7 });

        Assert.DoesNotContain("Version=", key);
        Assert.StartsWith(JobTypeNames.Full(typeof(PerProviderJob<ProviderA>)), key);
        Assert.NotEqual(key, JobKeyBuilder<IQueueJob>.BuildForType(typeof(PerProviderJob<ProviderB>), new() { ["AnimeID"] = 7 }));
    }

    #endregion

    #region Registration

    [Fact]
    public void AnAssemblyScanSkipsOpenGenericJobTypes()
    {
        var services = new ServiceCollection();
        services.AddQueueProcessor(_ => { }, typeof(GenericJobTypeTests).Assembly);
        var registry = (QueueJobTypeRegistry)services.Single(d => d.ServiceType == typeof(QueueJobTypeRegistry)).ImplementationInstance!;

        Assert.DoesNotContain(registry.JobTypes, type => type.ContainsGenericParameters);
    }

    [Fact]
    public void ClosedTypesAreRegisteredAsServicesAndJobTypes()
    {
        var services = new ServiceCollection();
        services.AddQueueProcessor(_ => { });
        services.AddQueueJobTypes([typeof(PerProviderJob<ProviderA>), typeof(PerProviderJob<ProviderB>)]);
        var registry = (QueueJobTypeRegistry)services.Single(d => d.ServiceType == typeof(QueueJobTypeRegistry)).ImplementationInstance!;

        Assert.Contains(typeof(PerProviderJob<ProviderA>), registry.JobTypes);
        Assert.Contains(typeof(PerProviderJob<ProviderB>), registry.JobTypes);
        Assert.Contains(services, d => d.ServiceType == typeof(PerProviderJob<ProviderB>));
    }

    [Fact]
    public void AnOpenTypeIsRefused()
    {
        var services = new ServiceCollection();
        services.AddQueueProcessor(_ => { });

        Assert.Throws<ArgumentException>(() => services.AddQueueJobTypes([typeof(PerProviderJob<>)]));
    }

    [Fact]
    public void ATypeStoredUnderTooLongANameIsRefused()
    {
        var services = new ServiceCollection();
        services.AddQueueProcessor(_ => { });
        var overlong = typeof(PerProviderJob<PerProviderJob<PerProviderJob<ProviderA>>>);

        Assert.True(JobTypeNames.Stored(overlong).Length > QueuedJob.JobTypeMaxLength);
        Assert.False(QueueProcessorExtensions.FitsQueue(overlong));
        Assert.True(QueueProcessorExtensions.FitsQueue(typeof(PerProviderJob<ProviderA>)));
        Assert.Throws<ArgumentException>(() => services.AddQueueJobTypes([overlong]));
    }

    #endregion

    #region Concurrency

    [Fact]
    public void AProvidedLimitIsReadWhenAsked_ForItsClosedTypeOnly()
    {
        var table = new Dictionary<Type, int>();
        var registry = ConcurrencyRegistry.Build(
            [typeof(PerProviderJob<ProviderA>), typeof(PerProviderJob<ProviderB>)],
            limitProviders: [new FixedLimits(table)]
        );
        Assert.Equal(int.MaxValue, registry.GetTypeLimit(typeof(PerProviderJob<ProviderA>)));

        table[typeof(PerProviderJob<ProviderA>)] = 3;

        Assert.Equal(3, registry.GetTypeLimit(typeof(PerProviderJob<ProviderA>)));
        Assert.Equal(int.MaxValue, registry.GetTypeLimit(typeof(PerProviderJob<ProviderB>)));
    }

    [Fact]
    public void AProvidedLimitGivesTheClosedTypeAPoolOfItsOwn()
    {
        var limits = new FixedLimits(new() { [typeof(PerProviderJob<ProviderA>)] = 1 });
        var pools = new PoolDiscovery(NullLogger<PoolDiscovery>.Instance, 10, 4)
            .Discover([typeof(PerProviderJob<ProviderA>), typeof(PerProviderJob<ProviderB>)], [], [limits]);

        var own = Assert.Single(pools, pool => pool.Name == JobTypeNames.Key(typeof(PerProviderJob<ProviderA>)));
        Assert.Equal(1, own.MaxWorkers);
        Assert.Contains(typeof(PerProviderJob<ProviderB>), Assert.Single(pools, pool => pool.Name == "Default").HandledTypes);
    }

    [Fact]
    public void ProvidersOfTheSameSimpleNameKeepTheirOwnPoolsAndLimits()
    {
        var own = typeof(PerProviderJob<ProviderA>);
        var other = typeof(PerProviderJob<OtherPlugin.ProviderA>);
        var limits = new FixedLimits(new() { [own] = 4, [other] = 1 });
        var pools = new PoolDiscovery(NullLogger<PoolDiscovery>.Instance, 10, 4).Discover([own, other], [], [limits]);

        Assert.Equal(4, Assert.Single(pools, pool => pool.HandledTypes.Contains(own)).MaxWorkers);
        Assert.Equal(1, Assert.Single(pools, pool => pool.HandledTypes.Contains(other)).MaxWorkers);
    }

    #endregion

    #region Persistence

    [Fact]
    public async Task AStoredClosedJobFindsItsTypeAgain()
    {
        var connectionString = $"Data Source=test_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        await using var keeper = new SqliteConnection(connectionString);
        keeper.Open();
        await using (var migration = new SqliteQueueDbContext(connectionString))
            await migration.Database.MigrateAsync(TestContext.Current.CancellationToken);

        var repository = new JobRepository(new TestDbContextFactory(connectionString));
        var type = typeof(PerProviderJob<ProviderA>);
        var context = QueueScheduler.BuildContext(type, "per-provider-a", new PerProviderJob<ProviderA> { AnimeID = 7 }, 0, null);
        await repository.InsertBatchAsync([context.Job], TestContext.Current.CancellationToken);

        var loaded = Assert.Single(await repository.LoadAllAsync(TestContext.Current.CancellationToken));
        Assert.Equal(JobTypeNames.Stored(type), loaded.JobType);
        Assert.True(loaded.JobType.Length <= 256);

        var (orchestrator, deleted) = Orchestrator();
        orchestrator.Initialize(
            [loaded],
            [new WorkerPool("Pool", 1, 0, [type, typeof(PerProviderJob<ProviderB>)], [])]
        );

        Assert.True(orchestrator.IsQueued("per-provider-a"));
        Assert.Empty(deleted);
    }

    [Fact]
    public void APoolFindsAClosedJobByItsStoredName()
    {
        var type = typeof(PerProviderJob<ProviderA>);
        var pool = new WorkerPool("Pool", 1, 0, [type, typeof(ProviderB)], []);

        Assert.Equal(type, pool.ResolveJobType(JobTypeNames.Stored(type)));
        Assert.Equal(typeof(ProviderB), pool.ResolveJobType(JobTypeNames.Stored(typeof(ProviderB))));
    }

    private sealed class TestDbContextFactory(string connectionString) : IDbContextFactory<QueueDbContext>
    {
        public QueueDbContext CreateDbContext() => new SqliteQueueDbContext(connectionString);
    }

    private static (QueueOrchestrator Orchestrator, List<Guid> Deleted) Orchestrator()
    {
        var deleted = new List<Guid>();
        var repo = new Mock<IJobRepository>();
        repo.Setup(r => r.DeleteBatchAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<Guid>, CancellationToken>((ids, _) =>
            {
                lock (deleted)
                    deleted.AddRange(ids);
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
        return (orchestrator, deleted);
    }

    #endregion
}
