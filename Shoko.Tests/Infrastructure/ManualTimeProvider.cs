using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Shoko.Tests.Infrastructure;

/// <summary>
///   A clock the tests move by hand, whose timers fire once it passes their
///   due time.
/// </summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private readonly Lock _lock = new();

    private readonly List<ManualTimer> _timers = [];

    private DateTimeOffset _now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow()
    {
        lock (_lock)
            return _now;
    }

    /// <inheritdoc/>
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        timer.Change(dueTime, period);
        lock (_lock)
            _timers.Add(timer);
        return timer;
    }

    /// <summary>
    ///   Moves the clock on, firing the timers it passes.
    /// </summary>
    /// <param name="time">How far.</param>
    public void Advance(TimeSpan time)
    {
        List<ManualTimer> due;
        lock (_lock)
        {
            _now += time;
            due = [.. _timers.Where(timer => timer.DueAt is { } at && at <= _now)];
            foreach (var timer in due)
                timer.DueAt = null;
        }

        foreach (var timer in due)
            timer.Fire();
    }

    private sealed class ManualTimer(ManualTimeProvider clock, TimerCallback callback, object? state) : ITimer
    {
        public DateTimeOffset? DueAt { get; set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            DueAt = dueTime == Timeout.InfiniteTimeSpan ? null : clock.GetUtcNow() + dueTime;
            return true;
        }

        public void Fire() => callback(state);

        public void Dispose() => DueAt = null;

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
