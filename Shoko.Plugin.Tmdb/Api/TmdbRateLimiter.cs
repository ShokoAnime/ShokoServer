using System;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Shoko.Plugin.Tmdb.Api;

/// <summary>
///   Paces the requests sent to TMDb and holds the pause the plugin reports
///   while TMDb will not take work. Thread-safe.
/// </summary>
/// <remarks>
///   <para>
///     A sliding window smooths the requests out. A 429 pauses every request
///     for as long as TMDb asks, and three server errors within the error
///     window pause them for an escalating while, from a minute to an hour.
///   </para>
///   <para>
///     The provider hands the pause to the core, which holds the plugin's
///     jobs back until it runs out. A timer lifts it on time even when no
///     request runs.
///   </para>
/// </remarks>
public sealed class TmdbRateLimiter : IDisposable
{
    #region Fields

    /// <summary>
    ///   How many server errors in the error window trip the pause.
    /// </summary>
    private const int ErrorsToTrip = 3;

    private readonly ILogger<TmdbRateLimiter> _logger;

    private readonly TimeProvider _timeProvider;

    private readonly TimeSpan _errorWindow;

    private readonly Lock _lock = new();

    private readonly CancellationTokenSource _disposeCts = new();

    private readonly DateTimeOffset?[] _errorTimes = new DateTimeOffset?[ErrorsToTrip];

    private volatile SlidingWindowRateLimiter _limiter;

    private int _errorSlot;

    private int _serverErrorLevel;

    private DateTimeOffset? _pausedUntil;

    private TmdbPauseReason _pauseReason;

    private ITimer? _pauseTimer;

    private bool _disposed;

    #endregion

    #region Constructors

    /// <summary>
    ///   Creates the rate limiter.
    /// </summary>
    /// <param name="settings">How many requests a window takes, and how long a window is.</param>
    /// <param name="timeProvider">The clock the pauses are measured by; the system's when left out.</param>
    /// <param name="logger">Where pauses are logged; nowhere when left out.</param>
    /// <param name="errorWindow">How close together the server errors that trip the pause must be; ten seconds when left out.</param>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> is <see langword="null"/>.</exception>
    public TmdbRateLimiter(TmdbRateLimitConfiguration settings, TimeProvider? timeProvider = null, ILogger<TmdbRateLimiter>? logger = null, TimeSpan? errorWindow = null)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _timeProvider = timeProvider ?? TimeProvider.System;
        _logger = logger ?? NullLogger<TmdbRateLimiter>.Instance;
        _errorWindow = errorWindow ?? TimeSpan.FromSeconds(10);
        _limiter = CreateLimiter(settings);
    }

    #endregion

    #region Pacing

    /// <summary>
    ///   Takes new window settings. Requests waiting on the old window finish
    ///   on it, which is disposed of a while later.
    /// </summary>
    /// <param name="settings">The new settings.</param>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> is <see langword="null"/>.</exception>
    public void Reconfigure(TmdbRateLimitConfiguration settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var old = _limiter;
        _limiter = CreateLimiter(settings);
        _ = Task.Delay(TimeSpan.FromSeconds(15), CancellationToken.None)
            .ContinueWith(_ => old.Dispose(), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
    }

    /// <summary>
    ///   Waits out any pause and a slot in the window, then runs a request.
    /// </summary>
    /// <typeparam name="T">What the request answers.</typeparam>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>What the request answered.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The wait was cancelled, or the limiter disposed of.</exception>
    public async Task<T> EnsureRateAsync<T>(Func<Task<T>> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCts.Token);
        while (true)
        {
            await WaitForPauseAsync(linked.Token).ConfigureAwait(false);
            using var lease = await _limiter.AcquireAsync(1, linked.Token).ConfigureAwait(false);

            // A pause may have begun while the slot was waited for.
            if (RemainingPause() is not null)
                continue;

            return await request().ConfigureAwait(false);
        }
    }

    private async Task WaitForPauseAsync(CancellationToken cancellationToken)
    {
        // A pause may be pushed back while it is waited out, so it is read again.
        while (RemainingPause() is { } remaining)
            await Task.Delay(remaining + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 50)), _timeProvider, cancellationToken).ConfigureAwait(false);
    }

    private static SlidingWindowRateLimiter CreateLimiter(TmdbRateLimitConfiguration settings)
        => new(new()
        {
            PermitLimit = Math.Max(1, settings.MaxRequestsPerWindow),
            Window = TimeSpan.FromMilliseconds(Math.Max(1, settings.WindowDurationMs)),
            SegmentsPerWindow = 10,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = int.MaxValue,
            AutoReplenishment = true,
        });

    #endregion

    #region Pausing

    /// <summary>
    ///   Raised when a pause begins, ends or changes its reason.
    /// </summary>
    public event EventHandler? PauseStateChanged;

    /// <summary>
    ///   Why TMDb is not given work, or <see cref="TmdbPauseReason.None"/>
    ///   while it is.
    /// </summary>
    public TmdbPauseReason PauseReason
    {
        get
        {
            lock (_lock)
                return _pauseReason;
        }
    }

    /// <summary>
    ///   When the pause runs out, while there is one.
    /// </summary>
    public DateTimeOffset? ResumesAt
    {
        get
        {
            lock (_lock)
                return _pauseReason is TmdbPauseReason.None ? null : _pausedUntil;
        }
    }

    /// <summary>
    ///   The server errors counted towards the next trip, for the tests.
    /// </summary>
    internal int ServerErrorLevel
    {
        get
        {
            lock (_lock)
                return _serverErrorLevel;
        }
    }

    /// <summary>
    ///   TMDb answered 429: pauses every request for as long as it asks, or
    ///   for longer when a longer pause is on.
    /// </summary>
    /// <param name="retryAfter">How long TMDb asked to wait; a second when left out.</param>
    public void NotifyRateLimitExceeded(TimeSpan? retryAfter)
    {
        var delay = retryAfter is { Ticks: > 0 } wait ? wait : TimeSpan.FromSeconds(1);
        if (!Pause(delay, TmdbPauseReason.RateLimited))
            return;

        _logger.LogInformation("TMDb is rate limiting requests. All TMDb jobs paused for {Duration} seconds. They will resume automatically.", (int)Math.Ceiling(delay.TotalSeconds));
        PauseStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    ///   TMDb answered with a server error. The third within the error window
    ///   pauses every request, for longer each time until a request succeeds
    ///   after the pause.
    /// </summary>
    public void NotifyServerError()
    {
        TimeSpan? duration = null;
        var changed = false;
        lock (_lock)
        {
            var now = _timeProvider.GetUtcNow();
            _errorTimes[_errorSlot] = now;
            _errorSlot = (_errorSlot + 1) % ErrorsToTrip;
            var oldest = DateTimeOffset.MaxValue;
            foreach (var time in _errorTimes)
            {
                if (time is not { } at)
                    return;
                if (at < oldest)
                    oldest = at;
            }

            if (now - oldest >= _errorWindow)
                return;

            // The errors already counted are not counted again for the next trip.
            Array.Clear(_errorTimes);
            _errorSlot = 0;
            _serverErrorLevel = Math.Min(_serverErrorLevel + 1, 5);
            duration = GetServerErrorPause(_serverErrorLevel);
            changed = PauseUnderLock(duration.Value, TmdbPauseReason.ServerErrors);
        }

        if (!changed)
        {
            _logger.LogDebug("TMDb is temporarily unavailable, but a longer pause is already on.");
            return;
        }

        _logger.LogInformation("TMDb is temporarily unavailable. All TMDb jobs paused for {Duration} minutes. They will resume automatically.", (int)duration!.Value.TotalMinutes);
        PauseStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    ///   A request succeeded: lifts a pause that has run out and resets the
    ///   escalation once the pause is over.
    /// </summary>
    public void NotifySuccess()
    {
        var lifted = false;
        lock (_lock)
        {
            if (_serverErrorLevel is 0 && _pauseReason is TmdbPauseReason.None)
                return;
            if (_pausedUntil is { } until && until > _timeProvider.GetUtcNow())
                return;

            _serverErrorLevel = 0;
            Array.Clear(_errorTimes);
            _errorSlot = 0;
            lifted = Lift();
        }

        if (!lifted)
            return;

        _logger.LogInformation("TMDb is available again. Queued TMDb jobs will now resume.");
        PauseStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    ///   How long a trip of the given level pauses the requests.
    /// </summary>
    /// <param name="level">The level, from one.</param>
    /// <returns>The pause.</returns>
    internal static TimeSpan GetServerErrorPause(int level) => level switch
    {
        1 => TimeSpan.FromMinutes(1),
        2 => TimeSpan.FromMinutes(3),
        3 => TimeSpan.FromMinutes(5),
        4 => TimeSpan.FromMinutes(15),
        _ => TimeSpan.FromMinutes(60),
    };

    private bool Pause(TimeSpan duration, TmdbPauseReason reason)
    {
        lock (_lock)
            return PauseUnderLock(duration, reason);
    }

    // The longer pause wins, whatever its reason. Called under the lock.
    private bool PauseUnderLock(TimeSpan duration, TmdbPauseReason reason)
    {
        if (_disposed)
            return false;

        var until = _timeProvider.GetUtcNow() + duration;
        if (_pausedUntil is { } current && current >= until)
            return false;

        var changed = _pauseReason != reason;
        _pausedUntil = until;
        _pauseReason = reason;
        Arm(duration);
        return changed;
    }

    private void Arm(TimeSpan dueTime)
    {
        var clamped = dueTime < TimeSpan.Zero ? TimeSpan.Zero : dueTime > TimeSpan.FromDays(1) ? TimeSpan.FromDays(1) : dueTime;
        if (_pauseTimer is { } timer)
        {
            timer.Change(clamped, Timeout.InfiniteTimeSpan);
            return;
        }

        // The timer outlives the request that armed it, so it takes nothing of its context along.
        using (ExecutionContext.SuppressFlow())
            _pauseTimer = _timeProvider.CreateTimer(_ => Expire(), null, clamped, Timeout.InfiniteTimeSpan);
    }

    private void Expire()
    {
        TmdbPauseReason reason;
        lock (_lock)
        {
            if (_disposed || _pauseReason is TmdbPauseReason.None)
                return;

            // A timer that fired early waits out the rest of the pause.
            if (RemainingPauseUnderLock() is { } remaining)
            {
                Arm(remaining);
                return;
            }

            reason = _pauseReason;
            Lift();
        }

        _logger.LogInformation(
            reason is TmdbPauseReason.RateLimited ? "TMDb rate limit pause expired. Queued TMDb jobs will now resume." : "TMDb pause expired. Queued TMDb jobs will now resume."
        );
        PauseStateChanged?.Invoke(this, EventArgs.Empty);
    }

    // Called under the lock.
    private bool Lift()
    {
        _pausedUntil = null;
        if (_pauseReason is TmdbPauseReason.None)
            return false;

        _pauseReason = TmdbPauseReason.None;
        return true;
    }

    private TimeSpan? RemainingPause()
    {
        lock (_lock)
            return RemainingPauseUnderLock();
    }

    private TimeSpan? RemainingPauseUnderLock()
        => _pausedUntil is { } until && until - _timeProvider.GetUtcNow() is { Ticks: > 0 } remaining ? remaining : null;

    #endregion

    #region Disposal

    /// <summary>
    ///   Stops the pause timer and gives up on the requests still waiting.
    /// </summary>
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
                return;

            _disposed = true;
            _pauseTimer?.Dispose();
        }

        _disposeCts.Cancel();
        _disposeCts.Dispose();
        _limiter.Dispose();
    }

    #endregion
}
