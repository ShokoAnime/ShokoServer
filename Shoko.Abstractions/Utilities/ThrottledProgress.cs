using System;
using System.Threading;

namespace Shoko.Abstractions.Utilities;

/// <summary>
///   Passes progress on to a parent at most once per interval, so a loop
///   over many items does not report each one. All values are percentages
///   from 0 to 100, as a scheduled action's progress takes them.
/// </summary>
/// <remarks>
///   The first report and every report of 100 always go through. A report
///   inside the interval is held back, and goes out with the next report
///   after the interval or with <see cref="Flush"/>; nothing goes out on a
///   timer. Values are clamped. Safe to use from several threads.
/// </remarks>
public sealed class ThrottledProgress : IProgress<decimal>
{
    #region Fields

    /// <summary>
    ///   The interval used when none is given, half a second.
    /// </summary>
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromMilliseconds(500);

    private readonly IProgress<decimal>? _parent;

    private readonly TimeProvider _timeProvider;

    private readonly Lock _lock = new();

    private long? _lastForwardedAt;

    private decimal? _pending;

    #endregion

    #region Constructors

    /// <summary>
    ///   Creates a throttle in front of <paramref name="parent"/>.
    /// </summary>
    /// <param name="parent">
    ///   Takes the values let through, or <c>null</c> to drop
    ///   them.
    /// </param>
    /// <param name="interval">
    ///   The least time between two values passed on, or
    ///   <c>null</c> for <see cref="DefaultInterval"/>.
    /// </param>
    /// <param name="timeProvider">
    ///   The clock, or <c>null</c> for the system's.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   <paramref name="interval"/> is negative.
    /// </exception>
    public ThrottledProgress(IProgress<decimal>? parent, TimeSpan? interval = null, TimeProvider? timeProvider = null)
    {
        Interval = interval ?? DefaultInterval;
        ArgumentOutOfRangeException.ThrowIfLessThan(Interval, TimeSpan.Zero, nameof(interval));
        _parent = parent;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    #endregion

    #region Properties

    /// <summary>
    ///   The least time between two values passed on.
    /// </summary>
    public TimeSpan Interval { get; }

    #endregion

    #region Methods

    /// <summary>
    ///   Passes <paramref name="value"/> on when it is the first, is 100, or
    ///   the interval is up, and holds it back otherwise.
    /// </summary>
    /// <param name="value">The progress, from 0 to 100.</param>
    public void Report(decimal value)
    {
        value = Math.Clamp(value, 0m, 100m);
        lock (_lock)
        {
            var now = _timeProvider.GetTimestamp();
            if (value < 100m && _lastForwardedAt is { } last && _timeProvider.GetElapsedTime(last, now) < Interval)
            {
                _pending = value;
                return;
            }

            _lastForwardedAt = now;
            _pending = null;
            _parent?.Report(value);
        }
    }

    /// <summary>
    ///   Passes on the value held back last, if there is one.
    /// </summary>
    public void Flush()
    {
        lock (_lock)
        {
            if (_pending is not { } value)
                return;

            _lastForwardedAt = _timeProvider.GetTimestamp();
            _pending = null;
            _parent?.Report(value);
        }
    }

    #endregion
}
