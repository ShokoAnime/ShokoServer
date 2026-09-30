using System;
using System.Threading;

namespace Shoko.Server.Databases;

/// <summary>
/// Reports how far a long startup step has come, at most once every
/// <see cref="Interval"/> or once per tenth of the work, whichever comes
/// first, so a large library does not flood the log. Safe to advance from
/// several threads.
/// </summary>
/// <param name="report">Called with each progress message.</param>
/// <param name="label">What the step does, without trailing dots.</param>
/// <param name="total">The number of items the step works through.</param>
/// <param name="timeProvider">The clock, or <c>null</c> for the system clock.</param>
internal sealed class StartupProgress(Action<string> report, string label, int total, TimeProvider? timeProvider = null)
{
    #region Fields

    /// <summary>
    /// The longest time between two reports while the step is running.
    /// </summary>
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    private readonly Lock _lock = new();

    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    private long _lastReportedAt = (timeProvider ?? TimeProvider.System).GetTimestamp();

    private int _done;

    private int _nextTenth = 1;

    #endregion

    #region Methods

    /// <summary>
    /// Counts items as done, and reports when a tenth of the work is passed
    /// or <see cref="Interval"/> has gone by since the last report. The end
    /// is never reported, since the step reports its own summary.
    /// </summary>
    /// <param name="count">The number of items just done.</param>
    public void Advance(int count = 1)
    {
        lock (_lock)
        {
            _done += count;
            if (_done >= total)
                return;

            var tenth = (int)((long)_done * 10 / total);
            if (tenth < _nextTenth && _timeProvider.GetElapsedTime(_lastReportedAt) < Interval)
                return;

            _nextTenth = tenth + 1;
            _lastReportedAt = _timeProvider.GetTimestamp();
            report($"{label}... {_done}/{total}");
        }
    }

    #endregion
}
