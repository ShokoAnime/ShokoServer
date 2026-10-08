using System;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shoko.Abstractions.Connectivity.Suspensions;

namespace Shoko.Plugin.Tmdb.Api;

/// <summary>
///   Paces the requests sent to TMDB and holds them while TMDB will not take
///   work, reporting why through the plugin's suspension provider.
///   Thread-safe.
/// </summary>
/// <remarks>
///   <para>
///     A sliding window smooths the requests out. A 429 holds every request
///     for as long as TMDB asks, and three server errors within the error
///     window hold them for an escalating while, from a minute to an hour.
///     The two waits are kept apart, and a request waits for the later.
///   </para>
///   <para>
///     Each wait is reported as a <see cref="SuspensionKind.RateLimited"/>
///     or <see cref="SuspensionKind.ServerErrors"/> suspension with its end,
///     so the core holds the plugin's jobs back until it runs out.
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

    private readonly ISuspensionReporter<TmdbSuspensionProvider>? _reporter;

    private DateTimeOffset? _rateLimitedUntil;

    private DateTimeOffset? _serverErrorsUntil;

    private bool _disposed;

    #endregion

    #region Constructors

    /// <summary>
    ///   Creates the rate limiter.
    /// </summary>
    /// <param name="settings">How many requests a window takes, and how long a window is.</param>
    /// <param name="timeProvider">The clock the waits are measured by; the system's when left out.</param>
    /// <param name="logger">Where waits are logged; nowhere when left out.</param>
    /// <param name="errorWindow">How close together the server errors that trip the wait must be; ten seconds when left out.</param>
    /// <param name="reporter">Where the waits are reported as suspensions; nowhere when left out.</param>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> is <c>null</c>.</exception>
    public TmdbRateLimiter(
        TmdbRateLimitConfiguration settings,
        TimeProvider? timeProvider = null,
        ILogger<TmdbRateLimiter>? logger = null,
        TimeSpan? errorWindow = null,
        ISuspensionReporter<TmdbSuspensionProvider>? reporter = null
    )
    {
        ArgumentNullException.ThrowIfNull(settings);

        _reporter = reporter;
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
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> is <c>null</c>.</exception>
    public void Reconfigure(TmdbRateLimitConfiguration settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var old = _limiter;
        _limiter = CreateLimiter(settings);
        _ = Task.Delay(TimeSpan.FromSeconds(15), CancellationToken.None)
            .ContinueWith(_ => old.Dispose(), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
    }

    /// <summary>
    ///   Waits out any hold and a slot in the window, then runs a request.
    /// </summary>
    /// <typeparam name="T">What the request answers.</typeparam>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>What the request answered.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The wait was cancelled, or the limiter disposed of.</exception>
    public async Task<T> EnsureRateAsync<T>(Func<Task<T>> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCts.Token);
        while (true)
        {
            await WaitForPauseAsync(linked.Token).ConfigureAwait(false);
            using var lease = await _limiter.AcquireAsync(1, linked.Token).ConfigureAwait(false);

            // A hold may have begun while the slot was waited for.
            if (RemainingPause() is not null)
                continue;

            return await request().ConfigureAwait(false);
        }
    }

    private async Task WaitForPauseAsync(CancellationToken cancellationToken)
    {
        // A hold may be pushed back while it is waited out, so it is read again.
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

    #region Suspending

    /// <summary>
    ///   When the rate limit TMDB asked for runs out, while one is on.
    /// </summary>
    public DateTimeOffset? RateLimitedUntil
    {
        get
        {
            lock (_lock)
                return Active(_rateLimitedUntil);
        }
    }

    /// <summary>
    ///   When the pause for server errors runs out, while one is on.
    /// </summary>
    public DateTimeOffset? ServerErrorsUntil
    {
        get
        {
            lock (_lock)
                return Active(_serverErrorsUntil);
        }
    }

    /// <summary>
    ///   When every request may go again: the later of the two waits, while
    ///   either is on.
    /// </summary>
    public DateTimeOffset? ResumesAt
    {
        get
        {
            lock (_lock)
                return Latest();
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
    ///   TMDB answered 429: holds every request for as long as it asks, or
    ///   for longer when a longer rate limit is on, and reports it.
    /// </summary>
    /// <param name="retryAfter">How long TMDB asked to wait; a second when left out.</param>
    public void NotifyRateLimitExceeded(TimeSpan? retryAfter)
    {
        var delay = retryAfter is { Ticks: > 0 } wait ? wait : TimeSpan.FromSeconds(1);
        DateTimeOffset until;
        lock (_lock)
        {
            if (_disposed)
                return;

            until = _timeProvider.GetUtcNow() + delay;
            if (_rateLimitedUntil is { } current && current >= until)
                return;

            _rateLimitedUntil = until;
        }

        _logger.LogInformation("TMDB is rate limiting requests. All TMDB jobs paused for {Duration} seconds. They will resume automatically.", (int)Math.Ceiling(delay.TotalSeconds));
        Report(SuspensionKind.RateLimited, until);
    }

    /// <summary>
    ///   TMDB answered with a server error. The third within the error window
    ///   holds every request, for longer each time until a request succeeds
    ///   after the wait, and reports it.
    /// </summary>
    public void NotifyServerError()
    {
        TimeSpan duration;
        DateTimeOffset until;
        lock (_lock)
        {
            if (_disposed)
                return;

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
            until = now + duration;
            if (_serverErrorsUntil is { } current && current >= until)
            {
                _logger.LogDebug("TMDB is temporarily unavailable, but a longer pause is already on.");
                return;
            }

            _serverErrorsUntil = until;
        }

        _logger.LogInformation("TMDB is temporarily unavailable. All TMDB jobs paused for {Duration} minutes. They will resume automatically.", (int)duration.TotalMinutes);
        Report(SuspensionKind.ServerErrors, until);
    }

    /// <summary>
    ///   A request succeeded: resets the escalation once the server error wait
    ///   is over.
    /// </summary>
    public void NotifySuccess()
    {
        lock (_lock)
        {
            if (_serverErrorLevel is 0 || Active(_serverErrorsUntil) is not null)
                return;

            _serverErrorLevel = 0;
            _serverErrorsUntil = null;
            Array.Clear(_errorTimes);
            _errorSlot = 0;
        }
    }

    /// <summary>
    ///   How long a trip of the given level holds the requests.
    /// </summary>
    /// <param name="level">The level, from one.</param>
    /// <returns>The wait.</returns>
    internal static TimeSpan GetServerErrorPause(int level) => level switch
    {
        1 => TimeSpan.FromMinutes(1),
        2 => TimeSpan.FromMinutes(3),
        3 => TimeSpan.FromMinutes(5),
        4 => TimeSpan.FromMinutes(15),
        _ => TimeSpan.FromMinutes(60),
    };

    // The core clears the suspension once its end passes, so nothing resumes it here.
    private void Report(SuspensionKind kind, DateTimeOffset until)
    {
        if (_reporter is null)
            return;

        try
        {
            _reporter.Suspend(kind, resumesAt: until.UtcDateTime);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Could not report the TMDB {Kind} suspension.", kind);
        }
    }

    // Called under the lock.
    private DateTimeOffset? Active(DateTimeOffset? until)
        => until is { } at && at > _timeProvider.GetUtcNow() ? at : null;

    // Called under the lock.
    private DateTimeOffset? Latest()
        => (Active(_rateLimitedUntil), Active(_serverErrorsUntil)) switch
        {
            ({ } rate, { } errors) => rate > errors ? rate : errors,
            ({ } rate, null) => rate,
            (null, { } errors) => errors,
            _ => null,
        };

    private TimeSpan? RemainingPause()
    {
        lock (_lock)
            return Latest() is { } until ? until - _timeProvider.GetUtcNow() : null;
    }

    #endregion

    #region Disposal

    /// <summary>
    ///   Gives up on the requests still waiting.
    /// </summary>
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
                return;

            _disposed = true;
        }

        _disposeCts.Cancel();
        _disposeCts.Dispose();
        _limiter.Dispose();
    }

    #endregion
}
