using System;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Events;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.Server.Providers.TMDB;

// The 5xx breaker below (Notify5xxError/NotifySuccess) is a hand-rolled fixed-count-in-window
// state machine, not Polly's AdvancedCircuitBreakerPolicy, even though TmdbMetadataService already
// depends on Polly elsewhere. Polly's breaker trips on failure *rate* over a sliding window, a different
// semantic than "3 errors within 10s" — swapping it in would change trip behavior and has no test coverage
// against the new shape. Revisit if the ring-buffer approach needs further tuning.
/// <summary>
/// Rate limiter for the TMDB API (~40 req/sec enforced by TMDB).
/// Uses a sliding window to smooth request distribution, and pauses TMDB jobs on a
/// server-enforced 429 backoff via <see cref="NotifyRateLimitExceeded"/>.
/// </summary>
public sealed class TmdbRateLimiter : IDisposable
{
    private readonly ILogger<TmdbRateLimiter> _logger;

    private readonly ConfigurationProvider<ServerSettings> _settingsProvider;

    private volatile SlidingWindowRateLimiter _limiter;

    private volatile int _maxRequestsPerWindow;

    private long _backoffUntilTicks;

    // Ring buffer of the last 3 distress-error timestamps (UTC ticks). 0 = slot not yet written.
    // Indexed by (_errorSlot % 3). The breaker trips when all 3 slots are within _errorWindowTicks of now.
    // Both fields are only accessed under _breakerLock.
    private readonly long[] _errorTimestamps = new long[3];

    private int _errorSlot;

    private readonly long _errorWindowTicks;

    // Guards _5xxPauseLevel, _pauseReason and all writes to _backoffUntilTicks so that they
    // are always updated atomically. Reads of _backoffUntilTicks from WaitForBackoffAsync
    // and EnsureRateAsync happen outside this lock via Interlocked.Read — that is safe because
    // lock exit provides a release fence and Interlocked.Read provides an acquire fence.
    private readonly Lock _breakerLock = new();

    private volatile int _5xxPauseLevel;

    private volatile TmdbPauseReason _pauseReason;

    // Lifts the pause once its deadline passes. Re-armed whenever the deadline moves.
    private readonly Timer _pauseExpiryTimer;

    private bool _disposed;

    private readonly CancellationTokenSource _disposeCts = new();

    /// <summary>
    /// Number of requests recorded in the current window.
    /// </summary>
    public int CallsInWindow =>
        _maxRequestsPerWindow - (int)(_limiter.GetStatistics()?.CurrentAvailablePermits ?? _maxRequestsPerWindow);

    /// <summary>
    /// Remaining request capacity in the current window.
    /// </summary>
    public int RemainingInWindow =>
        (int)(_limiter.GetStatistics()?.CurrentAvailablePermits ?? _maxRequestsPerWindow);

    public TmdbRateLimiter(ILogger<TmdbRateLimiter> logger, ConfigurationProvider<ServerSettings> settingsProvider)
        : this(logger, settingsProvider, TimeSpan.FromSeconds(10)) { }

    internal TmdbRateLimiter(ILogger<TmdbRateLimiter> logger, ConfigurationProvider<ServerSettings> settingsProvider, TimeSpan errorWindow)
    {
        _logger = logger;
        _settingsProvider = settingsProvider;
        _errorWindowTicks = errorWindow.Ticks;
        var settings = settingsProvider.Load().TMDB.RateLimit;
        _maxRequestsPerWindow = settings.MaxRequestsPerWindow;
        _limiter = CreateLimiter(settings.MaxRequestsPerWindow, settings.WindowDurationMs);
        using (DetachedFlow.Suppress())
            _pauseExpiryTimer = new(_ => OnPauseExpired(), null, Timeout.Infinite, Timeout.Infinite);
        _settingsProvider.Saved += OnSettingsSaved;
    }

    public void Dispose()
    {
        _settingsProvider.Saved -= OnSettingsSaved;
        lock (_breakerLock)
        {
            _disposed = true;
            _pauseExpiryTimer.Dispose();
        }
        _disposeCts.Cancel();
        _disposeCts.Dispose();
        _limiter.Dispose();
        GC.SuppressFinalize(this);
    }

    private void OnSettingsSaved(object? sender, ConfigurationSavedEventArgs<ServerSettings> eventArgs)
    {
        var settings = _settingsProvider.Load().TMDB.RateLimit;
        _maxRequestsPerWindow = settings.MaxRequestsPerWindow;
        var oldLimiter = _limiter;
        _limiter = CreateLimiter(settings.MaxRequestsPerWindow, settings.WindowDurationMs);
        // Dispose the old limiter after a grace period to let any in-flight AcquireAsync calls complete.
        _ = Task.Delay(TimeSpan.FromSeconds(15))
            .ContinueWith(_ => oldLimiter.Dispose(), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
    }

    private static SlidingWindowRateLimiter CreateLimiter(int maxRequests, int windowMs)
        => new(new SlidingWindowRateLimiterOptions
        {
            PermitLimit = maxRequests,
            Window = TimeSpan.FromMilliseconds(windowMs),
            SegmentsPerWindow = 10,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = int.MaxValue,
            AutoReplenishment = true,
        });

    /// <summary>
    /// Signal that TMDB returned a 429. All pending <see cref="EnsureRateAsync{T}"/> calls
    /// and TMDB jobs pause until the backoff window elapses, unless a longer pause is active.
    /// </summary>
    /// <param name="retryAfter">Duration to back off; defaults to 1 second if null.</param>
    public void NotifyRateLimitExceeded(TimeSpan? retryAfter)
    {
        var delay = retryAfter ?? TimeSpan.FromSeconds(1);
        var until = DateTimeOffset.UtcNow + delay;
        bool started;
        lock (_breakerLock)
        {
            // The longer pause wins, whichever its reason.
            if (until.UtcTicks <= Interlocked.Read(ref _backoffUntilTicks))
                return;

            Interlocked.Exchange(ref _backoffUntilTicks, until.UtcTicks);
            started = _pauseReason is not TmdbPauseReason.RateLimited;
            _pauseReason = TmdbPauseReason.RateLimited;
            ArmPauseExpiry(delay);
            if (started)
                _logger.LogInformation(
                    "TMDB is rate limiting requests. All TMDB jobs paused for {Duration} seconds. They will resume automatically.",
                    (int)Math.Ceiling(delay.TotalSeconds));
            else
                _logger.LogTrace("TMDB rate limit exceeded. Backing off until {Until}", until);
        }

        if (started)
            PauseStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Acquire a rate-limit slot, then execute <paramref name="action"/>.
    /// Blocks if the current window is full or a server 429 backoff is active.
    /// </summary>
    public async Task<T> EnsureRateAsync<T>(Func<Task<T>> action)
    {
        while (true)
        {
            await WaitForBackoffAsync(_disposeCts.Token);
            using var lease = await _limiter.AcquireAsync(1, _disposeCts.Token);
            // Re-check backoff: a 429 may have arrived after WaitForBackoffAsync returned
            // but before we acquired the slot. Release the slot and retry rather than
            // holding it idle for the full backoff duration.
            var backoffTicks = Interlocked.Read(ref _backoffUntilTicks);
            if (backoffTicks > 0 && DateTimeOffset.UtcNow.UtcTicks < backoffTicks)
                continue;
            return await action();
        }
    }

    private async Task WaitForBackoffAsync(CancellationToken cancellationToken = default)
    {
        // Loop: a concurrent 429 can arrive mid-wait and push the deadline forward.
        // Re-read the ticks after each delay to catch that case before returning.
        while (true)
        {
            var backoffTicks = Interlocked.Read(ref _backoffUntilTicks);
            if (backoffTicks == 0 || DateTimeOffset.UtcNow.UtcTicks >= backoffTicks)
                return;

            var wait = new DateTimeOffset(backoffTicks, TimeSpan.Zero) - DateTimeOffset.UtcNow;
            if (wait > TimeSpan.Zero)
            {
                var jitter = Jitter();
                _logger.LogTrace("TMDB server backoff active. Waiting {Wait}ms", (wait + jitter).TotalMilliseconds);
                await Task.Delay(wait + jitter, cancellationToken);
            }
        }
    }

    internal static TimeSpan Jitter() => TimeSpan.FromMilliseconds(Random.Shared.Next(0, 50));

    /// <summary>
    /// Exposes the raw backoff deadline for unit tests.
    /// </summary>
    internal long BackoffUntilTicks => Interlocked.Read(ref _backoffUntilTicks);

    /// <summary>
    /// Remaining time on the current backoff, or <see langword="null"/> if no backoff is active.
    /// </summary>
    public TimeSpan? RemainingPauseTime
    {
        get
        {
            var ticks = Interlocked.Read(ref _backoffUntilTicks);
            if (ticks == 0) return null;
            var remaining = new DateTimeOffset(ticks, TimeSpan.Zero) - DateTimeOffset.UtcNow;
            return remaining > TimeSpan.Zero ? remaining : null;
        }
    }

    /// <summary>
    /// Returns <see cref="PauseReason"/> and <see cref="RemainingPauseTime"/> as a single
    /// consistent snapshot so callers don't observe a state change between two separate reads.
    /// </summary>
    /// <returns>Whether TMDB is paused, why, and the time left on the pause.</returns>
    public (bool IsPaused, TmdbPauseReason Reason, TimeSpan? Remaining) GetPauseSnapshot()
    {
        // The pause expiry and NotifySuccess clear _backoffUntilTicks and _pauseReason as a pair
        // under this lock; reading them outside it can observe the pair mid-flip.
        lock (_breakerLock)
        {
            var reason = _pauseReason;
            var ticks = _backoffUntilTicks;
            var remaining = ticks == 0 ? (TimeSpan?)null : new DateTimeOffset(ticks, TimeSpan.Zero) - DateTimeOffset.UtcNow;
            return (reason is not TmdbPauseReason.None, reason, remaining > TimeSpan.Zero ? remaining : null);
        }
    }

    /// <summary>
    /// True while a 429 or 5XX pause is active. Used by the queue acquisition filter
    /// to block TMDB API jobs from starting until the pause elapses.
    /// </summary>
    public bool IsPaused => _pauseReason is not TmdbPauseReason.None;

    /// <summary>
    /// Why TMDB jobs are paused, or <see cref="TmdbPauseReason.None"/> when they are not.
    /// </summary>
    public TmdbPauseReason PauseReason => _pauseReason;

    /// <summary>
    /// Fired when the pause starts, ends or changes its reason.
    /// </summary>
    public event EventHandler? PauseStateChanged;

    /// <summary>
    /// Signal that TMDB returned a 5XX error.
    /// Records the error timestamp in a 3-slot ring buffer; if all 3 slots fall within
    /// the error window, all pending <see cref="EnsureRateAsync{T}"/> calls pause for
    /// an escalating duration, unless a longer pause is active.
    /// </summary>
    public void Notify5xxError()
    {
        var now = DateTimeOffset.UtcNow.UtcTicks;
        var changed = false;

        // All ring-buffer state is read and written under _breakerLock so that the
        // slot write, 3-slot snapshot, and breaker trip are never observed in a partial
        // state by a concurrent caller. Interlocked is not needed here because the lock
        // already provides the necessary memory ordering.
        lock (_breakerLock)
        {
            // Round-robin slot assignment — (uint) cast makes modulo safe across int overflow.
            var slot = (int)((uint)_errorSlot++ % 3);
            _errorTimestamps[slot] = now;

            // If any slot is 0, fewer than 3 errors have ever been recorded.
            var t0 = _errorTimestamps[0];
            var t1 = _errorTimestamps[1];
            var t2 = _errorTimestamps[2];
            var oldest = Math.Min(Math.Min(t0, t1), t2);

            if (oldest == 0 || now - oldest >= _errorWindowTicks)
                return;

            // All 3 errors are within the window — trip the breaker.
            var nextLevel = Math.Min(_5xxPauseLevel + 1, 5);
            var duration = Get5xxPauseDuration(nextLevel);
            var newTicks = (DateTimeOffset.UtcNow + duration).UtcTicks;

            // The level always advances; the pause only replaces a shorter one.
            _5xxPauseLevel = nextLevel;
            if (newTicks > Interlocked.Read(ref _backoffUntilTicks))
            {
                Interlocked.Exchange(ref _backoffUntilTicks, newTicks);
                changed = _pauseReason is not TmdbPauseReason.ServerErrors;
                _pauseReason = TmdbPauseReason.ServerErrors;

                // Time-based recovery, since NotifySuccess never fires while the acquisition filter blocks all TMDB jobs.
                ArmPauseExpiry(duration);
                _logger.LogInformation(
                    "TMDB is temporarily unavailable. All TMDB jobs paused for {Duration} minutes. They will resume automatically.",
                    (int)duration.TotalMinutes);
            }
            else
            {
                _logger.LogDebug("TMDB is temporarily unavailable, but a longer pause is already active.");
            }

            // Clear the ring buffer so further errors from this same still-active pause (e.g. requests
            // that were already in flight when the breaker tripped) don't immediately re-trip and escalate
            // the level again — the next escalation should come from a fresh trio of errors after recovery.
            Array.Clear(_errorTimestamps, 0, _errorTimestamps.Length);
            _errorSlot = 0;
        }

        if (changed)
            PauseStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Arms the expiry timer for the pause's new deadline. Called under <see cref="_breakerLock"/>.
    /// </summary>
    /// <param name="duration">Time until the deadline.</param>
    private void ArmPauseExpiry(TimeSpan duration)
    {
        if (_disposed)
            return;

        // Clamped to what the timer takes; one that fires early re-arms for the rest.
        var dueTime = duration < TimeSpan.Zero ? TimeSpan.Zero : duration > TimeSpan.FromDays(1) ? TimeSpan.FromDays(1) : duration;
        _pauseExpiryTimer.Change(dueTime, Timeout.InfiniteTimeSpan);
    }

    /// <summary>
    /// Lifts the pause once its deadline has passed, so it ends even when no TMDB request runs.
    /// </summary>
    private void OnPauseExpired()
    {
        lock (_breakerLock)
        {
            if (_disposed || _pauseReason is TmdbPauseReason.None)
                return;

            // The timer's clock may run ahead of the wall clock; wait out the rest.
            var remaining = new DateTimeOffset(Interlocked.Read(ref _backoffUntilTicks), TimeSpan.Zero) - DateTimeOffset.UtcNow;
            if (remaining > TimeSpan.Zero)
            {
                ArmPauseExpiry(remaining);
                return;
            }

            Interlocked.Exchange(ref _backoffUntilTicks, 0);
            LogPauseLifted(_pauseReason, expired: true);
            _pauseReason = TmdbPauseReason.None;
        }

        PauseStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Logs the end of a pause.
    /// </summary>
    /// <param name="reason">Why TMDB was paused.</param>
    /// <param name="expired">Whether the pause ran out, rather than ending on a successful request.</param>
    private void LogPauseLifted(TmdbPauseReason reason, bool expired)
    {
        if (reason is TmdbPauseReason.RateLimited)
            _logger.LogInformation("TMDB rate limit pause expired. Queued TMDB jobs will now resume.");
        else if (expired)
            _logger.LogInformation("TMDB pause expired. Queued TMDB jobs will now resume.");
        else
            _logger.LogInformation("TMDB is available again. Queued TMDB jobs will now resume.");
    }

    /// <summary>
    /// Signal that a TMDB request completed successfully.
    /// Resets the ramp level to 0 once a 5XX pause has elapsed, so the next error window starts fresh,
    /// and lifts an elapsed pause the expiry timer has not lifted yet.
    /// </summary>
    public void NotifySuccess()
    {
        // Cheap pre-check to skip the lock entirely in the overwhelmingly common case where the
        // breaker has never tripped and nothing is paused, as every successful TMDB call ends here.
        if (_5xxPauseLevel == 0 && _pauseReason is TmdbPauseReason.None) return;

        lock (_breakerLock)
        {
            // The pause may have been lifted by the expiry timer already (backoffTicks == 0)
            // or may still be active. Only reset once the deadline has passed.
            var backoffTicks = Interlocked.Read(ref _backoffUntilTicks);
            if (backoffTicks > 0 && DateTimeOffset.UtcNow.UtcTicks < backoffTicks)
                return;

            if (_5xxPauseLevel != 0)
            {
                Interlocked.Exchange(ref _backoffUntilTicks, 0);
                _5xxPauseLevel = 0;
                // Clear ring buffer so a single 5xx after recovery doesn't immediately re-trip.
                Array.Clear(_errorTimestamps, 0, _errorTimestamps.Length);
                _errorSlot = 0;
            }

            if (_pauseReason is TmdbPauseReason.None)
                return;

            Interlocked.Exchange(ref _backoffUntilTicks, 0);
            LogPauseLifted(_pauseReason, expired: false);
            _pauseReason = TmdbPauseReason.None;
        }

        PauseStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Maps a ramp level (1–5) to a pause duration.
    /// Called by <see cref="Notify5xxError"/> and exposed internally for tests.
    /// </summary>
    internal static TimeSpan Get5xxPauseDuration(int level) => level switch
    {
        1 => TimeSpan.FromMinutes(1),
        2 => TimeSpan.FromMinutes(3),
        3 => TimeSpan.FromMinutes(5),
        4 => TimeSpan.FromMinutes(15),
        _ => TimeSpan.FromMinutes(60),
    };
}
