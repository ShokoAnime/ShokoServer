using System;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Events;
using Shoko.Server.Settings;

namespace Shoko.Server.Providers.TMDB;

// The 5xx breaker below (Notify5xxError/NotifySuccess) is a hand-rolled fixed-count-in-window
// state machine, not Polly's AdvancedCircuitBreakerPolicy, even though TmdbMetadataService already
// depends on Polly elsewhere. Polly's breaker trips on failure *rate* over a sliding window, a different
// semantic than "3 errors within 10s" — swapping it in would change trip behavior and has no test coverage
// against the new shape. Revisit if the ring-buffer approach needs further tuning.
/// <summary>
/// Rate limiter for the TMDB API (~40 req/sec enforced by TMDB).
/// Uses a sliding window to smooth request distribution and adapts to server-enforced
/// 429 backoff via <see cref="NotifyRateLimitExceeded"/>.
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

    // Guards _5xxPauseLevel and all writes to _backoffUntilTicks so that the level/deadline
    // pair is always updated atomically. Reads of _backoffUntilTicks from WaitForBackoffAsync
    // and EnsureRateAsync happen outside this lock via Interlocked.Read — that is safe because
    // lock exit provides a release fence and Interlocked.Read provides an acquire fence.
    private readonly Lock _breakerLock = new();

    private volatile int _5xxPauseLevel;

    private volatile bool _is5xxPaused;

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
        _settingsProvider.Saved += OnSettingsSaved;
    }

    public void Dispose()
    {
        _settingsProvider.Saved -= OnSettingsSaved;
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
    /// will pause until the backoff window elapses.
    /// </summary>
    /// <param name="retryAfter">Duration to back off; defaults to 1 second if null.</param>
    public void NotifyRateLimitExceeded(TimeSpan? retryAfter)
    {
        var delay = retryAfter ?? TimeSpan.FromSeconds(1);
        var until = DateTimeOffset.UtcNow + delay;
        var newTicks = until.UtcTicks;
        lock (_breakerLock)
        {
            if (newTicks <= Interlocked.Read(ref _backoffUntilTicks))
                return;
            Interlocked.Exchange(ref _backoffUntilTicks, newTicks);
        }
        _logger.LogTrace("TMDB rate limit exceeded. Backing off until {Until}", until);
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

    /// <summary>Exposes the raw backoff deadline for unit tests.</summary>
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
    /// Returns <see cref="Is5xxPaused"/> and <see cref="RemainingPauseTime"/> as a single
    /// consistent snapshot so callers don't observe a state change between two separate reads.
    /// </summary>
    public (bool IsPaused, TimeSpan? Remaining) GetPauseSnapshot()
    {
        // Both fields must be read together under the lock: SchedulePauseExpiry and NotifySuccess
        // clear _backoffUntilTicks and _is5xxPaused as a pair under this same lock, and reading them
        // independently outside it can observe the pair mid-flip (paused but no remaining time, or
        // vice versa).
        lock (_breakerLock)
        {
            var paused = _is5xxPaused;
            var ticks = _backoffUntilTicks;
            var remaining = ticks == 0 ? (TimeSpan?)null : new DateTimeOffset(ticks, TimeSpan.Zero) - DateTimeOffset.UtcNow;
            return (paused, remaining > TimeSpan.Zero ? remaining : null);
        }
    }

    /// <summary>
    /// True while a 5XX circuit-breaker pause is active. Used by the queue acquisition filter
    /// to block TMDB API jobs from starting until the pause elapses.
    /// </summary>
    public bool Is5xxPaused => _is5xxPaused;

    /// <summary>Fired when <see cref="Is5xxPaused"/> transitions between true and false.</summary>
    public event EventHandler? PauseStateChanged;

    /// <summary>
    /// Signal that TMDB returned a 5XX error.
    /// Records the error timestamp in a 3-slot ring buffer; if all 3 slots fall within
    /// the error window, all pending <see cref="EnsureRateAsync{T}"/> calls pause for
    /// an escalating duration.
    /// </summary>
    public void Notify5xxError()
    {
        var now = DateTimeOffset.UtcNow.UtcTicks;

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

            // Always advance level and set pause state when the ring buffer trips,
            // even if a longer 429 backoff is already active — the breaker must engage
            // so the acquisition filter blocks new job dispatch.
            _5xxPauseLevel = nextLevel;
            if (newTicks > Interlocked.Read(ref _backoffUntilTicks))
                Interlocked.Exchange(ref _backoffUntilTicks, newTicks);

            _logger.LogInformation(
                "TMDB is temporarily unavailable. All TMDB jobs paused for {Duration} minutes. They will resume automatically.",
                (int)duration.TotalMinutes);
            var wasAlreadyPaused = _is5xxPaused;
            _is5xxPaused = true;
            if (!wasAlreadyPaused)
                PauseStateChanged?.Invoke(this, EventArgs.Empty);

            // Clear the ring buffer so further errors from this same still-active pause (e.g. requests
            // that were already in flight when the breaker tripped) don't immediately re-trip and escalate
            // the level again — the next escalation should come from a fresh trio of errors after recovery.
            Array.Clear(_errorTimestamps, 0, _errorTimestamps.Length);
            _errorSlot = 0;

            // Schedule time-based recovery so the pause auto-clears even when no
            // TMDB job runs to call NotifySuccess (which would otherwise never fire
            // while the acquisition filter is blocking all TMDB jobs).
            SchedulePauseExpiry(duration);
        }
    }

    private void SchedulePauseExpiry(TimeSpan duration)
    {
        _ = Task.Delay(duration, _disposeCts.Token)
            .ContinueWith(_ =>
            {
                lock (_breakerLock)
                {
                    // A re-trip may have pushed the deadline further out; let that trip's timer handle it.
                    if (Interlocked.Read(ref _backoffUntilTicks) > DateTimeOffset.UtcNow.UtcTicks)
                        return;
                    if (!_is5xxPaused)
                        return;
                    Interlocked.Exchange(ref _backoffUntilTicks, 0);
                    _is5xxPaused = false;
                }
                _logger.LogInformation("TMDB pause expired. Queued TMDB jobs will now resume.");
                PauseStateChanged?.Invoke(this, EventArgs.Empty);
            }, CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);
    }

    /// <summary>
    /// Signal that a TMDB request completed successfully.
    /// Resets the ramp level to 0 once a 5XX pause has elapsed, so the next error window starts fresh.
    /// </summary>
    public void NotifySuccess()
    {
        // Cheap pre-check to skip the lock entirely in the overwhelmingly common case where the
        // breaker has never tripped — every successful TMDB call (up to 10 concurrent) would
        // otherwise funnel through this lock for no reason.
        if (_5xxPauseLevel == 0) return;

        lock (_breakerLock)
        {
            if (_5xxPauseLevel == 0) return;

            // The pause may have been cleared by SchedulePauseExpiry already (backoffTicks == 0)
            // or may still be active. Only reset the ramp once the deadline has passed.
            var backoffTicks = Interlocked.Read(ref _backoffUntilTicks);
            if (backoffTicks > 0 && DateTimeOffset.UtcNow.UtcTicks < backoffTicks)
                return;

            Interlocked.Exchange(ref _backoffUntilTicks, 0);
            _5xxPauseLevel = 0;
            // Clear ring buffer so a single 5xx after recovery doesn't immediately re-trip.
            Array.Clear(_errorTimestamps, 0, _errorTimestamps.Length);
            _errorSlot = 0;
            if (_is5xxPaused)
            {
                _is5xxPaused = false;
                _logger.LogInformation("TMDB is available again. Queued TMDB jobs will now resume.");
                PauseStateChanged?.Invoke(this, EventArgs.Empty);
            }
        }
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
