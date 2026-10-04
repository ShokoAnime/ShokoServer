using System;
using System.Threading;
using System.Threading.Tasks;

namespace Shoko.QueueProcessor.Orchestration;

/// <summary>
/// In-memory state of one running job that changes while it runs: its cancellation source, a
/// user's request to cancel it, and the progress it reports. Created by
/// <see cref="QueueOrchestrator"/> when the job is registered as executing and ended when it
/// leaves the executing set. Never persisted.
/// </summary>
internal sealed class JobExecutionState : IProgress<decimal>
{
    /// <summary>
    /// The least time between two progress events for one job, so a job reporting in a tight
    /// loop cannot flood the subscribers. The latest value is always stored, and one held back
    /// goes out when the interval is up.
    /// </summary>
    internal static readonly TimeSpan ProgressEventInterval = TimeSpan.FromMilliseconds(100);

    private readonly Action<JobExecutionState, decimal> _onProgress;

    // Boxed decimal? so it can be read and written atomically without a lock.
    private object? _progress;

    private long _lastProgressEventAt;

    // The value the last progress event carried, boxed like _progress.
    private object? _announced;

    // Sends a value held back by the throttle once the interval is up. Created on first need.
    private Timer? _trailingTimer;

    // 1 while the trailing timer is armed.
    private int _trailingArmed;

    private readonly Lock _timerLock = new();

    private volatile CancellationTokenSource? _cancellation;

    private volatile bool _cancellationRequested;

    private volatile bool _ended;

    /// <summary>
    /// Creates the state of a job that has just been registered as executing.
    /// </summary>
    /// <param name="id">The job's ID.</param>
    /// <param name="jobKey">The job's key.</param>
    /// <param name="onProgress">Called with a new progress value, at most once per <see cref="ProgressEventInterval"/>.</param>
    public JobExecutionState(Guid id, string jobKey, Action<JobExecutionState, decimal> onProgress)
    {
        Id = id;
        JobKey = jobKey;
        _onProgress = onProgress;
    }

    /// <summary>
    /// The job's ID.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// The job's key.
    /// </summary>
    public string JobKey { get; }

    /// <summary>
    /// The last progress the job reported, from 0 to 100, or <c>null</c> if it has not
    /// reported any.
    /// </summary>
    public decimal? Progress => Volatile.Read(ref _progress) as decimal?;

    /// <summary>
    /// Whether a user asked for the job to be cancelled. Never goes back to <c>false</c>.
    /// </summary>
    public bool CancellationRequested => _cancellationRequested;

    /// <summary>
    /// Stores <paramref name="value"/>, clamped to 0 to 100, as the job's progress, and raises a
    /// progress event, or, when one was raised less than <see cref="ProgressEventInterval"/> ago,
    /// raises it with the latest value once the interval is up. Ignored once the job has ended.
    /// </summary>
    /// <param name="value">The progress, as a percentage.</param>
    public void Report(decimal value)
    {
        if (_ended)
            return;

        value = Math.Clamp(value, 0m, 100m);
        var previous = Progress;
        if (previous == value)
            return;

        Volatile.Write(ref _progress, value);

        // The first value, and the last one, always go out; anything in between is throttled.
        var now = Environment.TickCount64;
        var last = Interlocked.Read(ref _lastProgressEventAt);
        var interval = (long)ProgressEventInterval.TotalMilliseconds;
        if (previous is not null && value < 100m && now - last < interval)
        {
            ArmTrailingEdge(TimeSpan.FromMilliseconds(interval - (now - last)));
            return;
        }

        // Another report went out at the same moment, maybe with an older value.
        if (Interlocked.CompareExchange(ref _lastProgressEventAt, now, last) != last)
        {
            ArmTrailingEdge(ProgressEventInterval);
            return;
        }

        Announce(value);
    }

    /// <summary>
    /// Raises a progress event with <paramref name="value"/>.
    /// </summary>
    /// <param name="value">The progress.</param>
    private void Announce(decimal value)
    {
        Volatile.Write(ref _announced, value);
        _onProgress(this, value);
    }

    /// <summary>
    /// Makes sure the latest value goes out after <paramref name="delay"/>, unless a timer for it
    /// is already armed.
    /// </summary>
    /// <param name="delay">How long until the interval is up.</param>
    private void ArmTrailingEdge(TimeSpan delay)
    {
        if (Interlocked.Exchange(ref _trailingArmed, 1) == 1)
            return;

        lock (_timerLock)
        {
            if (_ended)
                return;

            if (_trailingTimer is null)
            {
                using (DetachedFlow.Suppress())
                    _trailingTimer = new Timer(_ => FlushTrailingEdge(), null, Timeout.Infinite, Timeout.Infinite);
            }

            _trailingTimer.Change(delay, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>
    /// Raises the progress event held back by the throttle, if the latest value was not sent yet
    /// and the job has not ended.
    /// </summary>
    private void FlushTrailingEdge()
    {
        Interlocked.Exchange(ref _trailingArmed, 0);
        if (_ended || Progress is not { } value || Volatile.Read(ref _announced) as decimal? == value)
            return;

        Interlocked.Exchange(ref _lastProgressEventAt, Environment.TickCount64);
        Announce(value);
    }

    /// <summary>
    /// Attaches the job's cancellation source, and cancels it right away when the cancellation was
    /// requested before the worker got this far. Called by the worker under the orchestrator's lock.
    /// </summary>
    /// <param name="cancellation">The job's cancellation source, owned and disposed by the worker.</param>
    /// <returns><c>true</c> when the cancellation was already requested.</returns>
    internal bool Attach(CancellationTokenSource cancellation)
    {
        _cancellation = cancellation;
        return _cancellationRequested;
    }

    /// <summary>
    /// Marks the cancellation as requested. Called under the orchestrator's lock.
    /// </summary>
    /// <returns><c>true</c> when it was not requested before.</returns>
    internal bool MarkCancellationRequested()
    {
        if (_cancellationRequested)
            return false;

        _cancellationRequested = true;
        return true;
    }

    /// <summary>
    /// Cancels the job's cancellation source, if the worker attached one and has not disposed it.
    /// Called outside the orchestrator's lock, as it runs the token's callbacks.
    /// </summary>
    /// <returns>A task that completes once the token's callbacks have run.</returns>
    /// <exception cref="AggregateException">A callback registered on the token threw.</exception>
    internal async Task CancelAsync()
    {
        if (_cancellation is not { } cancellation)
            return;

        try
        {
            await cancellation.CancelAsync().ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            // The job ended and the worker disposed the source in the meantime.
        }
    }

    /// <summary>
    /// Marks the job as ended, after which its progress reports are dropped.
    /// </summary>
    internal void End()
    {
        _ended = true;
        _cancellation = null;
        lock (_timerLock)
        {
            _trailingTimer?.Dispose();
            _trailingTimer = null;
        }
    }
}
