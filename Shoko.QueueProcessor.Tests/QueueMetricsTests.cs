using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Shoko.QueueProcessor.Analytics;
using Xunit;

namespace Shoko.QueueProcessor.Tests;

/// <summary>Tests for <see cref="QueueMetrics"/> rolling stats and snapshot generation.</summary>
public class QueueMetricsTests
{
    // ── Jobs/sec sliding window ───────────────────────────────────────────────

    [Theory]
    [InlineData(0, false)]
    [InlineData(10, true)]
    public void JobsPerSecond_IsPositiveOnlyAfterCompletions(int completions, bool expectedPositive)
    {
        var metrics = new QueueMetrics(windowSeconds: 60, rollingAvgSamples: 10);

        for (var i = 0; i < completions; i++)
            metrics.RecordCompletion("TestJob", "Default", TimeSpan.FromMilliseconds(50));

        var snap = metrics.GetSnapshot(new Dictionary<string, PoolStatus>(), new Dictionary<string, (int, int)>(), new Dictionary<string, string>(), 0, 0);

        if (expectedPositive)
            Assert.True(snap.JobsPerSecond > 0);
        else
            Assert.Equal(0, snap.JobsPerSecond);
    }

    // ── Per-type rolling average ──────────────────────────────────────────────

    [Fact]
    public void AvgExecutionMs_Accurate()
    {
        var metrics = new QueueMetrics(windowSeconds: 60, rollingAvgSamples: 100);
        metrics.RecordCompletion("JobA", "Pool1", TimeSpan.FromMilliseconds(100));
        metrics.RecordCompletion("JobA", "Pool1", TimeSpan.FromMilliseconds(200));
        metrics.RecordCompletion("JobA", "Pool1", TimeSpan.FromMilliseconds(300));

        var snap = metrics.GetSnapshot(new Dictionary<string, PoolStatus>(), new Dictionary<string, (int, int)>(), new Dictionary<string, string>(), 0, 0);

        Assert.True(snap.ByType.ContainsKey("JobA"));
        Assert.Equal(200.0, snap.ByType["JobA"].AvgExecutionMs, precision: 0);
    }

    [Fact]
    public void RollingAvg_ExceedsCapacity_OldestDropped()
    {
        var metrics = new QueueMetrics(windowSeconds: 60, rollingAvgSamples: 3); // tiny window
        metrics.RecordCompletion("JobA", "Pool1", TimeSpan.FromMilliseconds(1000));
        metrics.RecordCompletion("JobA", "Pool1", TimeSpan.FromMilliseconds(1000));
        metrics.RecordCompletion("JobA", "Pool1", TimeSpan.FromMilliseconds(1000));
        // These push the 1000ms values out
        metrics.RecordCompletion("JobA", "Pool1", TimeSpan.FromMilliseconds(10));
        metrics.RecordCompletion("JobA", "Pool1", TimeSpan.FromMilliseconds(10));
        metrics.RecordCompletion("JobA", "Pool1", TimeSpan.FromMilliseconds(10));

        var snap = metrics.GetSnapshot(new Dictionary<string, PoolStatus>(), new Dictionary<string, (int, int)>(), new Dictionary<string, string>(), 0, 0);

        // After eviction, avg should be 10ms
        Assert.Equal(10.0, snap.ByType["JobA"].AvgExecutionMs, precision: 0);
    }

    // ── TotalCompleted / TotalFailed monotonic counters ───────────────────────

    [Fact]
    public void TotalCompleted_IncreasesMonotonically()
    {
        var metrics = new QueueMetrics();
        metrics.RecordCompletion("JobA", "Pool1", TimeSpan.FromMilliseconds(10));
        metrics.RecordCompletion("JobA", "Pool1", TimeSpan.FromMilliseconds(10));
        metrics.RecordCompletion("JobA", "Pool1", TimeSpan.FromMilliseconds(10));

        var snap = metrics.GetSnapshot(new Dictionary<string, PoolStatus>(), new Dictionary<string, (int, int)>(), new Dictionary<string, string>(), 0, 0);

        Assert.Equal(3L, snap.ByType["JobA"].TotalCompleted);
    }

    [Fact]
    public void TotalFailed_IncreasesOnRecordFailure()
    {
        var metrics = new QueueMetrics();
        metrics.RecordFailure("JobA", "Pool1");
        metrics.RecordFailure("JobA", "Pool1");

        var snap = metrics.GetSnapshot(new Dictionary<string, PoolStatus>(), new Dictionary<string, (int, int)>(), new Dictionary<string, string>(), 0, 0);

        Assert.Equal(2L, snap.ByType["JobA"].TotalFailed);
    }

    // ── TotalWaiting / TotalExecuting ─────────────────────────────────────────

    [Fact]
    public void GetSnapshot_TotalWaiting_SumsPoolWaitingCounts()
    {
        var metrics = new QueueMetrics();
        var poolStatus = new Dictionary<string, PoolStatus>
        {
            ["Pool1"] = new PoolStatus { WaitingCount = 10 },
            ["Pool2"] = new PoolStatus { WaitingCount = 15 }
        };

        var snap = metrics.GetSnapshot(poolStatus, new Dictionary<string, (int, int)>(), new Dictionary<string, string>(), 0, 0);

        Assert.Equal(25, snap.TotalWaiting);
    }

    // ── Thread-safety smoke test ──────────────────────────────────────────────

    [Fact]
    public void ConcurrentWrites_DoNotThrow()
    {
        var metrics = new QueueMetrics(windowSeconds: 60, rollingAvgSamples: 50);
        var exceptions = new ConcurrentBag<Exception>();

        var threads = new Thread[8];
        for (var i = 0; i < threads.Length; i++)
        {
            threads[i] = new Thread(() =>
            {
                try
                {
                    for (var j = 0; j < 500; j++)
                    {
                        metrics.RecordCompletion("Job", "Pool", TimeSpan.FromMilliseconds(j));
                        metrics.RecordFailure("Job", "Pool");
                        metrics.RecordEnqueue("Job", "Pool");
                    }
                }
                catch (Exception ex) { exceptions.Add(ex); }
            });
        }

        foreach (var t in threads) t.Start();
        foreach (var t in threads) t.Join();

        Assert.Empty(exceptions);
    }
}
