using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Concurrency;
using Shoko.QueueProcessor.Orchestration;
using Xunit;

namespace Shoko.QueueProcessor.Tests;

/// <summary>Tests for <see cref="PoolDiscovery"/> automatic pool creation from attributes.</summary>
public class PoolDiscoveryTests
{
    // ── Fixture job types ─────────────────────────────────────────────────────

    [LimitConcurrency(1)]
    [DisallowConcurrencyGroup("AniDB_UDP")]
    private class GetAniDBAnimeJob : IQueueJob
    {
        public string TypeName => "GetAniDBAnimeJob";
        public string Title => "";
        public Dictionary<string, object> Details => [];
        public void PostInit() { }
        public Task Process() => Task.CompletedTask;
    }

    [LimitConcurrency(1)]
    [DisallowConcurrencyGroup("AniDB_UDP")]
    private class SyncAniDBMylistJob : IQueueJob
    {
        public string TypeName => "SyncAniDBMylistJob";
        public string Title => "";
        public Dictionary<string, object> Details => [];
        public void PostInit() { }
        public Task Process() => Task.CompletedTask;
    }

    [LimitConcurrency(2, maxAllowedConcurrentJobs: 4)]
    private class HashFileJob : IQueueJob
    {
        public string TypeName => "HashFileJob";
        public string Title => "";
        public Dictionary<string, object> Details => [];
        public void PostInit() { }
        public Task Process() => Task.CompletedTask;
    }

    private class GeneralJob : IQueueJob
    {
        public string TypeName => "GeneralJob";
        public string Title => "";
        public Dictionary<string, object> Details => [];
        public void PostInit() { }
        public Task Process() => Task.CompletedTask;
    }

    // Attribute used to associate a filter with UDP jobs
    private class AniDBUdpAttribute : Attribute { }

    private PoolDiscovery MakeDiscovery(int maxTotal = 10, int defaultSize = 4) =>
        new(NullLogger<PoolDiscovery>.Instance, maxTotal, defaultSize);

    // ── Pool creation tests ───────────────────────────────────────────────────

    [Fact]
    public void Discover_ConcurrencyGroupTypes_GoToSamePool()
    {
        var sut = MakeDiscovery();
        var pools = sut.Discover(
            [typeof(GetAniDBAnimeJob), typeof(SyncAniDBMylistJob)],
            []);

        var udpPool = pools.SingleOrDefault(p => p.Name == "AniDB_UDP");
        Assert.NotNull(udpPool);
        Assert.Contains(typeof(GetAniDBAnimeJob), udpPool.HandledTypes);
        Assert.Contains(typeof(SyncAniDBMylistJob), udpPool.HandledTypes);
    }

    [Fact]
    public void Discover_GroupPool_SizeFromLimitAttribute()
    {
        var sut = MakeDiscovery();
        var pools = sut.Discover([typeof(GetAniDBAnimeJob)], []);

        var udpPool = pools.Single(p => p.Name == "AniDB_UDP");
        Assert.Equal(1, udpPool.MaxWorkers);
    }

    [Fact]
    public void Discover_LimitOnlyType_GetsDedicatedPool()
    {
        var sut = MakeDiscovery();
        var pools = sut.Discover([typeof(HashFileJob)], []);

        var hashPool = pools.SingleOrDefault(p => p.Name == "HashFileJob");
        Assert.NotNull(hashPool);
        Assert.Equal(2, hashPool.MaxWorkers);
        Assert.Contains(typeof(HashFileJob), hashPool.HandledTypes);
    }

    [Fact]
    public void Discover_UnlimitedType_GoesToDefaultPool()
    {
        var sut = MakeDiscovery();
        var pools = sut.Discover([typeof(GeneralJob)], []);

        var defaultPool = pools.SingleOrDefault(p => p.Name == "Default");
        Assert.NotNull(defaultPool);
        Assert.Contains(typeof(GeneralJob), defaultPool.HandledTypes);
    }

    [Fact]
    public void Discover_NoJobTypes_OnlyDefaultPool()
    {
        var sut = MakeDiscovery();
        var pools = sut.Discover([], []);

        Assert.Single(pools);
        Assert.Equal("Default", pools[0].Name);
    }

    [Fact]
    public void Discover_MixedTypes_CorrectPoolCount()
    {
        var sut = MakeDiscovery();
        var pools = sut.Discover(
            [typeof(GetAniDBAnimeJob), typeof(SyncAniDBMylistJob), typeof(HashFileJob), typeof(GeneralJob)],
            []);

        // AniDB_UDP + HashFile + Default = 3 pools
        Assert.Equal(3, pools.Count);
    }

    // ── Acquisition filter linkage tests ──────────────────────────────────────

    [Fact]
    public void Discover_GlobalFilter_AttachedToDefaultOnly()
    {
        var globalFilter = new Mock<IAcquisitionFilter>();
        globalFilter.Setup(f => f.WatchedAttributeType).Returns((Type?)null);
        globalFilter.Setup(f => f.GetTypesToExclude()).Returns([]);

        var sut = MakeDiscovery();
        var pools = sut.Discover([typeof(GetAniDBAnimeJob), typeof(GeneralJob)], [globalFilter.Object]);

        // Global filter on Default pool
        var defaultPool = pools.Single(p => p.Name == "Default");
        Assert.Contains(globalFilter.Object, defaultPool.AcquisitionFilters);

        // Not on AniDB_UDP pool
        var udpPool = pools.Single(p => p.Name == "AniDB_UDP");
        Assert.DoesNotContain(globalFilter.Object, udpPool.AcquisitionFilters);
    }

    // ── MaxTotalWorkers cap tests ──────────────────────────────────────────────

    [Fact]
    public void Discover_PoolSize_CappedByMaxTotalWorkers()
    {
        // maxTotalWorkers = 1, HashFileJob wants 2 → capped to 1
        var sut = MakeDiscovery(maxTotal: 1, defaultSize: 1);
        var pools = sut.Discover([typeof(HashFileJob)], []);

        var pool = pools.Single(p => p.Name == "HashFileJob");
        Assert.Equal(1, pool.MaxWorkers);
    }

    // ── Override tests ────────────────────────────────────────────────────────

    [Theory]
    [InlineData(3, 10, 3)] // MaxAllowed=4, override=3, so 3 is within bounds
    [InlineData(10, 20, 4)] // MaxAllowed=4, so the override is capped at it
    public void Discover_Override_AdjustsPoolSizeUpToMaxAllowed(int overrideValue, int maxTotal, int expectedWorkers)
    {
        var overrides = new Dictionary<string, int> { ["HashFileJob"] = overrideValue };
        var sut = new PoolDiscovery(NullLogger<PoolDiscovery>.Instance, maxTotal, 4, overrides);
        var pools = sut.Discover([typeof(HashFileJob)], []);

        var pool = pools.Single(p => p.Name == "HashFileJob");
        Assert.Equal(expectedWorkers, pool.MaxWorkers);
    }
}
