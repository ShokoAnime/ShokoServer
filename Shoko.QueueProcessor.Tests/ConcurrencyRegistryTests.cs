using System;
using System.Collections.Generic;
using Shoko.QueueProcessor.Concurrency;
using Shoko.QueueProcessor.Orchestration;
using Xunit;

namespace Shoko.QueueProcessor.Tests;

/// <summary>Tests for <see cref="ConcurrencyRegistry"/> limit enforcement and override logic.</summary>
public class ConcurrencyRegistryTests
{
    // ── Fixture job types ─────────────────────────────────────────────────────

    [LimitConcurrency(2, maxAllowedConcurrentJobs: 4)]
    [DisallowConcurrencyGroup("AniDB_UDP")]
    private class AniDBUdpJobA { }

    [LimitConcurrency(2, maxAllowedConcurrentJobs: 4)]
    [DisallowConcurrencyGroup("AniDB_UDP")]
    private class AniDBUdpJobB { }

    [LimitConcurrency(3)]
    private class HashFileJob { }

    [DisallowConcurrentExecution]
    private class SingletonJob { }

    private class UnlimitedJob { }

    // ── Build tests ────────────────────────────────────────────────────────────

    [Fact]
    public void Build_TypeLimit_FromAttribute()
    {
        var registry = ConcurrencyRegistry.Build([typeof(HashFileJob)]);
        Assert.Equal(3, registry.GetTypeLimit(typeof(HashFileJob)));
    }

    [Fact]
    public void Build_DisallowConcurrentExecution_LimitIsOne()
    {
        var registry = ConcurrencyRegistry.Build([typeof(SingletonJob)]);
        Assert.Equal(1, registry.GetTypeLimit(typeof(SingletonJob)));
    }

    [Fact]
    public void Build_UnlimitedType_ReturnsMaxValue()
    {
        var registry = ConcurrencyRegistry.Build([typeof(UnlimitedJob)]);
        Assert.Equal(int.MaxValue, registry.GetTypeLimit(typeof(UnlimitedJob)));
    }

    [Fact]
    public void Build_GroupLimit_DerivedFromMinTypeLimit()
    {
        var registry = ConcurrencyRegistry.Build([typeof(AniDBUdpJobA), typeof(AniDBUdpJobB)]);
        Assert.Equal(2, registry.GetGroupLimit("AniDB_UDP"));
    }

    [Fact]
    public void Build_GroupAssignment()
    {
        var registry = ConcurrencyRegistry.Build([typeof(AniDBUdpJobA)]);
        Assert.Equal("AniDB_UDP", registry.GetGroup(typeof(AniDBUdpJobA)));
    }

    [Fact]
    public void Build_NoGroup_ReturnsNull()
    {
        var registry = ConcurrencyRegistry.Build([typeof(HashFileJob)]);
        Assert.Null(registry.GetGroup(typeof(HashFileJob)));
    }

    // ── Override tests ────────────────────────────────────────────────────────

    [Fact]
    public void Build_Override_LowersLimit()
    {
        var overrides = new Dictionary<string, int> { ["HashFileJob"] = 1 };
        var registry = ConcurrencyRegistry.Build([typeof(HashFileJob)], overrides);
        Assert.Equal(1, registry.GetTypeLimit(typeof(HashFileJob)));
    }

    [Fact]
    public void Build_Override_CannotExceedMaxAllowed()
    {
        // MaxAllowed=4, override requests 10 → capped at 4
        var overrides = new Dictionary<string, int> { ["AniDBUdpJobA"] = 10 };
        var registry = ConcurrencyRegistry.Build([typeof(AniDBUdpJobA)], overrides);
        Assert.Equal(4, registry.GetTypeLimit(typeof(AniDBUdpJobA)));
    }

    [Fact]
    public void Build_Override_CannotOverrideSingletonJob()
    {
        // DisallowConcurrentExecution = limit of 1; override should not lower below 1
        // (and also: we check that singleton is not overrideable by design)
        var overrides = new Dictionary<string, int> { ["SingletonJob"] = 5 };
        var registry = ConcurrencyRegistry.Build([typeof(SingletonJob)], overrides);
        // MaxAllowedConcurrentJobs = 1 (from DisallowConcurrentExecution), so override of 5 is capped to 1
        Assert.Equal(1, registry.GetTypeLimit(typeof(SingletonJob)));
    }

    // ── CanRun tests ──────────────────────────────────────────────────────────

    [Fact]
    public void CanRun_BelowTypeLimit_ReturnsTrue()
    {
        var registry = ConcurrencyRegistry.Build([typeof(HashFileJob)]);
        var typeCounts = new Dictionary<Type, int> { [typeof(HashFileJob)] = 2 };
        var groupCounts = new Dictionary<string, int>();

        Assert.True(registry.CanRun(typeof(HashFileJob), typeCounts, groupCounts));
    }

    [Fact]
    public void CanRun_AtTypeLimit_ReturnsFalse()
    {
        var registry = ConcurrencyRegistry.Build([typeof(HashFileJob)]);
        var typeCounts = new Dictionary<Type, int> { [typeof(HashFileJob)] = 3 }; // at limit
        var groupCounts = new Dictionary<string, int>();

        Assert.False(registry.CanRun(typeof(HashFileJob), typeCounts, groupCounts));
    }

    [Fact]
    public void CanRun_BelowGroupLimit_ReturnsTrue()
    {
        var registry = ConcurrencyRegistry.Build([typeof(AniDBUdpJobA), typeof(AniDBUdpJobB)]);
        var typeCounts = new Dictionary<Type, int> { [typeof(AniDBUdpJobA)] = 1 };
        var groupCounts = new Dictionary<string, int> { ["AniDB_UDP"] = 1 };

        Assert.True(registry.CanRun(typeof(AniDBUdpJobB), typeCounts, groupCounts));
    }

    [Fact]
    public void CanRun_AtGroupLimit_ReturnsFalse()
    {
        var registry = ConcurrencyRegistry.Build([typeof(AniDBUdpJobA), typeof(AniDBUdpJobB)]);
        var typeCounts = new Dictionary<Type, int>();
        var groupCounts = new Dictionary<string, int> { ["AniDB_UDP"] = 2 }; // group at limit

        Assert.False(registry.CanRun(typeof(AniDBUdpJobB), typeCounts, groupCounts));
    }

    [Fact]
    public void CanRun_UnlimitedType_AlwaysTrue()
    {
        var registry = ConcurrencyRegistry.Build([typeof(UnlimitedJob)]);
        var typeCounts = new Dictionary<Type, int> { [typeof(UnlimitedJob)] = 999 };
        var groupCounts = new Dictionary<string, int>();

        Assert.True(registry.CanRun(typeof(UnlimitedJob), typeCounts, groupCounts));
    }

    [Fact]
    public void CanRun_SingletonRunning_ReturnsFalse()
    {
        var registry = ConcurrencyRegistry.Build([typeof(SingletonJob)]);
        var typeCounts = new Dictionary<Type, int> { [typeof(SingletonJob)] = 1 };
        var groupCounts = new Dictionary<string, int>();

        Assert.False(registry.CanRun(typeof(SingletonJob), typeCounts, groupCounts));
    }

    [Fact]
    public void CanRun_SingletonNotRunning_ReturnsTrue()
    {
        var registry = ConcurrencyRegistry.Build([typeof(SingletonJob)]);
        var typeCounts = new Dictionary<Type, int>();
        var groupCounts = new Dictionary<string, int>();

        Assert.True(registry.CanRun(typeof(SingletonJob), typeCounts, groupCounts));
    }
}
