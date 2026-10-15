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

    [Theory]
    [InlineData(typeof(HashFileJob), 3)] // LimitConcurrency
    [InlineData(typeof(SingletonJob), 1)] // DisallowConcurrentExecution
    [InlineData(typeof(UnlimitedJob), int.MaxValue)] // No attribute
    public void Build_TypeLimit_FollowsTheTypesAttributes(Type jobType, int expectedLimit)
    {
        var registry = ConcurrencyRegistry.Build([jobType]);
        Assert.Equal(expectedLimit, registry.GetTypeLimit(jobType));
    }

    [Fact]
    public void Build_GroupLimit_DerivedFromMinTypeLimit()
    {
        var registry = ConcurrencyRegistry.Build([typeof(AniDBUdpJobA), typeof(AniDBUdpJobB)]);
        Assert.Equal(2, registry.GetGroupLimit("AniDB_UDP"));
    }

    [Theory]
    [InlineData(typeof(AniDBUdpJobA), "AniDB_UDP")]
    [InlineData(typeof(HashFileJob), null)] // No group attribute
    public void Build_Group_FollowsTheTypesAttributes(Type jobType, string? expectedGroup)
    {
        var registry = ConcurrencyRegistry.Build([jobType]);
        Assert.Equal(expectedGroup, registry.GetGroup(jobType));
    }

    // ── Override tests ────────────────────────────────────────────────────────

    [Theory]
    [InlineData(typeof(HashFileJob), 1, 1)] // Lowers the limit
    [InlineData(typeof(AniDBUdpJobA), 10, 4)] // MaxAllowed=4, override requests 10, capped at 4
    [InlineData(typeof(SingletonJob), 5, 1)] // DisallowConcurrentExecution sets MaxAllowed=1, so 5 is capped to 1
    public void Build_Override_AdjustsTheLimitUpToMaxAllowed(Type jobType, int overrideValue, int expectedLimit)
    {
        var overrides = new Dictionary<string, int> { [jobType.Name] = overrideValue };
        var registry = ConcurrencyRegistry.Build([jobType], overrides);
        Assert.Equal(expectedLimit, registry.GetTypeLimit(jobType));
    }

    // ── CanRun tests ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(typeof(HashFileJob), 2, true)]
    [InlineData(typeof(HashFileJob), 3, false)] // At the limit
    [InlineData(typeof(UnlimitedJob), 999, true)]
    [InlineData(typeof(SingletonJob), 1, false)]
    [InlineData(typeof(SingletonJob), 0, true)]
    public void CanRun_ComparesTheRunningCountToTheTypeLimit(Type jobType, int runningCount, bool expected)
    {
        var registry = ConcurrencyRegistry.Build([jobType]);
        var typeCounts = new Dictionary<Type, int> { [jobType] = runningCount };
        var groupCounts = new Dictionary<string, int>();

        Assert.Equal(expected, registry.CanRun(jobType, typeCounts, groupCounts));
    }

    [Theory]
    [InlineData(1, 1, true)]
    [InlineData(0, 2, false)] // Group at limit
    public void CanRun_ComparesTheRunningCountToTheGroupLimit(int siblingRunning, int groupRunning, bool expected)
    {
        var registry = ConcurrencyRegistry.Build([typeof(AniDBUdpJobA), typeof(AniDBUdpJobB)]);
        var typeCounts = new Dictionary<Type, int> { [typeof(AniDBUdpJobA)] = siblingRunning };
        var groupCounts = new Dictionary<string, int> { ["AniDB_UDP"] = groupRunning };

        Assert.Equal(expected, registry.CanRun(typeof(AniDBUdpJobB), typeCounts, groupCounts));
    }
}
