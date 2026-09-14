using System;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Events;
using Shoko.Server.Settings;

#nullable enable
namespace Shoko.Server.Providers.Anilist;

/// <summary>
/// Rate limiter for the AniList GraphQL API. Mirrors
/// <see cref="TMDB.TmdbRateLimiter"/>: a local sliding window smooths our own
/// request rate, a server-driven backoff honours 429 responses and the
/// per-response quota headers, and a 5XX circuit breaker pauses all AniList
/// jobs while the upstream is unhealthy. Unlike the TMDB breaker it trips on
/// the very first server error: AniList has been fragile, and a client that
/// keeps knocking during an outage makes it worse. Jobs re-queue and resume on
/// their own once the pause lifts, so no data is lost by waiting.
/// </summary>
public sealed class AnilistRateLimiter : IDisposable
{
    private readonly ILogger<AnilistRateLimiter> _logger;

    private readonly ConfigurationProvider<ServerSettings> _settingsProvider;

    private volatile SlidingWindowRateLimiter _limiter;

    private volatile int _maxRequestsPerWindow;

    private volatile int _configuredMaxRequestsPerWindow;

    private volatile int _windowDurationMs;

    private volatile int _serverLimit = -1;

    private long _backoffUntilTicks;

    // Errors within this window of the previous pause escalate the level instead of restarting at 1.
    private readonly long _errorWindowTicks;

    private long _lastErrorTicks;

    // Guards _5xxPauseLevel and all writes to _backoffUntilTicks so the level/deadline pair is
    // always updated atomically. Reads elsewhere go through Interlocked.Read.
    private readonly Lock _breakerLock = new();

    private volatile int _5xxPauseLevel;

    private volatile bool _is5xxPaused;

    private volatile int _remainingRequests = -1;

    private readonly CancellationTokenSource _disposeCts = new();

    /// <summary>
    /// Remaining request capacity in the current local window.
    /// </summary>
    public int RemainingInWindow =>
        (int)(_limiter.GetStatistics()?.CurrentAvailablePermits ?? _maxRequestsPerWindow);

    /// <summary>
    /// Requests remaining in the server-side quota window, as last reported by
    /// AniList's <c>X-RateLimit-Remaining</c> header, or <see langword="null"/>
    /// before the first response.
    /// </summary>
    public int? RemainingRequests => _remainingRequests < 0 ? null : _remainingRequests;

    public AnilistRateLimiter(ILogger<AnilistRateLimiter> logger, ConfigurationProvider<ServerSettings> settingsProvider)
        : this(logger, settingsProvider, TimeSpan.FromMinutes(5)) { }

    internal AnilistRateLimiter(ILogger<AnilistRateLimiter> logger, ConfigurationProvider<ServerSettings> settingsProvider, TimeSpan errorWindow)
    {
        _logger = logger;
        _settingsProvider = settingsProvider;
        _errorWindowTicks = errorWindow.Ticks;
        var settings = settingsProvider.Load().Anilist.RateLimit;
        _configuredMaxRequestsPerWindow = settings.MaxRequestsPerWindow;
        _windowDurationMs = settings.WindowDurationMs;
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
    }

    private void OnSettingsSaved(object? sender, ConfigurationSavedEventArgs<ServerSettings> eventArgs)
    {
        var settings = _settingsProvider.Load().Anilist.RateLimit;
        _configuredMaxRequestsPerWindow = settings.MaxRequestsPerWindow;
        _windowDurationMs = settings.WindowDurationMs;
        ReplaceLimiter(EffectiveLimit());
    }

    /// <summary>
    /// The length of the window AniList's advertised limit applies to.
    /// </summary>
    private static readonly TimeSpan ServerWindow = TimeSpan.FromMinutes(1);

    // The configured limit is a pace the user chose; the server-advertised limit, scaled from its
    // one-minute window down to ours, is the ceiling we never exceed.
    private int EffectiveLimit()
    {
        if (_serverLimit <= 0)
            return _configuredMaxRequestsPerWindow;

        var serverAllowance = Math.Max(1, (int)Math.Floor(_serverLimit * (_windowDurationMs / ServerWindow.TotalMilliseconds)));
        return Math.Min(_configuredMaxRequestsPerWindow, serverAllowance);
    }

    private void ReplaceLimiter(int maxRequests)
    {
        if (maxRequests == _maxRequestsPerWindow)
            return;

        _maxRequestsPerWindow = maxRequests;
        var oldLimiter = _limiter;
        _limiter = CreateLimiter(maxRequests, _windowDurationMs);
        // Dispose the old limiter after a grace period to let any in-flight AcquireAsync calls complete.
        _ = Task.Delay(TimeSpan.FromSeconds(15))
            .ContinueWith(_ => oldLimiter.Dispose(), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
    }

    /// <summary>
    /// The request limit per window currently enforced locally, after applying
    /// the limit AniList advertises in its <c>X-RateLimit-Limit</c> header.
    /// </summary>
    public int MaxRequestsPerWindow => _maxRequestsPerWindow;

    private static SlidingWindowRateLimiter CreateLimiter(int maxRequests, int windowMs)
        => new(new SlidingWindowRateLimiterOptions
        {
            PermitLimit = maxRequests,
            Window = TimeSpan.FromMilliseconds(windowMs),
            SegmentsPerWindow = 12,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = int.MaxValue,
            AutoReplenishment = true,
        });

    /// <summary>
    /// Signal that AniList returned a 429. All pending <see cref="EnsureRateAsync{T}"/> calls
    /// will pause until the backoff window elapses.
    /// </summary>
    /// <param name="retryAfter">Duration to back off; defaults to 60 seconds, which is AniList's quota window.</param>
    public void NotifyRateLimitExceeded(TimeSpan? retryAfter)
    {
        var delay = retryAfter ?? TimeSpan.FromSeconds(60);
        SetBackoff(DateTimeOffset.UtcNow + delay, "AniList rate limit exceeded");
    }

    /// <summary>
    /// Record the quota headers from a response. When the quota is exhausted
    /// and the reset time is known, back off until then so we don't burn a
    /// request just to be told no.
    /// </summary>
    /// <param name="limit">The <c>X-RateLimit-Limit</c> value, if present. Lowers the local window when AniList advertises less than we're configured for.</param>
    /// <param name="remaining">The <c>X-RateLimit-Remaining</c> value.</param>
    /// <param name="resetAt">The <c>X-RateLimit-Reset</c> value, if present.</param>
    public void NotifyQuota(int? limit, int remaining, DateTimeOffset? resetAt)
    {
        if (limit is > 0 && limit.Value != _serverLimit)
        {
            _serverLimit = limit.Value;
            var effective = EffectiveLimit();
            if (effective != _maxRequestsPerWindow)
                _logger.LogInformation("AniList advertises {ServerLimit} requests per minute. Using {Effective} per {Window}s window locally.", limit.Value, effective, _windowDurationMs / 1000d);
            ReplaceLimiter(effective);
        }

        _remainingRequests = Math.Max(remaining, 0);
        if (remaining > 0 || resetAt is null)
            return;

        SetBackoff(resetAt.Value, "AniList request quota exhausted");
    }

    private void SetBackoff(DateTimeOffset until, string reason)
    {
        var newTicks = until.UtcTicks;
        lock (_breakerLock)
        {
            if (newTicks <= Interlocked.Read(ref _backoffUntilTicks))
                return;
            Interlocked.Exchange(ref _backoffUntilTicks, newTicks);
        }
        _logger.LogTrace("{Reason}. Backing off until {Until}", reason, until);
    }

    /// <summary>
    /// Acquire a rate-limit slot, then execute <paramref name="action"/>.
    /// Blocks if the current window is full or a server backoff is active.
    /// </summary>
    public async Task<T> EnsureRateAsync<T>(Func<Task<T>> action)
    {
        while (true)
        {
            await WaitForBackoffAsync(_disposeCts.Token).ConfigureAwait(false);
            using var lease = await _limiter.AcquireAsync(1, _disposeCts.Token).ConfigureAwait(false);
            // Re-check backoff: a 429 may have arrived after WaitForBackoffAsync returned
            // but before we acquired the slot. Release the slot and retry rather than
            // holding it idle for the full backoff duration.
            var backoffTicks = Interlocked.Read(ref _backoffUntilTicks);
            if (backoffTicks > 0 && DateTimeOffset.UtcNow.UtcTicks < backoffTicks)
                continue;
            return await action().ConfigureAwait(false);
        }
    }

    private async Task WaitForBackoffAsync(CancellationToken cancellationToken = default)
    {
        // Loop: a concurrent 429 can arrive mid-wait and push the deadline forward.
        while (true)
        {
            var backoffTicks = Interlocked.Read(ref _backoffUntilTicks);
            if (backoffTicks == 0 || DateTimeOffset.UtcNow.UtcTicks >= backoffTicks)
                return;

            var wait = new DateTimeOffset(backoffTicks, TimeSpan.Zero) - DateTimeOffset.UtcNow;
            if (wait > TimeSpan.Zero)
            {
                var jitter = Jitter();
                _logger.LogTrace("AniList server backoff active. Waiting {Wait}ms", (wait + jitter).TotalMilliseconds);
                await Task.Delay(wait + jitter, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    internal static TimeSpan Jitter() => TimeSpan.FromMilliseconds(Random.Shared.Next(0, 250));

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
    /// Returns the pause state and remaining time as a single consistent snapshot.
    /// </summary>
    public (bool IsPaused, TimeSpan? Remaining, int? RemainingRequests) GetPauseSnapshot()
    {
        lock (_breakerLock)
        {
            var ticks = _backoffUntilTicks;
            var remaining = ticks == 0 ? (TimeSpan?)null : new DateTimeOffset(ticks, TimeSpan.Zero) - DateTimeOffset.UtcNow;
            remaining = remaining > TimeSpan.Zero ? remaining : null;
            return (_is5xxPaused || remaining is not null, remaining, RemainingRequests);
        }
    }

    /// <summary>
    /// True while a 5XX circuit-breaker pause is active. Used by the queue acquisition filter
    /// to block AniList API jobs from starting until the pause elapses.
    /// </summary>
    public bool Is5xxPaused => _is5xxPaused;

    /// <summary>
    /// Fired when <see cref="Is5xxPaused"/> transitions between true and false.
    /// </summary>
    public event EventHandler? PauseStateChanged;

    /// <summary>
    /// Signal that AniList returned a 5XX error. The first one trips the
    /// breaker; further errors while it is already tripped are absorbed, and
    /// an error soon after a pause lifted escalates the next pause.
    /// </summary>
    public void Notify5xxError()
    {
        var now = DateTimeOffset.UtcNow.UtcTicks;
        lock (_breakerLock)
        {
            // Requests already in flight when the breaker tripped will fail too; don't escalate on those.
            if (_is5xxPaused)
                return;

            // A fresh error long after the last one starts the ramp over; a quick repeat escalates it.
            if (_lastErrorTicks != 0 && now - _lastErrorTicks >= _errorWindowTicks)
                _5xxPauseLevel = 0;
            _lastErrorTicks = now;

            var nextLevel = Math.Min(_5xxPauseLevel + 1, 5);
            var duration = Get5xxPauseDuration(nextLevel);
            var newTicks = (DateTimeOffset.UtcNow + duration).UtcTicks;

            _5xxPauseLevel = nextLevel;
            if (newTicks > Interlocked.Read(ref _backoffUntilTicks))
                Interlocked.Exchange(ref _backoffUntilTicks, newTicks);

            _logger.LogInformation(
                "AniList is temporarily unavailable. All AniList jobs paused for {Duration} minutes. They will resume automatically.",
                (int)duration.TotalMinutes);
            _is5xxPaused = true;
            PauseStateChanged?.Invoke(this, EventArgs.Empty);

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
                    if (Interlocked.Read(ref _backoffUntilTicks) > DateTimeOffset.UtcNow.UtcTicks)
                        return;
                    if (!_is5xxPaused)
                        return;
                    Interlocked.Exchange(ref _backoffUntilTicks, 0);
                    _is5xxPaused = false;
                }
                _logger.LogInformation("AniList pause expired. Queued AniList jobs will now resume.");
                PauseStateChanged?.Invoke(this, EventArgs.Empty);
            }, CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);
    }

    /// <summary>
    /// Signal that an AniList request completed successfully. Resets the ramp
    /// level once a 5XX pause has elapsed, so the next outage starts at the
    /// shortest pause again.
    /// </summary>
    public void NotifySuccess()
    {
        if (_5xxPauseLevel == 0) return;

        lock (_breakerLock)
        {
            if (_5xxPauseLevel == 0) return;

            var backoffTicks = Interlocked.Read(ref _backoffUntilTicks);
            if (backoffTicks > 0 && DateTimeOffset.UtcNow.UtcTicks < backoffTicks)
                return;

            Interlocked.Exchange(ref _backoffUntilTicks, 0);
            _5xxPauseLevel = 0;
            _lastErrorTicks = 0;
            if (_is5xxPaused)
            {
                _is5xxPaused = false;
                _logger.LogInformation("AniList is available again. Queued AniList jobs will now resume.");
                PauseStateChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    internal static TimeSpan Get5xxPauseDuration(int level) => level switch
    {
        1 => TimeSpan.FromMinutes(1),
        2 => TimeSpan.FromMinutes(3),
        3 => TimeSpan.FromMinutes(5),
        4 => TimeSpan.FromMinutes(15),
        _ => TimeSpan.FromMinutes(60),
    };
}
