using System;
using System.Collections.Generic;
using System.Threading;
using Shoko.QueueProcessor.Analytics;
using Xunit;

namespace Shoko.QueueProcessor.Tests;

/// <summary>Tests for <see cref="QueueMetrics"/> rolling stats and snapshot generation.</summary>
public class QueueMetricsTests
{
    // ── Jobs/sec sliding window ───────────────────────────────────────────────

    [Fact]
    public void JobsPerSecond_NoCompletions_IsZero()
    {
        var metrics = new QueueMetrics(windowSeconds: 60, rollingAvgSamples: 10);
        var snap = metrics.GetSnapshot(new Dictionary<string, PoolStatus>(), new Dictionary<string, (int, int)>(), new Dictionary<string, string>(), 0, 0);

        Assert.Equal(0, snap.JobsPerSecond);
    }

    [Fact]
    public void JobsPerSecond_AfterCompletions_IsPositive()
    {
        var metrics = new QueueMetrics(windowSeconds: 60, rollingAvgSamples: 10);

        for (var i = 0; i < 10; i++)
            metrics.RecordCompletion("TestJob", "Default", TimeSpan.FromMilliseconds(50));

        var snap = metrics.GetSnapshot(new Dictionary<string, PoolStatus>(), new Dictionary<string, (int, int)>(), new Dictionary<string, string>(), 0, 0);

        Assert.True(snap.JobsPerSecond > 0);
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

    // ── ByPool in snapshot ────────────────────────────────────────────────────

    [Fact]
    public void GetSnapshot_ByPool_ReflectsProvidedPoolStatus()
    {
        var metrics = new QueueMetrics();
        var poolStatus = new Dictionary<string, PoolStatus>
        {
            ["TestPool"] = new PoolStatus
            {
                Name = "TestPool",
                MaxWorkers = 2,
                ActiveWorkers = 1,
                IdleWorkers = 1,
                WaitingCount = 5,
                IsBlocked = false,
                HandledTypeNames = ["JobA"]
            }
        };

        var snap = metrics.GetSnapshot(poolStatus, new Dictionary<string, (int, int)>(), new Dictionary<string, string>(), 0, 0);

        Assert.True(snap.ByPool.ContainsKey("TestPool"));
        Assert.Equal(1, snap.ByPool["TestPool"].ActiveWorkers);
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

    [Fact]
    public void GetSnapshot_SnapshotAt_IsRecent()
    {
        var metrics = new QueueMetrics();
        var before = DateTime.UtcNow;
        var snap = metrics.GetSnapshot(new Dictionary<string, PoolStatus>(), new Dictionary<string, (int, int)>(), new Dictionary<string, string>(), 0, 0);
        var after = DateTime.UtcNow;

        Assert.True(snap.SnapshotAt >= before && snap.SnapshotAt <= after);
    }

    // ── Thread-safety smoke test ──────────────────────────────────────────────

    [Fact]
    public void ConcurrentWrites_DoNotThrow()
    {
        var metrics = new QueueMetrics(windowSeconds: 60, rollingAvgSamples: 50);
        var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

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
