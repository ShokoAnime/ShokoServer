using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Concurrency;
using Shoko.QueueProcessor.Orchestration;
using Xunit;

namespace Shoko.QueueProcessor.Tests;

/// <summary>Tests for <see cref="RetryPolicy"/> backoff schedule and <see cref="RetryPolicyResolver"/> per-type overrides.</summary>
public class RetryPolicyTests
{
    private static readonly RetryPolicy _global = new()
    {
        MaxRetries = 8,
        BaseDelay = TimeSpan.FromSeconds(30),
        MaxDelay = TimeSpan.FromHours(1)
    };

    private static readonly RetryPolicyResolver _resolver = new(_global);

    // ── RetryPolicy.GetDelay ──────────────────────────────────────────────────

    [Theory]
    [InlineData(0,   30)]    // first retry: base * 2^0 = 30s
    [InlineData(1,   60)]    // 30 * 2 = 60s
    [InlineData(2,  120)]    // 30 * 4 = 120s
    [InlineData(3,  240)]
    [InlineData(4,  480)]
    [InlineData(5,  960)]
    [InlineData(6, 1920)]
    [InlineData(7, 3600)]    // capped at 1h
    [InlineData(20, 3600)]   // far into retries — still capped
    public void GetDelay_ExponentialWithCap(int retryCount, double expectedSeconds)
    {
        var delay = _global.GetDelay(retryCount);
        Assert.Equal(expectedSeconds, delay.TotalSeconds, precision: 0);
    }

    [Fact]
    public void ShouldDiscard_BelowMax_ReturnsFalse()
    {
        Assert.False(_global.ShouldDiscard(7));
    }

    [Fact]
    public void ShouldDiscard_AtMax_ReturnsTrue()
    {
        Assert.True(_global.ShouldDiscard(8));
    }

    [Fact]
    public void ShouldDiscard_AboveMax_ReturnsTrue()
    {
        Assert.True(_global.ShouldDiscard(100));
    }

    // ── Zero-retry policy ─────────────────────────────────────────────────────

    [Fact]
    public void ShouldDiscard_ZeroMaxRetries_ImmediatelyDiscards()
    {
        var noRetry = new RetryPolicy { MaxRetries = 0 };
        Assert.True(noRetry.ShouldDiscard(0));
    }

    // ── RetryPolicyResolver ───────────────────────────────────────────────────

    private class NoRetryJob : IQueueJob
    {
        public string TypeName => "NoRetryJob";
        public string Title => "";
        public Dictionary<string, object> Details => [];
        public void PostInit() { }
        public Task Process() => Task.CompletedTask;
    }

    [RetryPolicy(MaxRetries = 0)]
    private class ZeroRetryAnnotatedJob : IQueueJob
    {
        public string TypeName => "ZeroRetryAnnotatedJob";
        public string Title => "";
        public Dictionary<string, object> Details => [];
        public void PostInit() { }
        public Task Process() => Task.CompletedTask;
    }

    [RetryPolicy(MaxRetries = 3, BaseDelaySeconds = 60, MaxDelaySeconds = 600)]
    private class ShortRetryJob : IQueueJob
    {
        public string TypeName => "ShortRetryJob";
        public string Title => "";
        public Dictionary<string, object> Details => [];
        public void PostInit() { }
        public Task Process() => Task.CompletedTask;
    }

    [Fact]
    public void Resolver_NoAttribute_ReturnsGlobal()
    {
        var policy = _resolver.For(typeof(NoRetryJob));

        Assert.Equal(8, policy.MaxRetries);
        Assert.Equal(30, policy.BaseDelay.TotalSeconds);
    }

    [Fact]
    public void Resolver_ZeroRetryAttribute_ReturnsZeroMax()
    {
        var policy = _resolver.For(typeof(ZeroRetryAnnotatedJob));

        Assert.Equal(0, policy.MaxRetries);
        Assert.True(policy.ShouldDiscard(0));
    }

    [Fact]
    public void Resolver_PartialAttributeOverride_MergesWithGlobal()
    {
        var policy = _resolver.For(typeof(ShortRetryJob));

        Assert.Equal(3, policy.MaxRetries);
        Assert.Equal(60, policy.BaseDelay.TotalSeconds);
        Assert.Equal(600, policy.MaxDelay.TotalSeconds);
    }

    [Fact]
    public void Resolver_ShortRetry_CappedByMaxDelay()
    {
        var policy = _resolver.For(typeof(ShortRetryJob));

        var delay = policy.GetDelay(10); // way beyond the cap
        Assert.Equal(600, delay.TotalSeconds);
    }
}
