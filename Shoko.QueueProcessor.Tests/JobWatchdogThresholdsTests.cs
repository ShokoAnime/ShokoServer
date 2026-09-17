using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging.Abstractions;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Concurrency;
using Shoko.QueueProcessor.Workers;
using Xunit;

namespace Shoko.QueueProcessor.Tests;

/// <summary>
/// Tests for <see cref="JobWatchdogThresholds"/>: which job types the watchdog watches, and what it
/// watches each of them against.
/// </summary>
public class JobWatchdogThresholdsTests
{
    // ── Fixture job types ─────────────────────────────────────────────────────

    private class PlainJob { }

    [LongRunning]
    private class ExemptJob { }

    private class DeadlinedJob { }

    // ── Fixture thresholds ────────────────────────────────────────────────────

    /// <summary>A threshold that answers with whatever the test last set.</summary>
    private sealed class StubThreshold(Type jobType, Func<TimeSpan, TimeSpan?> answer) : IJobWatchdogThreshold
    {
        public int Calls { get; private set; }

        public Type JobType { get; } = jobType;

        public TimeSpan? GetThreshold(TimeSpan defaultThreshold)
        {
            Calls++;
            return answer(defaultThreshold);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static readonly TimeSpan _default = TimeSpan.FromSeconds(90);

    private static JobWatchdogThresholds Build(params IJobWatchdogThreshold[] thresholds)
        => new(_default, [typeof(PlainJob), typeof(ExemptJob), typeof(DeadlinedJob)], thresholds, NullLogger.Instance);

    // ── Without a declared threshold ──────────────────────────────────────────

    [Fact]
    public void For_JobDeclaringNothing_UsesTheGlobalThreshold()
    {
        Assert.Equal(_default, Build().For(typeof(PlainJob)));
    }

    [Fact]
    public void For_UnknownJobType_UsesTheGlobalThreshold()
    {
        Assert.Equal(_default, Build().For(typeof(JobWatchdogThresholdsTests)));
    }

    [Fact]
    public void For_LongRunningJob_IsNotWatchedAtAll()
    {
        Assert.Null(Build().For(typeof(ExemptJob)));
    }

    // ── With a declared threshold ─────────────────────────────────────────────

    [Fact]
    public void For_DeclaredThreshold_IsHonoured()
    {
        var threshold = new StubThreshold(typeof(DeadlinedJob), _ => TimeSpan.FromMinutes(15));
        var thresholds = Build(threshold);

        Assert.Equal(TimeSpan.FromMinutes(15), thresholds.For(typeof(DeadlinedJob)));
        // Only the type that declared it. Everything else is on the global threshold.
        Assert.Equal(_default, thresholds.For(typeof(PlainJob)));
    }

    [Fact]
    public void For_DeclaredThreshold_IsHandedTheGlobalThreshold()
    {
        var seen = TimeSpan.Zero;
        var thresholds = Build(new StubThreshold(typeof(DeadlinedJob), given =>
        {
            seen = given;
            return null;
        }));

        thresholds.For(typeof(DeadlinedJob));

        Assert.Equal(_default, seen);
    }

    [Fact]
    public void For_DeclaredThreshold_IsAskedEveryTime()
    {
        var answer = TimeSpan.FromMinutes(5);
        var threshold = new StubThreshold(typeof(DeadlinedJob), _ => answer);
        var thresholds = Build(threshold);

        Assert.Equal(TimeSpan.FromMinutes(5), thresholds.For(typeof(DeadlinedJob)));
        answer = TimeSpan.FromMinutes(20);

        // Worked out per poll rather than pinned at startup, so a threshold that
        // follows a setting follows it while the server runs.
        Assert.Equal(TimeSpan.FromMinutes(20), thresholds.For(typeof(DeadlinedJob)));
        Assert.Equal(2, threshold.Calls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void For_DeclaredThresholdThatIsNotPositive_FallsBackToTheGlobalThreshold(int seconds)
    {
        var thresholds = Build(new StubThreshold(typeof(DeadlinedJob), _ => TimeSpan.FromSeconds(seconds)));

        Assert.Equal(_default, thresholds.For(typeof(DeadlinedJob)));
    }

    [Fact]
    public void For_DeclaredThresholdReturningNull_FallsBackToTheGlobalThreshold()
    {
        var thresholds = Build(new StubThreshold(typeof(DeadlinedJob), _ => null));

        Assert.Equal(_default, thresholds.For(typeof(DeadlinedJob)));
    }

    [Fact]
    public void For_DeclaredThresholdThatThrows_FallsBackToTheGlobalThresholdAndKeepsWatching()
    {
        var thresholds = Build(new StubThreshold(typeof(DeadlinedJob), _ => throw new InvalidOperationException("no settings")));

        Assert.Equal(_default, thresholds.For(typeof(DeadlinedJob)));
        Assert.Equal(_default, thresholds.For(typeof(DeadlinedJob)));
    }

    [Fact]
    public void For_DeclaredThresholdOnALongRunningJob_WinsOverTheExemption()
    {
        var thresholds = Build(new StubThreshold(typeof(ExemptJob), _ => TimeSpan.FromMinutes(15)));

        // The attribute says only that the global threshold is wrong; the
        // threshold says what the right one is, so the job stays watched.
        Assert.Equal(TimeSpan.FromMinutes(15), thresholds.For(typeof(ExemptJob)));
    }

    [Fact]
    public void For_TwoThresholdsForOneJob_KeepsTheFirstOne()
    {
        var first = new StubThreshold(typeof(DeadlinedJob), _ => TimeSpan.FromMinutes(15));
        var second = new StubThreshold(typeof(DeadlinedJob), _ => TimeSpan.FromMinutes(30));
        var thresholds = Build(first, second);

        Assert.Equal(TimeSpan.FromMinutes(15), thresholds.For(typeof(DeadlinedJob)));
        Assert.Equal(0, second.Calls);
    }

    [Fact]
    public void For_NoThresholdsAtAll_IsTheGlobalThresholdThroughout()
    {
        var thresholds = new JobWatchdogThresholds(_default, [typeof(PlainJob)], new List<IJobWatchdogThreshold>(), NullLogger.Instance);

        Assert.Equal(_default, thresholds.For(typeof(PlainJob)));
    }
}
