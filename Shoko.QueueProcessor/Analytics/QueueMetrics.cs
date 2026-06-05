using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Shoko.QueueProcessor.Analytics;

/// <summary>
/// Singleton in-memory rolling statistics tracker.
/// All public methods are thread-safe.
/// </summary>
public class QueueMetrics
{
    // Sliding window of (completedAt, jobTypeName) for jobs/sec calculation
    private readonly ConcurrentQueue<(DateTime At, string JobType)> _completionWindow = new();

    // Per-type rolling average of execution times (fixed-size circular buffer)
    private readonly ConcurrentDictionary<string, RollingAverage> _execTimes = new();
    private readonly ConcurrentDictionary<string, long> _completedCounts = new();
    private readonly ConcurrentDictionary<string, long> _failureCounts = new();
    private readonly ConcurrentDictionary<string, string> _typeToPool = new();

    private readonly int _windowSeconds;
    private readonly int _rollingAvgSamples;

    public QueueMetrics(int windowSeconds = 60, int rollingAvgSamples = 100)
    {
        _windowSeconds = windowSeconds;
        _rollingAvgSamples = rollingAvgSamples;
    }

    /// <summary>Called by workers on successful job completion.</summary>
    public void RecordCompletion(string jobTypeName, string poolName, TimeSpan elapsed)
    {
        var now = DateTime.UtcNow;
        _completionWindow.Enqueue((now, jobTypeName));
        PruneWindow(now);

        _typeToPool.TryAdd(jobTypeName, poolName);
        _execTimes.GetOrAdd(jobTypeName, _ => new RollingAverage(_rollingAvgSamples))
                  .Add(elapsed.TotalMilliseconds);
        _completedCounts.AddOrUpdate(jobTypeName, 1L, (_, v) => v + 1L);
    }

    /// <summary>Called by workers on job failure (before retry decision).</summary>
    public void RecordFailure(string jobTypeName, string poolName)
    {
        _typeToPool.TryAdd(jobTypeName, poolName);
        _failureCounts.AddOrUpdate(jobTypeName, 1L, (_, v) => v + 1L);
    }

    /// <summary>Called when a job is enqueued (for type-to-pool tracking).</summary>
    public void RecordEnqueue(string jobTypeName, string poolName)
    {
        _typeToPool.TryAdd(jobTypeName, poolName);
    }

    /// <summary>
    /// Builds a snapshot. The caller supplies pool status, current queue counts,
    /// and an optional type-name → friendly-name map
    /// (from <see cref="Orchestration.QueueOrchestrator"/>) to avoid circular dependencies.
    /// </summary>
    public QueueMetricsSnapshot GetSnapshot(
        IReadOnlyDictionary<string, PoolStatus> poolStatus,
        IReadOnlyDictionary<string, (int Waiting, int Executing)> typeCounts,
        IReadOnlyDictionary<string, string> friendlyNames,
        int totalBlocked,
        int totalRetrying)
    {
        var now = DateTime.UtcNow;
        PruneWindow(now);

        var windowList = _completionWindow.ToArray();
        var jps = windowList.Length / (double)Math.Max(1, _windowSeconds);

        // Peak: highest count in any 1-second bucket within the window
        var peak = windowList.Length == 0 ? 0.0 :
            windowList.GroupBy(e => (int)(now - e.At).TotalSeconds)
                      .Max(g => g.Count()) / 1.0;

        var allTypes = _typeToPool.Keys
            .Union(typeCounts.Keys)
            .Distinct();

        var byType = new Dictionary<string, TypeMetrics>();
        foreach (var typeName in allTypes)
        {
            typeCounts.TryGetValue(typeName, out var counts);
            _typeToPool.TryGetValue(typeName, out var pool);
            _execTimes.TryGetValue(typeName, out var avg);
            _completedCounts.TryGetValue(typeName, out var completed);
            _failureCounts.TryGetValue(typeName, out var failed);
            friendlyNames.TryGetValue(typeName, out var friendlyName);

            byType[typeName] = new TypeMetrics
            {
                TypeName = typeName,
                FriendlyName = friendlyName ?? string.Empty,
                PoolName = pool ?? string.Empty,
                Waiting = counts.Waiting,
                Executing = counts.Executing,
                AvgExecutionMs = avg?.Average ?? 0,
                TotalCompleted = completed,
                TotalFailed = failed
            };
        }

        // PoolStatus.WaitingCount already excludes blocked and scheduled (disjoint buckets).
        var totalScheduled = poolStatus.Values.Sum(p => p.ScheduledCount);
        var totalWaiting = poolStatus.Values.Sum(p => p.WaitingCount);
        var totalExecuting = poolStatus.Values.Sum(p => p.ActiveWorkers);

        return new QueueMetricsSnapshot
        {
            JobsPerSecond = jps,
            JobsPerSecondPeak = peak,
            ByType = byType,
            ByPool = poolStatus,
            TotalWaiting = totalWaiting,
            TotalScheduled = totalScheduled,
            TotalExecuting = totalExecuting,
            TotalRetrying = totalRetrying,
            TotalBlocked = totalBlocked,
            SnapshotAt = now
        };
    }

    private void PruneWindow(DateTime now)
    {
        var cutoff = now.AddSeconds(-_windowSeconds);
        while (_completionWindow.TryPeek(out var oldest) && oldest.At < cutoff)
            _completionWindow.TryDequeue(out _);
    }

    /// <summary>Fixed-size circular buffer for computing a rolling average.</summary>
    private sealed class RollingAverage
    {
        private readonly double[] _buffer;
        private int _head;
        private int _count;
        private double _sum;
        private readonly object _lock = new();

        public RollingAverage(int capacity) => _buffer = new double[capacity];

        public void Add(double value)
        {
            lock (_lock)
            {
                if (_count == _buffer.Length)
                    _sum -= _buffer[_head];
                else
                    _count++;
                _buffer[_head] = value;
                _sum += value;
                _head = (_head + 1) % _buffer.Length;
            }
        }

        public double Average
        {
            get { lock (_lock) return _count == 0 ? 0 : _sum / _count; }
        }
    }
}
